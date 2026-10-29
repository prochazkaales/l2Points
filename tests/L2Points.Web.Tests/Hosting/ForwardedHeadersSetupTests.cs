using System.Net;
using L2Points.Web.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace L2Points.Web.Tests.Hosting;

public class ForwardedHeadersSetupTests
{
    private static async Task<HttpContext> RunAsync(bool trustAllProxies)
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Configure(options, trustAllProxies);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.7";

        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
        await middleware.Invoke(context);
        return context;
    }

    [Fact]
    public async Task Trusted_proxy_mode_applies_forwarded_proto_from_any_address()
    {
        var context = await RunAsync(trustAllProxies: true);

        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse("203.0.113.7"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task Default_mode_ignores_forwarded_headers_from_non_loopback()
    {
        var context = await RunAsync(trustAllProxies: false);

        Assert.Equal("http", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse("10.0.0.5"), context.Connection.RemoteIpAddress);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Both_modes_forward_for_and_proto_with_limit_one(bool trustAllProxies)
    {
        var options = new ForwardedHeadersOptions();

        ForwardedHeadersSetup.Configure(options, trustAllProxies);

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
    }
}
