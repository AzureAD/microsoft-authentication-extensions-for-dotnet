﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Microsoft.Identity.Client.Extensions.Msal.UnitTests
{
    /// <summary>
    /// These tests write data to disk / key chain / key ring etc. 
    /// </summary>
    [TestClass]
    public class MsalCacheStorageIntegrationTests
    {
        public static readonly string CacheFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        private readonly TraceSource _logger = new TraceSource("TestSource", SourceLevels.All);
        private static StorageCreationProperties s_storageCreationProperties;
        private static StorageCreationProperties s_storageCreationPropertiesWithLinuxFallback;

        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            var builder = new StorageCreationPropertiesBuilder(Path.GetFileName(CacheFilePath), Path.GetDirectoryName(CacheFilePath), "ClientIDGoesHere");
            builder = builder.WithMacKeyChain(serviceName: "Microsoft.Developer.IdentityService", accountName: "MSALCache");
            builder = builder.WithLinuxKeyring(
                schemaName: "msal.cache",
                collection: "default",
                secretLabel: "MSALCache",
                attribute1: new KeyValuePair<string, string>("MsalClientID", "Microsoft.Developer.IdentityService"),
                attribute2: new KeyValuePair<string, string>("MsalClientVersion", "1.0.0.0"));
            s_storageCreationProperties = builder.Build();

            s_storageCreationPropertiesWithLinuxFallback =
                new StorageCreationPropertiesBuilder(Path.GetFileName(CacheFilePath), Path.GetDirectoryName(CacheFilePath), "ClientIDGoesHere")
                            .WithMacKeyChain(serviceName: "Microsoft.Developer.IdentityService", accountName: "MSALCache")
                            .WithLinuxUnprotectedFile()
                            .Build();

        }

        [TestInitialize]
        public void TestiInitialize()
        {
            CleanTestData();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanTestData();
        }

        [TestMethod]
        public void MsalTestUserDirectory()
        {
            Assert.AreEqual(MsalCacheHelper.UserRootDirectory,
                Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                    : Environment.GetEnvironmentVariable("HOME"));
        }

        [RunOnOSX]
        public void CacheStorageFactoryMac()
        {
            MsalCacheStorage store = MsalCacheStorage.Create(s_storageCreationProperties, logger: _logger);
            Assert.IsTrue(store.CacheAccessor is MacKeyChainAccessor);
            store.VerifyPersistence();

            store = MsalCacheStorage.Create(s_storageCreationPropertiesWithLinuxFallback, logger: _logger);
            Assert.IsTrue(store.CacheAccessor is MacKeyChainAccessor);
        }

        [RunOnWindows]
        public void CacheStorageFactoryWindows()
        {
            MsalCacheStorage store = MsalCacheStorage.Create(s_storageCreationProperties, logger: _logger);
            Assert.IsTrue(store.CacheAccessor is DpApiEncryptedFileAccessor);
            store.VerifyPersistence();

            store = MsalCacheStorage.Create(s_storageCreationPropertiesWithLinuxFallback, logger: _logger);
            Assert.IsTrue(store.CacheAccessor is DpApiEncryptedFileAccessor);
        }

        [RunOnLinux]
        public void CacheStorageFactory_WithFallback_Linux()
        {
            MsalCacheStorage store = MsalCacheStorage.Create(s_storageCreationProperties, logger: _logger);
            Assert.IsTrue(store.CacheAccessor is LinuxKeyRingAccessor);

            // ADO Linux test agents do not have libsecret installed by default
            AssertException.Throws<MsalCachePersistenceException>(
                () => store.VerifyPersistence() );

            store = MsalCacheStorage.Create(s_storageCreationPropertiesWithLinuxFallback, _logger);
            Assert.IsTrue(store.CacheAccessor is FileAccessor);

            store.VerifyPersistence();
        }

        [TestMethod]
        public void MsalNewStoreNoFile()
        {
            var store = MsalCacheStorage.Create(s_storageCreationProperties, logger: _logger);
            Assert.IsFalse(store.ReadData().Any());
        }

        [TestMethod]
        public void MsalWriteEmptyData()
        {
            var store = MsalCacheStorage.Create(s_storageCreationProperties, logger: _logger);
            Assert.ThrowsException<ArgumentNullException>(() => store.WriteData(null));

            store.WriteData(new byte[0]);

            Assert.IsFalse(store.ReadData().Any());
        }

        [TestMethod]
        public void MsalWriteGoodData()
        {
            var store = MsalCacheStorage.Create(s_storageCreationProperties, logger: _logger);
            Assert.ThrowsException<ArgumentNullException>(() => store.WriteData(null));

            byte[] data = { 2, 2, 3 };
            byte[] data2 = { 2, 2, 3, 4, 4 };
            store.WriteData(data);
            Assert.IsTrue(Enumerable.SequenceEqual(store.ReadData(), data));

            store.WriteData(data);
            store.WriteData(data2);
            store.WriteData(data);
            store.WriteData(data2);
            Assert.IsTrue(Enumerable.SequenceEqual(store.ReadData(), data2));
        }

        [TestMethod]
        public void MsalTestClear()
        {
            var store = MsalCacheStorage.Create(s_storageCreationProperties, logger: _logger);
            var tempData = store.ReadData();

            var store2 = MsalCacheStorage.Create(s_storageCreationProperties, logger: _logger);
            AssertException.Throws<ArgumentNullException>(() => store.WriteData(null));

            byte[] data = { 2, 2, 3 };
            store.WriteData(data);
            store2.ReadData();

            Assert.IsTrue(Enumerable.SequenceEqual(store.ReadData(), data));
            Assert.IsTrue(File.Exists(CacheFilePath));

            store.Clear();

            Assert.IsFalse(store.ReadData().Any());
            Assert.IsFalse(store2.ReadData().Any());
            Assert.IsFalse(File.Exists(CacheFilePath));
        }

        /// <summary>
        /// Records an exception thrown when executing the provided action
        /// </summary>
        /// <typeparam name="TException">The type of exception to record</typeparam>
        /// <param name="action">The action to execute</param>
        /// <returns>The exception if thrown; otherwise, null</returns>
        private static TException Exception<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
                return null;
            }
            catch (TException ex)
            {
                return ex;
            }
        }

        private void CleanTestData()
        {
            var store = MsalCacheStorage.Create(s_storageCreationProperties, logger: _logger);
            store.Clear();
        }
    }
}
