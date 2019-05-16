// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client.Extensions.Msal.Providers;
using Microsoft.Rest;
using ITokenProvider = Microsoft.Identity.Client.Extensions.Msal.Providers.ITokenProvider;

namespace WebAppTestWithAzureSDK.Credentials
{
    public class DefaultChainServiceCredentials : ServiceClientCredentials
    {
        private readonly ITokenProvider _tokenProvider;
        private readonly IDictionary<string, IToken> _tokenCache = new Dictionary<string, IToken>();
        private SemaphoreSlim Semaphore { get; }

        public DefaultChainServiceCredentials(IConfiguration config = null, ILogger<DefaultChainServiceCredentials> logger = null)
        {
            _tokenProvider = new DefaultTokenProviderChain(config: config, logger: logger);
            Semaphore = new SemaphoreSlim(1, 1);
        }

        public override async Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // TODO: probably should be asking the ServiceClient what the resourceUri is...
            const string resourceUri = "https://management.azure.com";
            var token = await GetTokenAsync(resourceUri).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            await base.ProcessHttpRequestAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private async Task<IToken> GetTokenAsync(string resourceUri)
        {
            Semaphore.Wait();
            try
            {
                if (_tokenCache.ContainsKey(resourceUri))
                {
                    var token = _tokenCache[resourceUri];
                    if (!token.ExpiresOn.HasValue ||
                        !(token.ExpiresOn < DateTime.Now.Subtract(new TimeSpan(0, 5, 0))))
                    {
                        return _tokenCache[resourceUri];
                    }
                }

                var newToken = await _tokenProvider.GetTokenWithResourceUriAsync(resourceUri)
                    .ConfigureAwait(false);
                _tokenCache[resourceUri] = newToken;
                return _tokenCache[resourceUri];
            }
            finally
            {
                Semaphore.Release();
            }
        }
    }
}
