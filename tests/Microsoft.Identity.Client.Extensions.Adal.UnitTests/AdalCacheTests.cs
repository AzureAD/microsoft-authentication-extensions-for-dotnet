// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Client.Extensions.Adal.UnitTests
{
    [TestClass]
    public class AdalCacheTests
    {
        public static readonly string CacheFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        private readonly TraceSource _logger = new TraceSource("TestSource");
        private static StorageCreationProperties s_storageCreationProperties;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            var builder = new StorageCreationPropertiesBuilder(Path.GetFileName(CacheFilePath), Path.GetDirectoryName(CacheFilePath), "ClientIDGoesHere");
            builder = builder.WithMacKeyChain(serviceName: "Microsoft.Developer.IdentityService", accountName: "MSALCache");
            builder = builder.WithLinuxKeyring(
                schemaName: "adal.cache",
                collection: "default",
                secretLabel: "ADALCache",
                attribute1: new KeyValuePair<string, string>("ADALClientID", "Microsoft.Developer.IdentityService"),
                attribute2: new KeyValuePair<string, string>("AdalClientVersion", "1.0.0.0"));
            s_storageCreationProperties = builder.Build();
        }

        [TestMethod]
        public async Task ThreeRegisteredCachesRemainInSyncTestAsync()
        {
            if (File.Exists(s_storageCreationProperties.CacheFilePath))
            {
                File.Delete(s_storageCreationProperties.CacheFilePath);
            }

            string startString = "Something to start with";
            var startBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(startString), optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(s_storageCreationProperties.CacheFilePath, startBytes).ConfigureAwait(true);

            var storage = new AdalCacheStorage(s_storageCreationProperties, _logger);
            var cache1 = new AdalCache(storage, _logger);
            var cache2 = new AdalCache(storage, _logger);
            var cache3 = new AdalCache(storage, _logger);

            var storeVersion0 = storage.LastVersionToken;

            var args1 = new TokenCacheNotificationArgs();
            var args2 = new TokenCacheNotificationArgs();
            var args3 = new TokenCacheNotificationArgs();

            cache1.BeforeAccessNotification(args1);
            cache1.HasStateChanged = true;
            cache1.AfterAccessNotification(args1);

            var storeVersion1 = storage.LastVersionToken;

            Assert.AreNotEqual(storeVersion0, storeVersion1);

            cache2.BeforeAccessNotification(args2);
            cache2.AfterAccessNotification(args2);

            cache3.BeforeAccessNotification(args3);
            cache3.AfterAccessNotification(args3);

            var storeVersion2 = storage.LastVersionToken;
            Assert.AreEqual(storeVersion1, storeVersion2);

            File.Delete(s_storageCreationProperties.CacheFilePath);
            File.Delete(s_storageCreationProperties.CacheFilePath + ".version");
        }
    }
}
