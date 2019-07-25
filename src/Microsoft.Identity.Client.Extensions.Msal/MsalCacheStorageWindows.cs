// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace Microsoft.Identity.Client.Extensions.Msal
{
    /// <summary>
    /// 
    /// </summary>
    public class MsalCacheStorageWindows : MsalCacheStorage
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="creationProperties"></param>
        /// <param name="logger"></param>
        public MsalCacheStorageWindows(StorageCreationProperties creationProperties, TraceSource logger = null) : base(creationProperties, logger)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void ClearCore()
        {
            _logger.LogInformation("Clearing cache");
            DeleteCacheFile(CacheFilePath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected override byte[] ReadDataCore()
        {
            _logger.LogInformation("ReadDataCore");

            byte[] fileData = null;
            bool cacheFileExists = File.Exists(CacheFilePath);
            _logger.LogInformation($"ReadDataCore Cache file exists '{cacheFileExists}'");

            if (cacheFileExists)
            {
                TryProcessFile(() =>
                {
                    fileData = File.ReadAllBytes(CacheFilePath);
                    _logger.LogInformation($"ReadDataCore, read '{fileData.Length}' bytes from the file");
                });
            }

            if (fileData != null && fileData.Length > 0)
            {
                _logger.LogInformation($"Unprotecting the data");
                fileData = ProtectedData.Unprotect(fileData, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            }

            return fileData;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        protected override void WriteDataCore(byte[] data)
        {
            if (data.Length != 0)
            {
                _logger.LogInformation($"Protecting the data");
                data = ProtectedData.Protect(data, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            }

            WriteDataToFile(CacheFilePath, data);
        }
    }
}
