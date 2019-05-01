using System;

namespace Microsoft.Identity.Client.Extensions.Abstractions
{
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
}
