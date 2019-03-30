// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;

namespace Microsoft.Identity.Client.Extensions.Msal.Providers
{
    internal static class ConfigurationProviderExtension
    {
        public static string Get(this IConfigurationProvider config, string key)
        {
            return config.TryGet(key, out string val) ? val : null;
        }
    }
}
