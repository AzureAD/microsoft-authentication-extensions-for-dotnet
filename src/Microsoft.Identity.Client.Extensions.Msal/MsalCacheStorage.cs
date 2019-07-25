// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Microsoft.Identity.Client.Extensions.Msal
{
    /// <summary>
    /// Persist cache to file
    /// </summary>
    public abstract class MsalCacheStorage
    {
        private const int FileLockRetryCount = 20;
        private const int FileLockRetryWaitInMs = 200;
        internal readonly StorageCreationProperties _creationProperties;

        /// <summary>
        /// 
        /// </summary>
        protected readonly TraceSourceLogger _logger;

        /// <summary>
        /// A default logger for use if the user doesn't want to provide their own.
        /// </summary>
        protected static readonly Lazy<TraceSourceLogger> s_staticLogger = new Lazy<TraceSourceLogger>(() =>
        {
            return new TraceSourceLogger((TraceSource)EnvUtils.GetNewTraceSource(nameof(MsalCacheHelper) + "Singleton"));
        });

        /// <summary>
        /// Initializes a new instance of the <see cref="MsalCacheStorage"/> class.
        /// </summary>
        /// <param name="creationProperties">Properties for creating the cache storage on disk</param>
        /// <param name="logger">logger</param>
        /// <returns></returns>
        public static MsalCacheStorage Create(StorageCreationProperties creationProperties, TraceSource logger = null)
        {
            if (SharedUtilities.IsWindowsPlatform())
            {
                return new MsalCacheStorageWindows(creationProperties, logger);
            }
            else if (SharedUtilities.IsMacPlatform())
            {
                return new MsalCacheStorageMacOs(creationProperties, logger);
            }
            else if (SharedUtilities.IsLinuxPlatform())
            {
                return new MsalCacheStorageLinux(creationProperties, logger);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MsalCacheStorage"/> class.
        /// </summary>
        /// <param name="creationProperties">Properties for creating the cache storage on disk</param>
        /// <param name="logger">logger</param>
        protected MsalCacheStorage(StorageCreationProperties creationProperties, TraceSource logger = null)
        {
            _creationProperties = creationProperties;
            _logger = logger == null ? s_staticLogger.Value : new TraceSourceLogger(logger);
            _logger.LogInformation($"Initialized '{nameof(MsalCacheStorage)}'");
        }

        /// <summary>
        /// Gets cache file path
        /// </summary>
        public string CacheFilePath => _creationProperties.CacheFilePath;

        /// <summary>
        /// Gets a value indicating whether the persisted file has changed since we last read it.
        /// </summary>
        public bool HasChanged
        {
            get
            {
                // Attempts to make this more refined have all resulted in some form of cache inconsistency. Just returning
                // true here so we always load from disk.
                return true;
            }
        }

        /// <summary>
        /// Read and unprotect cache data
        /// </summary>
        /// <returns>Unprotected cache data</returns>
        public byte[] ReadData()
        {
            bool cacheFileExists = File.Exists(CacheFilePath);
            _logger.LogInformation($"ReadData Cache file exists '{cacheFileExists}'");

            byte[] data = null;
            try
            {
                _logger.LogInformation($"Reading Data");
                data = ReadDataCore();
                _logger.LogInformation($"Got '{data.Length}' bytes from file storage");
            }
            catch (Exception e)
            {
                _logger.LogError($"An exception was encountered while reading data from the {nameof(MsalCacheStorage)} : {e}");
                ClearCore();
            }

            return data ?? new byte[0];
        }

        /// <summary>
        /// Protect and write cache data to file
        /// </summary>
        /// <param name="data">Cache data</param>
        public void WriteData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                _logger.LogInformation($"Got '{data?.Length}' bytes to write to storage");
                WriteDataCore(data);
            }
            catch (Exception e)
            {
                _logger.LogError($"An exception was encountered while writing data to {nameof(MsalCacheStorage)} : {e}");
            }
        }

        /// <summary>
        /// Delete cache file
        /// </summary>
        public void Clear()
        {
            _logger.LogInformation("Clearing the cache file");
            ClearCore();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected abstract byte[] ReadDataCore();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        protected abstract void WriteDataCore(byte[] data);

        /// <summary>
        /// 
        /// </summary>
        protected abstract void ClearCore();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="data"></param>
        protected void WriteDataToFile(string filePath, byte[] data)
        {
            string directoryForCacheFile = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryForCacheFile))
            {
                string directory = Path.GetDirectoryName(filePath);
                _logger.LogInformation($"Creating directory '{directory}'");
                Directory.CreateDirectory(directory);
            }

            _logger.LogInformation($"Cache file directory exists. '{Directory.Exists(directoryForCacheFile)}' now writing cache file");

            TryProcessFile(() =>
            {
                File.WriteAllBytes(filePath, data);
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        protected void DeleteCacheFile(string filePath)
        {
            bool cacheFileExists = File.Exists(filePath);
            _logger.LogInformation($"DeleteCacheFile Cache file exists '{cacheFileExists}'");

            TryProcessFile(() =>
            {
                _logger.LogInformation("Before deleting the cache file");
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Problem deleting the cache file '{e}'");
                }

                _logger.LogInformation($"After deleting the cache file.");
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        protected void TryProcessFile(Action action)
        {
            for (int tryCount = 0; tryCount <= FileLockRetryCount; tryCount++)
            {
                try
                {
                    action.Invoke();
                    return;
                }
                catch (Exception e)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(FileLockRetryWaitInMs));

                    if (tryCount == FileLockRetryCount)
                    {
                        _logger.LogError($"An exception was encountered while processing the cache file from the {nameof(MsalCacheStorage)} ex:'{e}'");
                    }
                }
            }
        }
    }
}
