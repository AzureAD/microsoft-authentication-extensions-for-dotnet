// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.Identity.Client.Extensions.Web.TokenCacheProviders.Sql
{
    /// <summary>
    /// Represents an app's token cache entry in database
    /// </summary>
    public class AppTokenCache
    {
        /// <summary>
        /// 
        /// </summary>
        [Key]
        public int AppTokenCacheId { get; set; }

        /// <summary>
        /// The Appid or ClientId of the app
        /// </summary>
        public string ClientID { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public byte[] CacheBits { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public DateTime LastWrite { get; set; }

        /// <summary>
        /// Provided here as a precaution against concurrent updates by multiple threads.
        /// </summary>
        [Timestamp]
        public byte[] RowVersion { get; set; }
    }
}
