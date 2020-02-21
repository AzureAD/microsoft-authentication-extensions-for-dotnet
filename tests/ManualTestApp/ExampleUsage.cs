using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace ManualTestApp
{
    /// <summary>
    /// This class shows how to applications are supposed to use the extension API
    /// </summary>
    public class ExampleUsage
    {
        public static async Task Example_Async()
        {
            // 1. Use MSAL to create an instance of the Public Client Application
            var app = PublicClientApplicationBuilder.Create(Config.ClientId).Build();

            // 2. Configure the storage
            var storageProperties =
                    new StorageCreationPropertiesBuilder(
                        Config.CacheFileName,
                        Config.CacheDir,
                        Config.ClientId)
                    .WithLinuxKeyring(
                        Config.LinuxKeyRingSchema,
                        Config.LinuxKeyRingCollection,
                        Config.LinuxKeyRingLabel,
                        Config.LinuxKeyRingAttr1,
                        Config.LinuxKeyRingAttr2)
                    .WithMacKeyChain(
                        Config.KeyChainServiceName,
                        Config.KeyChainAccountName)
                    .Build();

            // 3. Create the high level MsalCacheHelper based on properties and a logger
            var cacheHelper = await MsalCacheHelper.CreateAsync(
                    storageProperties,
                    new TraceSource("MSAL.CacheExtension"))
                .ConfigureAwait(false);

            // 4. Let the cache helper handle MSAL's cache
            cacheHelper.RegisterCache(app.UserTokenCache);
        }
       
    }
}
