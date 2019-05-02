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
using Microsoft.Identity.Client.Extensions.Abstractions;
using Microsoft.Identity.Client.Extensions.Msal.Providers;
using Microsoft.Rest;
using ITokenProvider = Microsoft.Identity.Client.Extensions.Abstractions.ITokenProvider;

namespace WebAppTestWithAzureSDK.Credentials
{
    public class DefaultChainServiceCredentials : ServiceClientCredentials
    {
        private readonly ITokenProvider _tokenProvider;
        private readonly IDictionary<string, IToken> _tokenCache = new Dictionary<string, IToken>();
        private readonly object _tokenCacheLock = new object();

        public DefaultChainServiceCredentials(IConfiguration config = null, ILogger<DefaultChainServiceCredentials> logger = null)
        {
            _tokenProvider = new DefaultTokenProviderChain(config: config, logger: logger);
        }

        public override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // TODO: probably should be asking the ServiceClient what the resourceUri is...
            const string resourceUri = "https://management.azure.com";
            var token = GetToken(resourceUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            return base.ProcessHttpRequestAsync(request, cancellationToken);
        }

        private IToken GetToken(string resourceUri)
        {
            lock (_tokenCacheLock)
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

                var newToken = _tokenProvider.GetTokenWithResourceUriAsync(resourceUri)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                _tokenCache[resourceUri] = newToken;
                return _tokenCache[resourceUri];
            }
        }
    }
}
