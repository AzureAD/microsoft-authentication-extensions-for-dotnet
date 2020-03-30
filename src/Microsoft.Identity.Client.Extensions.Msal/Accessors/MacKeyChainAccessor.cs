// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;

namespace Microsoft.Identity.Client.Extensions.Msal
{
    /// <summary>
    /// 
    /// </summary>
    internal class MacKeychainAccessor : ICacheAccessor
    {
        private readonly string _cacheFilePath;
        private readonly string _keyChainServiceName;
        private readonly string _keyChainAccountName;
        private readonly TraceSourceLogger _logger;

        public MacKeychainAccessor(string cacheFilePath, string keyChainServiceName, string keyChainAccountName, TraceSourceLogger logger) 
        {
            if (string.IsNullOrWhiteSpace(cacheFilePath))
            {
                throw new ArgumentNullException(nameof(cacheFilePath));
            }

            if (string.IsNullOrWhiteSpace(keyChainServiceName))
            {
                throw new ArgumentNullException( nameof(keyChainServiceName));
            }

            if (string.IsNullOrWhiteSpace(keyChainAccountName))
            {
                throw new ArgumentNullException( nameof(keyChainAccountName));
            }

            _cacheFilePath = cacheFilePath;
            _keyChainServiceName = keyChainServiceName;
            _keyChainAccountName = keyChainAccountName;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Clear()
        {
            _logger.LogInformation("Clearing cache");
            FileIOWithRetries.DeleteCacheFile(_cacheFilePath, _logger);

            _logger.LogInformation("Before delete mac keychain");
            MacKeyChain.DeleteKey(_keyChainServiceName, _keyChainAccountName);
            _logger.LogInformation("After delete mac keychain");
        }

       
        public byte[] Read()
        {
            _logger.LogInformation("ReadDataCore");

            _logger.LogInformation($"ReadDataCore, Before reading from mac keychain");
            byte[] fileData = MacKeyChain.RetrieveKey(_keyChainServiceName, _keyChainAccountName, _logger);
            _logger.LogInformation($"ReadDataCore, read '{fileData?.Length}' bytes from the keychain");

            return fileData;
        }

        public void Write(byte[] data)
        {
            _logger.LogInformation("Before write to mac keychain");
            MacKeyChain.WriteKey(_keyChainServiceName, _keyChainAccountName, data);
            _logger.LogInformation("After write to mac keychain");

            // Change data to 1 byte so we can write it to the cache file to update the last write time using the same write code used for windows.
            FileIOWithRetries.WriteDataToFile(_cacheFilePath, new byte[] { 1 }, _logger);
        }

        public ICacheAccessor CreateForPersistenceValidation()
        {
            return new MacKeychainAccessor(
                _cacheFilePath + ".test",
                _keyChainServiceName + "test",
                _keyChainAccountName + "test",
                _logger);
        }

    }
}
