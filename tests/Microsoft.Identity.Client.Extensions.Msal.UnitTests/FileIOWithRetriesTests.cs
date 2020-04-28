// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Client.Extensions.Msal.UnitTests
{
    [TestClass]
    public class FileIOWithRetriesTests
    {
        private TraceSource _logger;
        private TraceStringListener _testListener;

        [TestInitialize]
        public void TestInitialize()
        {
            _logger = new TraceSource("TestSource", SourceLevels.All);
            _testListener = new TraceStringListener();

            _logger.Listeners.Add(_testListener);
        }

        [TestMethod]
        public async Task Touch_FiresEvents_Async()
        {
            // a directory and a path that do not exist
            string dir = Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            string fileName = "testFile";
            string path = Path.Combine(dir, fileName);

            FileSystemWatcher watcher = new FileSystemWatcher(dir, fileName);
            watcher.EnableRaisingEvents = true;
            var semaphore = new SemaphoreSlim(0);
            int cacheChangedEventFired = 0;


            // expect this event to be fired twice
            watcher.Changed += (sender, args) =>
            {
                _logger.TraceInformation("Event fired!");
                cacheChangedEventFired++;
                semaphore.Release();
            };

            Assert.IsFalse(File.Exists(path));
            try
            {
                FileIOWithRetries.TouchFile(path, new TraceSourceLogger(_logger));
                DateTime initialLastWriteTime = File.GetLastWriteTimeUtc(path);
                Assert.IsTrue(File.Exists(path));

                FileIOWithRetries.TouchFile(path, new TraceSourceLogger(_logger));
                Assert.IsTrue(File.Exists(path));

                DateTime subsequentLastWriteTime = File.GetLastWriteTimeUtc(path);
                Assert.IsTrue(subsequentLastWriteTime > initialLastWriteTime);

                await semaphore.WaitAsync(5000).ConfigureAwait(false); // if event isn't fired in 5s, bail out
                await semaphore.WaitAsync(5000).ConfigureAwait(false); // if event isn't fired in 5s, bail out
                Assert.AreEqual(2, cacheChangedEventFired);
            }
            finally
            {
                _logger.TraceInformation("Cleaning up");
                Trace.WriteLine(_testListener.CurrentLog);
                File.Delete(path);
            }
        }
    }
}
