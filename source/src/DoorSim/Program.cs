using System.Text.Json.Serialization;
using DoorSim.Endpoints;
using DoorSim.OpenApi;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;

AppDomain.CurrentDomain.UnhandledException += (_, args) =>
{
    var ex = args.ExceptionObject as Exception;
    Log.Fatal(ex, "Unhandled exception in AppDomain (IsTerminating={IsTerminating}): {Message}",
        args.IsTerminating, ex?.Message);
};

TaskScheduler.UnobservedTaskException += (_, args) =>
{
    Log.Warning(args.Exception, "Unobserved task exception: {Message}", args.Exception.Message);
    args.SetObserved();
};

var builder = WebApplication.CreateBuilder(args);

// LogEventBus is constructed here, before UseSerilog, because Serilog sinks are built inside
// the UseSerilog configuration lambda — which runs before builder.Build(), i.e. before the DI
// container exists. The same instance is registered into DI below so LogsEndpoints can inject it.
var logBus = new LogEventBus();
var logDirectory = builder.Configuration["Logs:Directory"]
                   ?? Path.Combine(builder.Environment.ContentRootPath, "logs");

builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
        // OSDP.Net logs TimeoutException as "Unexpected exception in polling loop" at Error level
        // whenever the serial port read times out between panel polls. This is normal inter-poll
        // silence, not a real error. Filter it out so genuine OSDP errors remain visible.
        .Filter.ByExcluding(e =>
            e.Exception is TimeoutException &&
            e.Properties.TryGetValue("SourceContext", out var sc) &&
            sc.ToString().Contains("OSDP.Net"))
        .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.Sink(new LogBroadcastSink(logBus))
        .WriteTo.File(
            Path.Combine(logDirectory, "doorsim-.log"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");

    var seqUrl = ctx.Configuration["Seq:ServerUrl"];
    if (!string.IsNullOrWhiteSpace(seqUrl))
        lc.WriteTo.Seq(seqUrl);
});

// -------------------------------------------------------------------------
// Services
// -------------------------------------------------------------------------

// Hardware — singleton to hold GPIO pins for app lifetime.
// Registered as GpioController? (nullable) so DI injects null when GPIO is unavailable.
#pragma warning disable CS8634 // nullable type arg doesn't match 'class' constraint — intentional for optional hardware
builder.Services.AddSingleton<GpioController?>(sp =>
#pragma warning restore CS8634
{
    var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("GPIO");
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        log.LogWarning("Not running on Linux — GPIO disabled (dev mode)");

        return null;
    }

    try
    {
        var gpio = new GpioController();
        log.LogInformation("GPIO controller initialized");

        return gpio;
    }
    catch (Exception ex)
    {
        log.LogWarning(ex, "Failed to open GPIO controller — simulation-only mode");

        return null;
    }
});

builder.Services.AddSingleton<ModbusRelayService>();
builder.Services.AddSingleton<ModbusTcpRelayService>();
builder.Services.AddSingleton<SimulationSettingsService>();
builder.Services.AddSingleton<DynamicSimulatorBank>();
builder.Services.AddSingleton<IReaderBank>(sp => sp.GetRequiredService<DynamicSimulatorBank>());
builder.Services.AddSingleton<SimulationMetricsService>();
builder.Services.AddSingleton<SimulationOrchestrator>();

// Registers the same LogEventBus instance the Serilog sink (constructed above, pre-DI) writes
// into, so LogsEndpoints.MapLogsEndpoints can inject it.
builder.Services.AddSingleton(logBus);

// Data
builder.Services.AddDbContext<DoorSimDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DoorSim")
                  ?? "Data Source=doorsim.db"));

builder.Services.AddScoped<CardLibraryService>();
builder.Services.AddScoped<CardFormatService>();
builder.Services.AddScoped<DoorConfigService>();

builder.Services.AddProblemDetails();

// Enums serialize as their string name (e.g. "Wiegand26") rather than the default integer —
// self-explanatory on the wire and in the generated OpenAPI schema. Keep in sync with the
// Blazor client, which uses the matching DoorSimJson.Options for the same reason.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// API documentation — always available, not gated to Development.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "DoorSim API",
            Version = "v1",
            Description = "Access-control reader/door simulator API — "
                          + "card library, door configuration, and simulation control."
        };

        return Task.CompletedTask;
    });

    options.AddSchemaTransformer<EnumDescriptionSchemaTransformer>();
});

// Honor forwarded headers from reverse proxies / Cloudflare Tunnel (X-Forwarded-Proto, X-Forwarded-Host)
// so generated URLs in OpenAPI / Scalar schema use HTTPS when hosted behind an SSL-terminating tunnel.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                               | ForwardedHeaders.XForwardedProto
                               | ForwardedHeaders.XForwardedHost;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// -------------------------------------------------------------------------
// Build
// -------------------------------------------------------------------------

var app = builder.Build();

// Initialize database schema
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DoorSimDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger(nameof(DatabaseInitializer));

    await DatabaseInitializer.InitializeAsync(db, logger);
}

// Load global simulation settings, then door configurations
await app.Services.GetRequiredService<SimulationSettingsService>().LoadAsync();
await app.Services.GetRequiredService<DynamicSimulatorBank>().LoadFromDbAsync();
await app.Services.GetRequiredService<SimulationMetricsService>().LoadAsync();

// -------------------------------------------------------------------------
// Middleware
// -------------------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseWebAssemblyDebugging();
}
else
    app.UseExceptionHandler();

app.UseForwardedHeaders();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();

// -------------------------------------------------------------------------
// Endpoints
// -------------------------------------------------------------------------

app.MapCardsEndpoints();
app.MapCardFormatsEndpoints();
app.MapDoorsEndpoints();
app.MapSimulationEndpoints();
app.MapSettingsEndpoints();
app.MapMetricsEndpoints();
app.MapLogsEndpoints();

// API documentation — always available, not gated to Development.
// ScalarOptions.ProxyUrl defaults to null (no proxy), which is what we want for
// this local-network tool — "Try it" hits the API directly.
app.MapOpenApi(); // serves /openapi/v1.json
app.MapScalarApiReference(options => options.WithTitle("DoorSim API")); // UI at /scalar/v1

app.MapFallbackToFile("index.html");

app.Run();
