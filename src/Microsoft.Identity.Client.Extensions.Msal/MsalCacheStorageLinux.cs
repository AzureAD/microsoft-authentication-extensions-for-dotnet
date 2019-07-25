// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Msal
{
    /// <summary>
    /// 
    /// </summary>
    public class MsalCacheStorageLinux : MsalCacheStorage
    {
        private IntPtr _libsecretSchema = IntPtr.Zero;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="creationProperties"></param>
        /// <param name="logger"></param>
        public MsalCacheStorageLinux(StorageCreationProperties creationProperties, TraceSource logger = null) : base(creationProperties, logger)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void ClearCore()
        {
            _logger.LogInformation("Clearing cache");
            DeleteCacheFile(CacheFilePath);

            _logger.LogInformation($"Before deletring secret from linux keyring");

            IntPtr error = IntPtr.Zero;

            Libsecret.secret_password_clear_sync(
                schema: GetLibsecretSchema(),
                cancellable: IntPtr.Zero,
                error: out error,
                attribute1Type: _creationProperties.KeyringAttribute1.Key,
                attribute1Value: _creationProperties.KeyringAttribute1.Value,
                attribute2Type: _creationProperties.KeyringAttribute2.Key,
                attribute2Value: _creationProperties.KeyringAttribute2.Value,
                end: IntPtr.Zero);

            if (error != IntPtr.Zero)
            {
                try
                {
                    GError err = (GError)Marshal.PtrToStructure(error, typeof(GError));
                    _logger.LogError($"An error was encountered while clearing secret from keyring in the {nameof(MsalCacheStorage)} domain:'{err.Domain}' code:'{err.Code}' message:'{err.Message}'");
                }
                catch (Exception e)
                {
                    _logger.LogError($"An exception was encountered while processing libsecret error information during clearing secret in the {nameof(MsalCacheStorage)} ex:'{e}'");
                }
            }

            _logger.LogInformation("After deleting secret from linux keyring");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected override byte[] ReadDataCore()
        {
            _logger.LogInformation("ReadDataCore");

            _logger.LogInformation($"ReadDataCore, Before reading from linux keyring");

            byte[] fileData = null;

            IntPtr error = IntPtr.Zero;

            string secret = Libsecret.secret_password_lookup_sync(
                schema: GetLibsecretSchema(),
                cancellable: IntPtr.Zero,
                error: out error,
                attribute1Type: _creationProperties.KeyringAttribute1.Key,
                attribute1Value: _creationProperties.KeyringAttribute1.Value,
                attribute2Type: _creationProperties.KeyringAttribute2.Key,
                attribute2Value: _creationProperties.KeyringAttribute2.Value,
                end: IntPtr.Zero);

            if (error != IntPtr.Zero)
            {
                try
                {
                    GError err = (GError)Marshal.PtrToStructure(error, typeof(GError));
                    _logger.LogError($"An error was encountered while reading secret from keyring in the {nameof(MsalCacheStorage)} domain:'{err.Domain}' code:'{err.Code}' message:'{err.Message}'");
                }
                catch (Exception e)
                {
                    _logger.LogError($"An exception was encountered while processing libsecret error information during reading in the {nameof(MsalCacheStorage)} ex:'{e}'");
                }
            }
            else if (string.IsNullOrEmpty(secret))
            {
                _logger.LogError("No matching secret found in the keyring");
            }
            else
            {
                _logger.LogInformation("Base64 decoding the secret string");
                fileData = Convert.FromBase64String(secret);
                _logger.LogInformation($"ReadDataCore, read '{fileData?.Length}' bytes from the keyring");
            }

            return fileData;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        protected override void WriteDataCore(byte[] data)
        {
            _logger.LogInformation("Before saving to linux keyring");

            IntPtr error = IntPtr.Zero;

            Libsecret.secret_password_store_sync(
                schema: GetLibsecretSchema(),
                collection: _creationProperties.KeyringCollection,
                label: _creationProperties.KeyringSecretLabel,
                password: Convert.ToBase64String(data),
                cancellable: IntPtr.Zero,
                error: out error,
                attribute1Type: _creationProperties.KeyringAttribute1.Key,
                attribute1Value: _creationProperties.KeyringAttribute1.Value,
                attribute2Type: _creationProperties.KeyringAttribute2.Key,
                attribute2Value: _creationProperties.KeyringAttribute2.Value,
                end: IntPtr.Zero);

            if (error != IntPtr.Zero)
            {
                try
                {
                    GError err = (GError)Marshal.PtrToStructure(error, typeof(GError));
                    _logger.LogError($"An error was encountered while saving secret to keyring in the {nameof(MsalCacheStorage)} domain:'{err.Domain}' code:'{err.Code}' message:'{err.Message}'");
                }
                catch (Exception e)
                {
                    _logger.LogError($"An exception was encountered while processing libsecret error information during saving in the {nameof(MsalCacheStorage)} ex:'{e}'");
                }
            }

            _logger.LogInformation("After saving to linux keyring");

            // Change data to 1 byte so we can write it to the cache file to update the last write time using the same write code used for windows.
            WriteDataToFile(CacheFilePath, new byte[] { 1 });
        }

        private IntPtr GetLibsecretSchema()
        {
            if (_libsecretSchema == IntPtr.Zero)
            {
                _logger.LogInformation("Before creating libsecret schema");

                _libsecretSchema = Libsecret.secret_schema_new(
                    name: _creationProperties.KeyringSchemaName,
                    flags: (int)Libsecret.SecretSchemaFlags.SECRET_SCHEMA_DONT_MATCH_NAME,
                    attribute1: _creationProperties.KeyringAttribute1.Key,
                    attribute1Type: (int)Libsecret.SecretSchemaAttributeType.SECRET_SCHEMA_ATTRIBUTE_STRING,
                    attribute2: _creationProperties.KeyringAttribute2.Key,
                    attribute2Type: (int)Libsecret.SecretSchemaAttributeType.SECRET_SCHEMA_ATTRIBUTE_STRING,
                    end: IntPtr.Zero);

                if (_libsecretSchema == IntPtr.Zero)
                {
                    _logger.LogError($"Failed to create libsecret schema from the {nameof(MsalCacheStorage)}");
                }

                _logger.LogInformation("After creating libsecret schema");
            }

            return _libsecretSchema;
        }
    }
}
