// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace ManualTestApp
{
    internal static class Utilities
    {
        internal static async Task<(string[], IPublicClientApplication, MsalCacheHelper)> GetPublicClientAsync(
            string resource,
            string tenant,
            Uri baseAuthority,
            bool validateAuthority,
            string clientId,
            string cacheFilename,
            string cacheDirectory,
            string serviceName,
            string accountName)
        {
            // tenant can be null
            resource = resource ?? throw new ArgumentNullException(nameof(resource));

            Console.WriteLine($"Using resource: '{resource}', tenant:'{tenant}'");

            var scopes = new string[] { resource + "/.default" };

            Console.WriteLine($"Using scopes: '{string.Join(",", scopes)}'");

            var authority = $"{baseAuthority.AbsoluteUri}{tenant}";
            Console.WriteLine($"GetPublicClient for authority: '{authority}' ValidateAuthority: '{validateAuthority}'");

            Uri authorityUri = new Uri(authority);
            var appBuilder = PublicClientApplicationBuilder.Create(clientId).WithAuthority(authorityUri, validateAuthority)
                .WithRedirectUri("http://localhost");
            var app = appBuilder.Build();
            Console.WriteLine($"Built public client");

            var storageCreationPropsBuilder = new StorageCreationPropertiesBuilder(cacheFilename, cacheDirectory, clientId);
            storageCreationPropsBuilder = storageCreationPropsBuilder.WithMacKeyChain(serviceName, accountName);
            var storageCreationProps = storageCreationPropsBuilder.Build();

            // This hooks up our custom cache onto the one used by MSAL
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageCreationProps).ConfigureAwait(false);
            cacheHelper.RegisterCache(app.UserTokenCache);

            Console.WriteLine($"Cache registered");

            return (scopes, app, cacheHelper);
        }

        internal static async Task DeviceCodeCallbackAsync(DeviceCodeResult deviceCode)
        {
            deviceCode = deviceCode ?? throw new ArgumentNullException(nameof(deviceCode));

            await Task.Yield();

            Console.WriteLine( $"Got device code back, the message is '{deviceCode.Message}'");
            var deviceParameters = new List<object> { deviceCode }.AsReadOnly();
        }
    }
}
