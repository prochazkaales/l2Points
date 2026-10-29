using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace L2Points.Web.Hosting;

public static class ForwardedHeadersSetup
{
    public const string TrustAllProxiesKey = "ForwardedHeaders:TrustAllProxies";

    public static void Configure(ForwardedHeadersOptions options, bool trustAllProxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;

        if (trustAllProxies)
        {
            // In Azure the ingress is the only path to the container and its IP is not fixed.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
        }
    }
}
