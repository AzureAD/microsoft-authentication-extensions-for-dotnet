// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.Identity.Client.Extensions.Web.Resource
{
    /// <summary>
    /// Model class to hold information parsed from the Azure AD issuer endpoint
    /// </summary>
    public class IssuerMetadata
    {
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(PropertyName = "tenant_discovery_endpoint")]
        public string TenantDiscoveryEndpoint { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(PropertyName = "api-version")]
        public string ApiVersion { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(PropertyName = "metadata")]
        public List<Metadata> Metadata { get; set; }
    }
}
