// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Client.Extensions.Adal.UnitTests
{
    [TestClass]
    public class AdalCacheStorageTests
    {
        public static readonly string CacheFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        private readonly TraceSource _logger = new TraceSource("TestSource");
        private static StorageCreationProperties s_storageCreationProperties;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            var builder = new StorageCreationPropertiesBuilder(Path.GetFileName(CacheFilePath), Path.GetDirectoryName(CacheFilePath));
            builder = builder.WithMacKeyChain(serviceName: "Microsoft.Developer.IdentityService", accountName: "ADALCache");
            builder = builder.WithLinuxKeyring(
                schemaName: "IdentityServiceAdalCache.cache",
                collection: "default",
                secretLabel: "ADALCache",
                attribute1: new KeyValuePair<string, string>("AdalClientID", "Microsoft.Developer.IdentityService"),
                attribute2: new KeyValuePair<string, string>("AdalClientVersion", "1.0.0.0"));
            s_storageCreationProperties = builder.Build();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            CleanTestData();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            CleanTestData();
        }

        [TestMethod]
        public void AdalTestUserDirectory()
        {
            Assert.AreEqual(AdalCacheStorage.UserRootDirectory,
                Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                    : Environment.GetEnvironmentVariable("HOME"));
        }

        [TestMethod]
        public void AdalNewStoreNoFile()
        {
            var store = new AdalCacheStorage(s_storageCreationProperties, logger: _logger);
            Assert.IsFalse(store.ReadData().Any());
        }

        [TestMethod]
        public void AdalWriteEmptyData()
        {
            var store = new AdalCacheStorage(s_storageCreationProperties, logger: _logger);
            Assert.ThrowsException<ArgumentNullException>(() => store.WriteData(null));

            store.WriteData(new byte[0]);

            var pass = store.ReadData();
            Assert.IsFalse(pass.Any());
        }

        [DoNotRunOnLinux]
        public void AdalWriteGoodData()
        {
            var store = new AdalCacheStorage(s_storageCreationProperties, logger: _logger);
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

        [DoNotRunOnLinux]
        public void AdalTestClear()
        {
            var store = new AdalCacheStorage(s_storageCreationProperties, logger: _logger);
            var store2 = new AdalCacheStorage(s_storageCreationProperties, logger: _logger);
            Assert.IsNotNull(Exception<ArgumentNullException>(() => store.WriteData(null)));

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
            if (File.Exists(CacheFilePath))
            {
                File.Delete(CacheFilePath);
            }
        }
    }
}
