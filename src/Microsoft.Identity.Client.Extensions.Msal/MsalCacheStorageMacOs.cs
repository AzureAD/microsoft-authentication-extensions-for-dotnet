// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Microsoft.Identity.Client.Extensions.Msal
{
    /// <summary>
    /// 
    /// </summary>
    public class MsalCacheStorageMacOs : MsalCacheStorage
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="creationProperties"></param>
        /// <param name="logger"></param>
        public MsalCacheStorageMacOs(StorageCreationProperties creationProperties, TraceSource logger = null) : base(creationProperties, logger)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void ClearCore()
        {
            _logger.LogInformation("Clearing cache");
            DeleteCacheFile(CacheFilePath);

            _logger.LogInformation("Before delete mac keychain");
            MacKeyChain.DeleteKey(_creationProperties.MacKeyChainServiceName, _creationProperties.MacKeyChainAccountName);
            _logger.LogInformation("After delete mac keychain");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected override byte[] ReadDataCore()
        {
            _logger.LogInformation("ReadDataCore");

            byte[] fileData = null;

            _logger.LogInformation($"ReadDataCore, Before reading from mac keychain");
            fileData = MacKeyChain.RetrieveKey(_creationProperties.MacKeyChainServiceName, _creationProperties.MacKeyChainAccountName, _logger);
            _logger.LogInformation($"ReadDataCore, read '{fileData?.Length}' bytes from the keychain");

            return fileData;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        protected override void WriteDataCore(byte[] data)
        {
            _logger.LogInformation("Before write to mac keychain");
            MacKeyChain.WriteKey(_creationProperties.MacKeyChainServiceName, _creationProperties.MacKeyChainAccountName, data);
            _logger.LogInformation("After write to mac keychain");

            // Change data to 1 byte so we can write it to the cache file to update the last write time using the same write code used for windows.
            WriteDataToFile(CacheFilePath, new byte[] { 1 });
        }
    }
}
