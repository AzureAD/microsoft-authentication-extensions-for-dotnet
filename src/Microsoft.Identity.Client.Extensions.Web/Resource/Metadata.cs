// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.Identity.Client.Extensions.Web.Resource
{
    /// <summary>
    /// Model child class to hold alias information parsed from the Azure AD issuer endpoint.
    /// </summary>
    public class Metadata
    {
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(PropertyName = "preferred_network")]
        public string PreferredNetwork { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(PropertyName = "preferred_cache")]
        public string PreferredCache { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(PropertyName = "aliases")]
        public List<string> Aliases { get; set; }
    }
}
