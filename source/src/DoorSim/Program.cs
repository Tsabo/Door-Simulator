using DoorSim.Data;
using DoorSim.Endpoints;
using DoorSim.Hardware;
using DoorSim.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    // OSDP.Net logs TimeoutException as "Unexpected exception in polling loop" at Error level
    // whenever the serial port read times out between panel polls. This is normal inter-poll
    // silence, not a real error. Filter it out so genuine OSDP errors remain visible.
    .Filter.ByExcluding(e =>
        e.Exception is TimeoutException &&
        e.Properties.TryGetValue("SourceContext", out var sc) &&
        sc.ToString().Contains("OSDP.Net"))
    .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

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
builder.Services.AddSingleton<SimulationOrchestrator>();

// Data
builder.Services.AddDbContext<DoorSimDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DoorSim")
        ?? "Data Source=doorsim.db"));

builder.Services.AddScoped<CardLibraryService>();
builder.Services.AddScoped<DoorConfigService>();

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

// -------------------------------------------------------------------------
// Middleware
// -------------------------------------------------------------------------

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();

// -------------------------------------------------------------------------
// Endpoints
// -------------------------------------------------------------------------

app.MapCardsEndpoints();
app.MapDoorsEndpoints();
app.MapSimulationEndpoints();
app.MapSettingsEndpoints();

app.MapFallbackToFile("index.html");

app.Run();
