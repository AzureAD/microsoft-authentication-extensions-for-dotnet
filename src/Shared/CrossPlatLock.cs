// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if ADAL
namespace Microsoft.Identity.Client.Extensions.Adal
#elif MSAL
namespace Microsoft.Identity.Client.Extensions.Msal
#else // WEB
namespace Microsoft.Identity.Client.Extensions.Web
#endif
{
    internal class CrossPlatLock : IDisposable
    {
        private const bool UseMutex = false;
        private const int MutexTimeout = 60000;
        private const int LockfileRetryCount = 6000;
        private const int LockfileRetryWait = 10;
        private Mutex _mutex;
        private FileStream _lockFileStream;
        private string _lockFilePath;

        public CrossPlatLock(string simpleName, string lockfilePath)
        {
#pragma warning disable CS0162 // Unreachable code detected
            if (UseMutex)
            {
                InitializeMutex(simpleName);
            }
            else
            {
                InitializeLockfileAsync(lockfilePath);
            }
#pragma warning restore CS0162 // Unreachable code detected
        }

        private void InitializeLockfileAsync(string lockfilePath)
        {
            _lockFilePath = lockfilePath;
            Exception exception = null;
            FileStream fileStream = null;
            for (int tryCount = 0; tryCount < LockfileRetryCount; tryCount++)
            {
                // Create lock file dir if it doesn't already exist
                Directory.CreateDirectory(Path.GetDirectoryName(lockfilePath));
                try
                {
                    // We are using the file locking to synchronize the store, do not allow multiple writers or readers for the file.
                    const int defaultBufferSize = 4096;
                    fileStream = new FileStream(lockfilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, defaultBufferSize, FileOptions.DeleteOnClose);
                    break;
                }
                catch (IOException ex)
                {
                    exception = ex;
                    Thread.Sleep(LockfileRetryWait);
                }
            }

            _lockFileStream = fileStream ?? throw new InvalidOperationException("Could not get access to the shared lock file.", exception);
        }

        private void InitializeMutex(string simpleName)
        {
            var mutex = new Mutex(initiallyOwned: false, name: $@"Global\{simpleName}");
            bool mutexAcquired;

            try
            {
                mutexAcquired = mutex.WaitOne(MutexTimeout);
            }
            catch (AbandonedMutexException)
            {
                // Abandoned mutexes are still acquired. Just treat as acquisition.
                mutexAcquired = true;
            }

            // Handle timeout
            if (!mutexAcquired)
            {
                throw new TimeoutException($"Unable to acquire mutex in {MutexTimeout} milliseconds");
            }

            _mutex = mutex;
        }

        public void Dispose()
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            _mutex = null;

            _lockFileStream?.Dispose();
            _lockFileStream = null;
        }
    }
}
