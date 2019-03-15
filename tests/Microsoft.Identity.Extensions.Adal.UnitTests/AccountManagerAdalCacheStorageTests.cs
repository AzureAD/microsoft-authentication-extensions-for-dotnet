// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Identity.Extensions.Adal.UnitTests
{
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented", Justification = "Unit Testing")]
    public class AccountManagerAdalCacheStorageTests : IDisposable
    {
        public static readonly string CacheFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        private readonly TraceSource logger = new TraceSource("TestSource");

        public AccountManagerAdalCacheStorageTests()
        {
            this.CleanTestData();
        }

        public void Dispose()
        {
            this.CleanTestData();
        }

        [Fact]
        public void NewStoreNoFile()
        {
            var store = new IdentityServiceAdalCacheStorage(CacheFilePath, instanceName: null, logger: this.logger);
            Assert.False(store.HasChanged);
            Assert.Empty(store.ReadData());
        }

        [Fact]
        public void WriteEmptyData()
        {
            var store = new IdentityServiceAdalCacheStorage(CacheFilePath, instanceName: null, logger: this.logger);
            Assert.Throws<ArgumentNullException>(() => store.WriteData(null));

            store.WriteData(new byte[0]);

            Assert.Empty(store.ReadData());
        }

        [Fact]
        public void WriteGoodData()
        {
            var store = new IdentityServiceAdalCacheStorage(CacheFilePath, instanceName: null, logger: this.logger);
            Assert.Throws<ArgumentNullException>(() => store.WriteData(null));

            byte[] data = { 2, 2, 3 };
            byte[] data2 = { 2, 2, 3, 4, 4 };
            store.WriteData(data);
            Assert.False(store.HasChanged);
            Enumerable.SequenceEqual(store.ReadData(), data);

            store.WriteData(data);
            Assert.False(store.HasChanged);
            store.WriteData(data2);
            Assert.False(store.HasChanged);
            store.WriteData(data);
            Assert.False(store.HasChanged);
            store.WriteData(data2);
            Assert.False(store.HasChanged);
            Enumerable.SequenceEqual(store.ReadData(), data2);
        }

        [Fact]
        public void TestClear()
        {
            var store = new IdentityServiceAdalCacheStorage(CacheFilePath, instanceName: null, logger: this.logger);
            var store2 = new IdentityServiceAdalCacheStorage(CacheFilePath, instanceName: null, logger: this.logger);
            Assert.NotNull(Exception<ArgumentNullException>(() => store.WriteData(null)));

            byte[] data = { 2, 2, 3 };
            store.WriteData(data);

            Assert.False(store.HasChanged);
            Assert.True(store2.HasChanged);

            store2.ReadData();

            Enumerable.SequenceEqual(store.ReadData(), data);
            Assert.True(File.Exists(CacheFilePath));

            store.Clear();
            Assert.False(store.HasChanged);
            Assert.True(store2.HasChanged);

            Assert.Empty(store.ReadData());
            Assert.Empty(store2.ReadData());
            Assert.False(File.Exists(CacheFilePath));
        }

        [Fact]
        public void TestRootSuffixWithNullSuffixCacheAndRegistry()
        {
            var store = new IdentityServiceAdalCacheStorage(CacheFilePath, instanceName: null, logger: this.logger);
            Assert.Equal(store.CacheFilePath, CacheFilePath, ignoreCase: true);
            string localDataPath = SharedUtilities.GetDefaultArtifactPath();

            store = new IdentityServiceAdalCacheStorage(null, instanceName: string.Empty, logger: this.logger);

            string cacheFilePath = Path.Combine(localDataPath, @"msal.cache");
            Assert.Equal(store.CacheFilePath, cacheFilePath, ignoreCase: true);

            store = new IdentityServiceAdalCacheStorage(null, instanceName: null, logger: this.logger);
            Assert.Equal(store.CacheFilePath, cacheFilePath, ignoreCase: true);
        }

        [Fact]
        public void TestRootSuffixWithGoodSuffixCacheAndRegistry()
        {
            var store = new IdentityServiceAdalCacheStorage(CacheFilePath, instanceName: "Exp", logger: this.logger);
            Assert.Equal(store.CacheFilePath, CacheFilePath, ignoreCase: true);
            string localDataPath = SharedUtilities.GetDefaultArtifactPath();

            store = new IdentityServiceAdalCacheStorage(null, instanceName: "Exp", logger: this.logger);
            string cacheFilePath = Path.Combine(localDataPath, "Exp", @"msal.cache");

            Assert.Equal(cacheFilePath, store.CacheFilePath);
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
