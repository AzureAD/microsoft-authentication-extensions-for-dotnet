// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Identity.Client.Extensions.Web.TokenCacheProviders.Sql
{
    /// <summary>
    /// 
    /// </summary>
    public static class MsalAppSqlTokenCacheProviderExtension
    {
        /// <summary>Adds the app and per user SQL token caches.</summary>
        /// <param name="services">The services collection to add to.</param>
        /// <param name="sqlTokenCacheOptions">The MSALSqlTokenCacheOptions is used by the caller to specify the Sql connection string</param>
        /// <returns></returns>
        public static IServiceCollection AddSqlTokenCaches(
            this IServiceCollection services,
            MsalSqlTokenCacheOptions sqlTokenCacheOptions)
        {
            AddSqlAppTokenCache(services, sqlTokenCacheOptions);
            AddSqlPerUserTokenCache(services, sqlTokenCacheOptions);
            return services;
        }

        /// <summary>Adds the Sql Server based application token cache to the service collection.</summary>
        /// <param name="services">The services collection to add to.</param>
        /// <param name="sqlTokenCacheOptions">The MSALSqlTokenCacheOptions is used by the caller to specify the Sql connection string</param>
        /// <returns></returns>
        public static IServiceCollection AddSqlAppTokenCache(
            this IServiceCollection services,
            MsalSqlTokenCacheOptions sqlTokenCacheOptions)
        {
            // Uncomment the following lines to create the database. In production scenarios, the database
            // will most probably be already present.
            //var tokenCacheDbContextBuilder = new DbContextOptionsBuilder<TokenCacheDbContext>();
            //tokenCacheDbContextBuilder.UseSqlServer(configuration.GetConnectionString("TokenCacheDbConnStr"));

            //var tokenCacheDbContext = new TokenCacheDbContext(tokenCacheDbContextBuilder.Options);
            //tokenCacheDbContext.Database.EnsureCreated();

            services.AddDataProtection();

            services.AddDbContext<TokenCacheDbContext>(options =>
                options.UseSqlServer(sqlTokenCacheOptions.SqlConnectionString));

            services.AddScoped<IMsalAppTokenCacheProvider>(factory =>
            {
                var dpprovider = factory.GetRequiredService<IDataProtectionProvider>();
                var tokenCacheDbContext = factory.GetRequiredService<TokenCacheDbContext>();
                var optionsMonitor = factory.GetRequiredService<IOptionsMonitor<AzureADOptions>>();

                return new MsalAppSqlTokenCacheProvider(tokenCacheDbContext, optionsMonitor, dpprovider);
            });

            return services;
        }

        /// <summary>Adds the Sql Server based per user token cache to the service collection.</summary>
        /// <param name="services">The services collection to add to.</param>
        /// <param name="sqlTokenCacheOptions">The MSALSqlTokenCacheOptions is used by the caller to specify the Sql connection string</param>
        /// <returns></returns>
        public static IServiceCollection AddSqlPerUserTokenCache(
            this IServiceCollection services,
            MsalSqlTokenCacheOptions sqlTokenCacheOptions)
        {
            // Uncomment the following lines to create the database. In production scenarios, the database
            // will most probably be already present.
            // var tokenCacheDbContextBuilder = new DbContextOptionsBuilder<TokenCacheDbContext>();
            // tokenCacheDbContextBuilder.UseSqlServer(configuration.GetConnectionString("TokenCacheDbConnStr"));

            // var tokenCacheDbContext = new TokenCacheDbContext(tokenCacheDbContextBuilder.Options);
            // tokenCacheDbContext.Database.EnsureCreated();

            services.AddDataProtection();

            services.AddDbContext<TokenCacheDbContext>(options =>
                options.UseSqlServer(sqlTokenCacheOptions.SqlConnectionString));

            services.AddScoped<IMsalUserTokenCacheProvider>(factory =>
            {
                var dpprovider = factory.GetRequiredService<IDataProtectionProvider>();
                var tokenCacheDbContext = factory.GetRequiredService<TokenCacheDbContext>();

                return new MsalPerUserSqlTokenCacheProvider(tokenCacheDbContext, dpprovider);
            });

            return services;
        }
    }
}
