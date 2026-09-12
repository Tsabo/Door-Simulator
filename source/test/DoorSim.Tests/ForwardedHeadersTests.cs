using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace DoorSim.Tests;

public class ForwardedHeadersTests
{
    [Test]
    public async Task ProcessHeaders_UpdatesSchemeAndHost_WhenForwardedHeadersPresent()
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto
                             | ForwardedHeaders.XForwardedHost
        };
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        var middleware = new ForwardedHeadersMiddleware(
            next: _ => Task.CompletedTask,
            loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            options: Options.Create(options)
        );

        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 5000);
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-Host"] = "doorsim.browns.info";

        await middleware.Invoke(context);

        await Assert.That(context.Request.Scheme).IsEqualTo("https");
        await Assert.That(context.Request.Host.Value).IsEqualTo("doorsim.browns.info");
    }
}
