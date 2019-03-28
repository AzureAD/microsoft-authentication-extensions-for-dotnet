// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Extensions.Msal.UnitTests
{
    [TestClass]
    public class MsalCacheHelperTests
    {
        public static readonly string CacheFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        private readonly TraceSource _logger = new TraceSource("TestSource");
        private static StorageCreationProperties s_storageCreationProperties;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            var builder = new StorageCreationPropertiesBuilder(Path.GetFileName(CacheFilePath), Path.GetDirectoryName(CacheFilePath));
            builder = builder.WithMacKeyChain(serviceName: "Microsoft.Developer.IdentityService", accountName: "MSALCache");
            builder = builder.WithLinuxKeyring(
                schemaName: "msal.cache",
                collection: "default",
                secretLabel: "MSALCache",
                attribute1: new KeyValuePair<string, string>("MsalClientID", "Microsoft.Developer.IdentityService"),
                attribute2: new KeyValuePair<string, string>("MsalClientVersion", "1.0.0.0"));
            s_storageCreationProperties = builder.Build();
        }

        [TestMethod]
        public void MultiAccessSerializationAsync()
        {
            var helper1 = new MsalCacheHelper(
                new MockTokenCache(),
                new MsalCacheStorage(s_storageCreationProperties, _logger),
                _logger);

            var helper2 = new MsalCacheHelper(
                new MockTokenCache(),
                new MsalCacheStorage(s_storageCreationProperties, _logger),
                _logger);

            //Test signalling thread 1
            var resetEvent1 = new ManualResetEvent(initialState: false);

            //Test signalling thread 2
            var resetEvent2 = new ManualResetEvent(initialState: false);

            //Thread 1 signalling test
            var resetEvent3 = new ManualResetEvent(initialState: false);

            // Thread 2 signalling test
            var resetEvent4 = new ManualResetEvent(initialState: false);

            var thread1 = new Thread(() =>
            {
                var args = new TokenCacheNotificationArgs();
                helper1.BeforeAccessNotification(args);
                resetEvent3.Set();
                resetEvent1.WaitOne();
                helper1.AfterAccessNotification(args);
            });

            var thread2 = new Thread(() =>
            {
                var args = new TokenCacheNotificationArgs();
                helper2.BeforeAccessNotification(args);
                resetEvent4.Set();
                resetEvent2.WaitOne();
                helper2.AfterAccessNotification(args);
                resetEvent4.Set();
            });

            // Let thread 1 start and get the lock
            thread1.Start();
            resetEvent3.WaitOne();

            // Start thread 2 and give it enough time to get blocked on the lock
            thread2.Start();
            Thread.Sleep(5000);

            // Make sure helper1 has the lock still, and helper2 doesn't
            Assert.IsNotNull(helper1.CacheLock);
            Assert.IsNull(helper2.CacheLock);

            // Allow thread1 to give up the lock, and wait for helper2 to get it
            resetEvent1.Set();
            resetEvent4.WaitOne();
            resetEvent4.Reset();

            // Make sure helper1 gave it up properly, and helper2 now owns the lock
            Assert.IsNull(helper1.CacheLock);
            Assert.IsNotNull(helper2.CacheLock);

            // Allow thread2 to give up the lock, and wait for it to complete
            resetEvent2.Set();
            resetEvent4.WaitOne();

            // Make sure thread2 cleaned up after itself as well
            Assert.IsNull(helper2.CacheLock);
        }
    }
}
