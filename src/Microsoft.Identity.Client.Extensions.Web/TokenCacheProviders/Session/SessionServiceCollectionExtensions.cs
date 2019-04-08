// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Identity.Client.Extensions.Web.TokenCacheProviders.Session
{
    /// <summary>
    /// 
    /// </summary>
    public static class SessionServiceCollectionExtensions
    {
        /// <summary>Adds both App and per-user session token caches.</summary>
        /// <param name="services">The services collection to add to.</param>
        /// <returns></returns>
        public static IServiceCollection AddSessionTokenCaches(this IServiceCollection services)
        {
            AddSessionAppTokenCache(services);
            AddSessionPerUserTokenCache(services);

            return services;
        }

        /// <summary>Adds the Http session based application token cache to the service collection.</summary>
        /// <param name="services">The services collection to add to.</param>
        /// <returns></returns>
        public static IServiceCollection AddSessionAppTokenCache(this IServiceCollection services)
        {
            services.AddScoped<IMsalAppTokenCacheProvider>(factory =>
            {
                var optionsMonitor = factory.GetRequiredService<IOptionsMonitor<AzureADOptions>>();

                return new MsalAppSessionTokenCacheProvider(optionsMonitor);
            });

            return services;
        }

        /// <summary>Adds the http session based per user token cache to the service collection.</summary>
        /// <param name="services">The services collection to add to.</param>
        /// <returns></returns>
        public static IServiceCollection AddSessionPerUserTokenCache(this IServiceCollection services)
        {
            services.AddScoped<IMsalUserTokenCacheProvider, MsalPerUserSessionTokenCacheProvider>();
            return services;
        }
    }
}
