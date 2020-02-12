// Copyright (c) Microsoft Corporation. All rights reserved.
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
    [TestClass]
    public class MsalCacheStorageTests
    {
        public static readonly string CacheFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        private readonly TraceSource _logger = new TraceSource("TestSource", SourceLevels.All);
        private static StorageCreationProperties s_storageCreationProperties;

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
        public void CacheStorageReadCanHandleReadingNull()
        {
            // Arrange
            var cacheAccessor = NSubstitute.Substitute.For<ICacheAccessor>();
            cacheAccessor.Read().Returns((byte[])null);

            var actualLogger = new TraceSourceLogger(_logger);
            var storage = new MsalCacheStorage(s_storageCreationProperties, cacheAccessor, actualLogger);

            // Act
            byte[] result = storage.ReadData();

            // Assert
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void CacheStorageReadCanHandleExceptionsWhenReading()
        {
            
            // Arrange
            var cacheAccessor = NSubstitute.Substitute.For<ICacheAccessor>();
            var exception = new InvalidOperationException();
            cacheAccessor.Read().Throws(exception);

            var actualLogger = new TraceSourceLogger(_logger);
            var storage = new MsalCacheStorage(s_storageCreationProperties, cacheAccessor, actualLogger);

            // Act
            byte[] result = storage.ReadData();

            // Assert
            Assert.AreEqual(0, result.Length);
        }

        // Regression https://github.com/AzureAD/microsoft-authentication-extensions-for-dotnet/issues/56
        [TestMethod]
        public void CacheStorageCanHandleMultipleExceptionsWhenReading()
        {
            // Arrange
            var stringListener = new TraceStringListener();
            var cacheAccessor = Substitute.For<ICacheAccessor>();
            var exception = new InvalidOperationException("some error");
            cacheAccessor.Read().Throws(exception);
            cacheAccessor.When((x) => x.Clear()).Do(x => throw exception);
            _logger.Listeners.Add(stringListener);
            var actualLogger = new TraceSourceLogger(_logger);
            var storage = new MsalCacheStorage(s_storageCreationProperties, cacheAccessor, actualLogger);

            // Act
            byte[] result = storage.ReadData();

            // Assert
            Assert.AreEqual(0, result.Length);
            Assert.IsTrue(stringListener.CurrentLog.Contains("TestSource Error"));
            Assert.IsTrue(stringListener.CurrentLog.Contains("InvalidOperationException"));
            Assert.IsTrue(stringListener.CurrentLog.Contains("some error"));
        }

        [TestMethod]
        public void VerifyPersistenceThrowsInnerExceptions()
        {
            // Arrange
            var stringListener = new TraceStringListener();
            var actualLogger = new TraceSourceLogger(_logger);
            var cacheAccessor = Substitute.For<ICacheAccessor>();
            var exception = new InvalidOperationException("some error");
            var storage = new MsalCacheStorage(s_storageCreationProperties, cacheAccessor, actualLogger);

            cacheAccessor.Read().Throws(exception);

            // Act
            var ex = AssertException.Throws<MsalCachePersistenceException>(
                () => storage.VerifyPersistence());

            // Assert
            Assert.AreEqual(ex.InnerException, exception);
        }


        [TestMethod]
        public void VerifyPersistenceThrowsIfDataReadIsEmpty()
        {
            // Arrange
            var stringListener = new TraceStringListener();
            var actualLogger = new TraceSourceLogger(_logger);
            var cacheAccessor = Substitute.For<ICacheAccessor>();
            var storage = new MsalCacheStorage(s_storageCreationProperties, cacheAccessor, actualLogger);


            // Act
            var ex = AssertException.Throws<MsalCachePersistenceException>(
                () => storage.VerifyPersistence());

            // Assert
            Assert.IsNull(ex.InnerException); // no more details available
        }

        [TestMethod]
        public void VerifyPersistenceThrowsIfDataReadIsDiffrentFromDataWritten()
        {
            // Arrange
            var stringListener = new TraceStringListener();
            var actualLogger = new TraceSourceLogger(_logger);
            var cacheAccessor = Substitute.For<ICacheAccessor>();
            var storage = new MsalCacheStorage(s_storageCreationProperties, cacheAccessor, actualLogger);
            cacheAccessor.Read().Returns(Encoding.UTF8.GetBytes("other_dummy_data"));

            // Act
            var ex = AssertException.Throws<MsalCachePersistenceException>(
                () => storage.VerifyPersistence());

            // Assert
            Assert.IsNull(ex.InnerException); // no more details available
        }


        [TestMethod]
        public void VerifyPersistenceHappyPath()
        {
            // Arrange
            byte[] dummyData = Encoding.UTF8.GetBytes(MsalCacheStorage.PersistenceValidationDummyData);
            var stringListener = new TraceStringListener();
            var actualLogger = new TraceSourceLogger(_logger);
            var cacheAccessor = Substitute.For<ICacheAccessor>();
            var storage = new MsalCacheStorage(s_storageCreationProperties, cacheAccessor, actualLogger);
            cacheAccessor.Read().Returns(dummyData);

            // Act
            storage.VerifyPersistence();

            // Assert
            Received.InOrder(() => {
                cacheAccessor.Write(Arg.Any<byte[]>());
                cacheAccessor.Read();
                cacheAccessor.Clear();
            });
        }

        [TestMethod]
        public void MsalTestUserDirectory()
        {
            Assert.AreEqual(MsalCacheHelper.UserRootDirectory,
                Environment.OSVersion.Platform == PlatformID.Win32NT
                    ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                    : Environment.GetEnvironmentVariable("HOME"));
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
