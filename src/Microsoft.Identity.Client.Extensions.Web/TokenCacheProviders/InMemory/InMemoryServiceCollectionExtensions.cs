﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Identity.Client.Extensions.Web.TokenCacheProviders.InMemory
{
    /// <summary>
    /// 
    /// </summary>
    public static class InMemoryServiceCollectionExtensions
    {
        /// <summary>Adds both the app and per-user in-memory token caches.</summary>
        /// <param name="services">The services collection to add to.</param>
        /// <param name="cacheOptions">the MSALMemoryTokenCacheOptions allows the caller to set the token cache expiration</param>
        /// <returns></returns>
        public static IServiceCollection AddInMemoryTokenCaches(
            this IServiceCollection services,
            MsalMemoryTokenCacheOptions cacheOptions = null)
        {
            var memoryCacheoptions = (cacheOptions == null)
                ? new MsalMemoryTokenCacheOptions
                    {
                        AbsoluteExpiration = DateTimeOffset.Now.AddDays(14)
                    }
                : cacheOptions;

            AddInMemoryAppTokenCache(services, memoryCacheoptions);
            AddInMemoryPerUserTokenCache(services, memoryCacheoptions);
            return services;
        }


        /// <summary>Adds the in-memory based application token cache to the service collection.</summary>
        /// <param name="services">The services collection to add to.</param>
        /// <param name="cacheOptions">the MSALMemoryTokenCacheOptions allows the caller to set the token cache expiration</param>
        public static IServiceCollection AddInMemoryAppTokenCache(
            this IServiceCollection services,
            MsalMemoryTokenCacheOptions cacheOptions)
        {
            services.AddMemoryCache();

            services.AddSingleton<IMsalAppTokenCacheProvider>(factory =>
            {
                var memoryCache = factory.GetRequiredService<IMemoryCache>();
                var optionsMonitor = factory.GetRequiredService<IOptionsMonitor<AzureADOptions>>();

                return new MsalAppMemoryTokenCacheProvider(memoryCache, cacheOptions, optionsMonitor);
            });

            return services;
        }

        /// <summary>Adds the in-memory based per user token cache to the service collection.</summary>
        /// <param name="services">The services collection to add to.</param>
        /// <param name="cacheOptions">the MSALMemoryTokenCacheOptions allows the caller to set the token cache expiration</param>
        /// <returns></returns>
        public static IServiceCollection AddInMemoryPerUserTokenCache(
            this IServiceCollection services,
            MsalMemoryTokenCacheOptions cacheOptions)
        {
            services.AddMemoryCache();

            services.AddSingleton<IMsalUserTokenCacheProvider>(factory =>
            {
                var memoryCache = factory.GetRequiredService<IMemoryCache>();
                return new MsalPerUserMemoryTokenCacheProvider(memoryCache, cacheOptions);
            });

            return services;
        }
    }
}
