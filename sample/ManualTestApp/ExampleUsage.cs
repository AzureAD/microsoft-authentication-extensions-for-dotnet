using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
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
        private const string TraceSourceName = "MSAL.Contoso.CacheExtension";

        /// <summary>
        /// Start reading here...
        /// </summary>
        /// <returns></returns>
        public static async Task Example_Async()
        {
            // 1. Use MSAL to create an instance of the Public Client Application
            var app = PublicClientApplicationBuilder.Create(Config.ClientId).Build();

            // 2. Configure the storage
            var cacheHelper = await CreateCacheHelperAsync().ConfigureAwait(false);

            // 3. Let the cache helper handle MSAL's cache
            cacheHelper.RegisterCache(app.UserTokenCache);
        }

        private static async Task<MsalCacheHelper> CreateCacheHelperAsync()
        {
            StorageCreationProperties storageProperties;
            MsalCacheHelper cacheHelper;
            try
            {
                storageProperties = ConfigureSecureStorage(usePlaintextFileOnLinux: false);
                cacheHelper = await MsalCacheHelper.CreateAsync(
                            storageProperties,
                            new TraceSource(TraceSourceName))
                         .ConfigureAwait(false);

                // the underlying persistence mechanism might not be usable
                // this typically happens on Linux over SSH
                cacheHelper.VerifyPersistence();

                return cacheHelper;
            }
            catch (MsalCachePersistenceException ex)
            {
                Console.WriteLine("Cannot persist data securely. ");
                Console.WriteLine("Details: " + ex);


                if (SharedUtilities.IsLinuxPlatform())
                {
                    storageProperties = ConfigureSecureStorage(usePlaintextFileOnLinux: true);

                    Console.WriteLine($"Falling back on using a plaintext " +
                        $"file located at {storageProperties.CacheFilePath} Users are responsible for securing this file!");

                    cacheHelper = await MsalCacheHelper.CreateAsync(
                           storageProperties,
                           new TraceSource(TraceSourceName))
                        .ConfigureAwait(false);

                    return cacheHelper;
                }
                throw;
            }
        }

        private static StorageCreationProperties ConfigureSecureStorage(bool usePlaintextFileOnLinux)
        {
            if (!usePlaintextFileOnLinux)
            {
                return new StorageCreationPropertiesBuilder(
                                   Config.CacheFileName,
                                   Config.CacheDir)
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
            }

            return new StorageCreationPropertiesBuilder(
                                     Config.CacheFileName,
                                     Config.CacheDir)
                                 .WithLinuxUnprotectedFile()
                                 .WithMacKeyChain(
                                     Config.KeyChainServiceName,
                                     Config.KeyChainAccountName)
                                 .Build();

        }
    }
}

