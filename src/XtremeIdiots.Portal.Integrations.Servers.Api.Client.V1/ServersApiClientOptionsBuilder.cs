using System;
using MX.Api.Client.Configuration;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    /// <summary>
    /// Builder for configuring Servers API client options.
    /// </summary>
    /// <remarks>
    /// Overrides <see cref="ApiClientOptionsBuilder{TOptions, TBuilder}.WithCaching(Action{CacheBuilder})"/>
    /// to capture the consumer's cache configuration so
    /// <see cref="ServiceCollectionExtensions.AddServersApiClient"/> can promote it into a
    /// <see cref="SharedCacheConfiguration"/> that is applied across every typed sub-API. This avoids the
    /// per-typed-client scoping crash you get if the same delegate is applied via
    /// <c>AddTypedApiClient</c> multiple times with expressions targeting different sub-API interfaces.
    /// </remarks>
    public class ServersApiClientOptionsBuilder : ApiClientOptionsBuilder<ServersApiClientOptions, ServersApiClientOptionsBuilder>
    {
        internal Action<CacheBuilder>? CapturedCacheConfigure { get; private set; }

        public new ServersApiClientOptionsBuilder WithCaching(Action<CacheBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            CapturedCacheConfigure = configure;
            return this;
        }
    }
}
