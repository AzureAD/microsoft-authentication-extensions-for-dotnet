// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Identity.Client.Extensions.Msal.Providers
{
    /// <summary>
    /// ITokenProvider describes the interface for fetching an access token
    /// </summary>
    public interface ITokenProvider
    {
        /// <summary>
        /// Check if the provider is available for use in the current environment
        /// </summary>
        /// <returns>True if a credential provider can be built</returns>
        Task<bool> AvailableAsync();

        /// <summary>
        /// GetTokenAsync will attempt to fetch a token for a given set of scopes
        /// </summary>
        /// <param name="scopes">Scopes requested to access a protected API</param>
        /// <returns>An access token as a string</returns>
       Task<IToken> GetTokenAsync(IEnumerable<string> scopes);
    }

    /// <summary>
    /// IToken is an AAD access token with an expiration
    /// </summary>
    public interface IToken
    {
        /// <summary>
        /// ExpiresOn provides an expiry for the access token. If null, there is no expiration.
        /// </summary>
        DateTimeOffset? ExpiresOn { get; }

        /// <summary>
        /// AccessToken is a string representation of an AAD access token
        /// </summary>
        string AccessToken { get; }
    }

    internal class AccessTokenWithExpiration : IToken
    {
        public DateTimeOffset? ExpiresOn { get; set; }

        public string AccessToken { get; set; }
    }
}
