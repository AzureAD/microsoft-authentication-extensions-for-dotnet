// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Identity.Client.Extensions.Msal.Providers
{
    /// <inheritdoc />
    /// <summary>
    /// TokenProviderChain creates an ordered list of probes that will determine if the environment will allow it to build
    /// a ICredentialProvider. The ICredentialProvider is then used to generate authentication credentials for the
    /// consumer.
    /// </summary>
    public class TokenProviderChain : ITokenProvider
    {
        private readonly IList<ITokenProvider> _providers;

        /// <summary>
        /// Create an instance of a TokenProviderChain providing a list of IProbes which will be executed in order to create a ICredentialProvider
        /// </summary>
        /// <param name="providers">providers to be executed in order to create a ICredentialProvider</param>
        /// <exception cref="ArgumentException">throws if no probes were provided or if no probe is able to build a ICredentialProvider</exception>
        public TokenProviderChain(IList<ITokenProvider> providers)
        {
            if (providers == null || !providers.Any())
            {
                throw new ArgumentException("must provide 1 or more IProbes");
            }

            _providers = providers;
        }

        /// <inheritdoc />
        public async Task<bool> AvailableAsync()
        {
            foreach (var p in _providers)
            {
                if (await p.AvailableAsync().ConfigureAwait(false))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public async Task<IToken> GetTokenAsync(IEnumerable<string> scopes)
        {
            ITokenProvider provider = null;
            foreach (var p in _providers)
            {
                if (!await p.AvailableAsync().ConfigureAwait(false))
                {
                    continue;
                }

                provider = p;
                break;
            }

            if (provider == null)
            {
                throw new NoProvidersAvailableException();
            }

            return await provider.GetTokenAsync(scopes).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    /// <summary>
    /// NoProbesAvailableException is thrown when the chain of providers doesn't contain any token providers able to fetch a token
    /// </summary>
    public class NoProvidersAvailableException : MsalClientException
    {
        private const string Code = "no_providers_are_available";

        private const string ErrorMessage =
            "All of the ITokenProviders were unable to find the variables needed to successfully create a credential provider.";

        /// <inheritdoc />
        /// <summary>
        /// Create a NoProbesAvailableException
        /// </summary>
        public NoProvidersAvailableException() : base(Code, ErrorMessage)
        {
        }

        /// <summary>
        /// Create a NoProbesAvailableException with an error message
        /// </summary>
        public NoProvidersAvailableException(string errorMessage) : base(Code, errorMessage)
        {
        }
    }
}
