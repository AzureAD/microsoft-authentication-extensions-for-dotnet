// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client.AppConfig;

namespace Microsoft.Identity.Client.Extensions.Msal.Providers
{
    /// <summary>
    /// SharedTokenCacheProbe (wip) provides shared access to tokens from the Microsoft family of products.
    /// This probe will provided access to tokens from accounts that have been authenticated in other Microsoft products to provide a single sign-on experience.
    /// </summary>
    public class SharedTokenCacheProvider : ITokenProvider
    {
        private static readonly string CacheFilePath =
            Path.Combine(SharedUtilities.GetUserRootDirectory(), "msal.cache");
        private readonly IPublicClientApplication _app;
        private readonly MsalCacheHelper _cacheHelper;
        private readonly IConfiguration _config;
        private readonly ILogger _logger;

        /// <inheritdoc />
        public SharedTokenCacheProvider(IConfiguration config = null, ILogger logger = null)
        {
            _logger = logger;
            _config = config ?? new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var authority = string.Format(CultureInfo.InvariantCulture,
                AadAuthority.AadCanonicalAuthorityTemplate,
                AadAuthority.DefaultTrustedHost,
                "common");
            var builder = new StorageCreationPropertiesBuilder(Path.GetFileName(CacheFilePath), Path.GetDirectoryName(CacheFilePath));
            builder = builder.WithMacKeyChain(serviceName: "Microsoft.Developer.IdentityService", accountName: "MSALCache");
            builder = builder.WithLinuxKeyring(
                schemaName: "msal.cache",
                collection: "default",
                secretLabel: "MSALCache",
                attribute1: new KeyValuePair<string, string>("MsalClientID", "Microsoft.Developer.IdentityService"),
                attribute2: new KeyValuePair<string, string>("MsalClientVersion", "1.0.0.0"));
            var storageCreationProperties = builder.Build();
            _app = PublicClientApplicationBuilder
                .Create("04b07795-8ddb-461a-bbee-02f9e1bf7b46")
                .WithAuthority(new Uri(authority))
                .Build();
            _cacheHelper = MsalCacheHelper.RegisterCache(_app.UserTokenCache, storageCreationProperties);
        }

        /// <inheritdoc />
        public async Task<bool> AvailableAsync()
        {
            Log(Microsoft.Extensions.Logging.LogLevel.Information, "checking for accounts in shared developer tool cache");
            var accounts = await GetAccountsAsync().ConfigureAwait(false);
            var available = accounts.Any();
            Log(Microsoft.Extensions.Logging.LogLevel.Information, $"provider available: {available}");
            return available;
        }

        /// <inheritdoc />
        public async Task<IToken> GetTokenAsync(IEnumerable<string> scopes)
        {
            Log(Microsoft.Extensions.Logging.LogLevel.Information, "checking for accounts in shared developer tool cache");
            var accounts = (await GetAccountsAsync().ConfigureAwait(false)).ToList();
            if(!accounts.Any())
            {
                throw new InvalidOperationException("there are no accounts available to acquire a token");
            }
            var res = await _app.AcquireTokenSilentAsync(scopes, accounts.First()).ConfigureAwait(false);
            return new AccessTokenWithExpiration { ExpiresOn = res.ExpiresOn, AccessToken = res.AccessToken };
        }

        private async Task<IEnumerable<IAccount>> GetAccountsAsync()
        {
            var accounts = (await _app.GetAccountsAsync().ConfigureAwait(false)).ToList();
            if (accounts.Any())
            {
                Log(Microsoft.Extensions.Logging.LogLevel.Information, $"found the following account usernames: {string.Join(", ", accounts.Select(i => i.Username))}");
            }
            else
            {
                const string msg = "no accounts found in the shared cache -- perhaps, log into Visual Studio, Azure CLI, Azure PowerShell, etc";
                Log(Microsoft.Extensions.Logging.LogLevel.Information, msg);
            }
            var username = _config.GetValue<string>(Constants.AzurePreferredAccountUsernameEnvName);
            if (!string.IsNullOrWhiteSpace(username))
            {
                Log(Microsoft.Extensions.Logging.LogLevel.Information, $"since {Constants.AzurePreferredAccountUsernameEnvName} is set accounts will be filtered by username: {username}");
                return accounts.Where(i => i.Username == username);
            }

            return accounts;
        }

        private void Log(Microsoft.Extensions.Logging.LogLevel level, string message, [CallerMemberName] string memberName = "")
        {
            _logger?.Log(level, $"{nameof(SharedTokenCacheProvider)}.{memberName} :: {message}");
        }
    }
}
