// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private const int LockfileRetryWait = 100;
        private const int LockfileRetryCount = 60000 / LockfileRetryWait;
        private FileStream _lockFileStream;

        public CrossPlatLock(string lockfilePath)
        {
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
                    fileStream = new FileStream(lockfilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, defaultBufferSize, FileOptions.DeleteOnClose);
                    using (var writer = new StreamWriter(fileStream, Encoding.UTF8, defaultBufferSize, leaveOpen: true))
                    {
                        writer.WriteLine($"{Process.GetCurrentProcess().Id} {Process.GetCurrentProcess().ProcessName}");
                    }
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

        public void Dispose()
        {
            _lockFileStream?.Dispose();
            _lockFileStream = null;
        }
    }
}
