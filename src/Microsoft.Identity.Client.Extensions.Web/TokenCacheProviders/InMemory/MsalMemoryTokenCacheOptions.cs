// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Identity.Client.Extensions.Web.TokenCacheProviders.InMemory
{
    /// <summary>
    /// MSAL's memory token cache options
    /// </summary>
    public class MsalMemoryTokenCacheOptions
    {
        /// <summary>
        /// 
        /// </summary>
        public MsalMemoryTokenCacheOptions()
        {
            AbsoluteExpiration = DateTimeOffset.Now.AddHours(12);
        }

        /// <summary>
        /// Gets or sets the value of The fixed date and time at which the cache entry will expire..
        /// The duration till the tokens are kept in memory cache. In production, a higher value , upto 90 days is recommended.
        /// </summary>
        /// <value>
        /// The AbsoluteExpiration value.
        /// </value>
        public DateTimeOffset AbsoluteExpiration { get; set; }
    }
}
