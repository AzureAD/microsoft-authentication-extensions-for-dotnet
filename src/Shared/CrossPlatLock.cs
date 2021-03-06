﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

#if ADAL
namespace Microsoft.Identity.Client.Extensions.Adal
#elif MSAL
namespace Microsoft.Identity.Client.Extensions.Msal
#else // WEB
namespace Microsoft.Identity.Client.Extensions.Web
#endif
{
    /// <summary>
    /// A cross-process lock that works on all platforms.
    /// It is important to note that this lock is not thread safe !
    /// </summary>
    internal sealed class CrossPlatLock : IDisposable
    {
        internal const int LockfileRetryDelayDefault = 100;
        internal const int LockfileRetryCountDefault = 60000 / LockfileRetryDelayDefault;
        private FileStream _lockFileStream;

        public CrossPlatLock(string lockfilePath, int lockFileRetryDelay = LockfileRetryDelayDefault, int lockFileRetryCount = LockfileRetryCountDefault)
        {
            Exception exception = null;
            FileStream fileStream = null;

            // Create lock file dir if it doesn't already exist
            Directory.CreateDirectory(Path.GetDirectoryName(lockfilePath));

            for (int tryCount = 0; tryCount < lockFileRetryCount; tryCount++)
            {

                try
                {
                    // We are using the file locking to synchronize the store, do not allow multiple writers or readers for the file.
                    const int defaultBufferSize = 4096;
                    var fileShare = FileShare.None;
                    if (SharedUtilities.IsWindowsPlatform())
                    {
                        // This is so that Windows can offer read due to the granularity of the locking. Unix will not
                        // lock with FileShare.Read. Read access on Windows is only for debugging purposes and will not
                        // affect the functionality.
                        //
                        // See: https://github.com/dotnet/coreclr/blob/98472784f82cee7326a58e0c4acf77714cdafe03/src/System.Private.CoreLib/shared/System/IO/FileStream.Unix.cs#L74-L89
                        fileShare = FileShare.Read;
                    }

                    var fileOptions = FileOptions.DeleteOnClose;
                    if (SharedUtilities.IsMonoPlatform())
                    {
                        // Deleting on close/dispose would cause a file locked by another process to be deleted when
                        // running on Mono since locking is a two step process - it requires creating a FileStream and then
                        // calling FileStream.Lock, which then may fail.
                        fileOptions = FileOptions.None;
                    }

                    fileStream = new FileStream(lockfilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, fileShare, defaultBufferSize, fileOptions);

                    if (SharedUtilities.IsMonoPlatform())
                    {
                        // Mono requires FileStream.Lock to be called to lock the file. Using FileShare.None when creating the
                        // FileStream is not enough to lock the file on Mono.
                        fileStream.Lock(0, 0);
                    }

                    using (var writer = new StreamWriter(fileStream, Encoding.UTF8, defaultBufferSize, leaveOpen: true))
                    {
                        writer.WriteLine($"{Process.GetCurrentProcess().Id} {Process.GetCurrentProcess().ProcessName}");
                    }
                        break;
                }
                catch (IOException ex)
                {
                    fileStream?.Dispose();
                    fileStream = null;
                    exception = ex;
                    Thread.Sleep(lockFileRetryDelay);
                }
                catch (UnauthorizedAccessException ex)
                {
                    fileStream?.Dispose();
                    fileStream = null;
                    exception = ex;
                    Thread.Sleep(lockFileRetryCount);
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
