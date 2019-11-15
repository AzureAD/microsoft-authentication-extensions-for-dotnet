// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Security.Cryptography;

namespace Microsoft.Identity.Client.Extensions.Msal
{
    internal class CacheAccessorWindows : ICacheAccessor
    {
        private readonly string _cacheFilePath;
        private readonly TraceSourceLogger _logger;

        public CacheAccessorWindows(string cacheFilePath, TraceSourceLogger logger)
        {
            _cacheFilePath = cacheFilePath;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Clear()
        {
            _logger.LogInformation("Clearing cache");
            FileIOWithRetries.DeleteCacheFile(_cacheFilePath, _logger);
        }

        public byte[] Read()
        {
            _logger.LogInformation("ReadDataCore");

            byte[] fileData = null;
            bool cacheFileExists = File.Exists(_cacheFilePath);
            _logger.LogInformation($"ReadDataCore Cache file exists '{cacheFileExists}'");

            if (cacheFileExists)
            {
                FileIOWithRetries.TryProcessFile(() =>
                {
                    fileData = File.ReadAllBytes(_cacheFilePath);
                    _logger.LogInformation($"ReadDataCore, read '{fileData.Length}' bytes from the file");
                }, _logger);
            }

            if (fileData != null && fileData.Length > 0)
            {
                _logger.LogInformation($"Unprotecting the data");
                fileData = ProtectedData.Unprotect(fileData, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            }

            return fileData;
        }

        public void Write(byte[] data)
        {
            if (data.Length != 0)
            {
                _logger.LogInformation($"Protecting the data");
                data = ProtectedData.Protect(data, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            }

            FileIOWithRetries.WriteDataToFile(_cacheFilePath, data, _logger);
        }
    }
}
