// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace Microsoft.Identity.Client.Extensions.Msal.Providers
{
    /// <summary>
    /// DefaultTokenProviderChain will attempt to build and AAD token in the following order
    ///     1) Service Principal with certificate or secret <see cref="ServicePrincipalTokenProvider"/>
    ///     2) Managed Identity for AppService or Virtual Machines <see cref="ManagedIdentityTokenProvider"/>
    ///     3) Shared Token Cache for your local developer environment <see cref="SharedTokenCacheProvider"/>
    /// </summary>
    public class DefaultTokenProviderChain : ITokenProvider
    {
        private readonly ITokenProvider _chain;
        private readonly TraceSource _logger;

        /// <inheritdoc />
        public DefaultTokenProviderChain(IConfigurationProvider config = null, TraceSource logger = null)
        {
            _logger = logger ?? new TraceSource(nameof(DefaultTokenProviderChain));
            config = config ?? new EnvironmentVariablesConfigurationProvider();
            config.Load();
            var providers = new List<ITokenProvider>
            {
                new ServicePrincipalTokenProvider(config: config, logger: logger),
                new ManagedIdentityTokenProvider(config: config, logger: logger),
                new SharedTokenCacheProvider(config: config, logger: logger)
            };
            _chain = new TokenProviderChain(providers);
        }


        /// <inheritdoc />
        public async Task<bool> AvailableAsync()
        {
            TraceEvent(TraceEventType.Information, "checking if any provider is available");
            var available = await _chain.AvailableAsync().ConfigureAwait(false);
            TraceEvent(TraceEventType.Information, $"provider available: {available}");
            return available;
        }

        /// <inheritdoc />
        public async Task<IToken> GetTokenAsync(IEnumerable<string> scopes)
        {
            TraceEvent(TraceEventType.Information, "getting token");
            var token = await _chain.GetTokenAsync(scopes).ConfigureAwait(false);
            TraceEvent(TraceEventType.Information, token != null ?
                $"token was returned and will expire on {token.ExpiresOn}" :
                "no token was returned");
            return token;
        }

        private void TraceEvent(TraceEventType type, string message, [CallerMemberName] string memberName = "")
        {
            _logger?.TraceEvent(type, 0, $"{nameof(DefaultTokenProviderChain)}.{memberName} :: {message}");
        }
    }
}
