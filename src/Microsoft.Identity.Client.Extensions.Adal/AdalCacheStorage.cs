// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace Microsoft.Identity.Client.Extensions.Adal
{
    /// <summary>
    /// Persist adal cache to file
    /// </summary>
    public sealed class AdalCacheStorage
    {
        /// <summary>
        /// A default logger for use if the user doesn't want to provide their own.
        /// </summary>
        private static readonly Lazy<TraceSourceLogger> s_staticLogger = new Lazy<TraceSourceLogger>(() =>
        {
            return new TraceSourceLogger((TraceSource)EnvUtils.GetNewTraceSource(nameof(AdalCacheStorage) + "Singleton"));
        });

        private const int FileLockRetryCount = 20;
        private const int FileLockRetryDelayInMs = 200;

        private readonly TraceSourceLogger _logger;

        internal StorageCreationProperties CreationProperties { get; }

        private IntPtr _libsecretSchema = IntPtr.Zero;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdalCacheStorage"/> class.
        /// </summary>
        /// <param name="creationProperties">Creation properties for storage on disk</param>
        /// <param name="logger">logger</param>
        public AdalCacheStorage(StorageCreationProperties creationProperties, TraceSource logger)
        {
            _logger = logger == null ? s_staticLogger.Value : new TraceSourceLogger(logger);
            CreationProperties = creationProperties ?? throw new ArgumentNullException(nameof(creationProperties));
            _logger.LogInformation($"Initialized '{nameof(AdalCacheStorage)}' with cacheFilePath '{creationProperties.CacheDirectory}'");
        }

        /// <summary>
        /// Gets the user's root directory across platforms.
        /// </summary>
        public static string UserRootDirectory
        {
            get
            {
                return SharedUtilities.GetUserRootDirectory();
            }
        }

        /// <summary>
        /// Gets cache file path
        /// </summary>
        public string CacheFilePath => CreationProperties.CacheFilePath;

        /// <summary>
        /// Gets the file path containing the guid representing the last time the cache was changed on disk.
        /// </summary>
        private string VersionFilePath => CacheFilePath + ".version";

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
        /// Read and unprotect adal cache data
        /// </summary>
        /// <returns>Unprotected adal cache data</returns>
        public byte[] ReadData()
        {
            bool cacheFileExists = File.Exists(CacheFilePath);
            _logger.LogInformation($"ReadData Cache file exists '{cacheFileExists}'");

            byte[] data = null;

            bool alreadyLoggedException = false;
            try
            {
                _logger.LogInformation($"Reading Data");
                byte[] fileData = ReadDataCore();

                _logger.LogInformation($"Got '{fileData?.Length}' bytes from file storage");

                if (fileData != null && fileData.Length > 0)
                {
                    _logger.LogInformation($"Unprotecting the data");
                    data = SharedUtilities.IsWindowsPlatform() ?
                        ProtectedData.Unprotect(fileData, optionalEntropy: null, scope: DataProtectionScope.CurrentUser) :
                        fileData;
                }
                else if (fileData == null || fileData.Length == 0)
                {
                    data = new byte[0];
                    _logger.LogInformation($"Empty data does not need to be unprotected");
                }
                else
                {
                    _logger.LogInformation($"Data does not need to be unprotected");
                    return fileData;
                }
            }
            catch (Exception e)
            {
                if (!alreadyLoggedException)
                {
                    _logger.LogError($"An exception was encountered while reading data from the {nameof(AdalCacheStorage)} : {e}");
                }

                ClearCore();
            }

            return data;
        }

        /// <summary>
        /// Protect and write adal cache data to file
        /// </summary>
        /// <param name="data">Adal cache data</param>
        public void WriteData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                _logger.LogInformation($"Got '{data?.Length}' bytes to write to storage");
                if (SharedUtilities.IsWindowsPlatform() && data.Length != 0)
                {
                    _logger.LogInformation($"Protecting the data");
                    data = ProtectedData.Protect(data, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
                }

                WriteDataCore(data);
            }
            catch (Exception e)
            {
                _logger.LogError($"An exception was encountered while writing data from the {nameof(AdalCacheStorage)} : {e}");
            }
        }

        /// <summary>
        /// Delete Adal cache file
        /// </summary>
        public void Clear()
        {
            _logger.LogInformation("Clearing the adal cache file");
            ClearCore();
        }

        private byte[] ReadDataCore()
        {
            _logger.LogInformation("ReadDataCore");

            byte[] fileData = null;

            bool cacheFileExists = File.Exists(CacheFilePath);
            _logger.LogInformation($"ReadDataCore Cache file exists '{cacheFileExists}'");

            if (SharedUtilities.IsWindowsPlatform())
            {
                if (cacheFileExists)
                {
                    TryProcessFile(() =>
                    {
                        fileData = File.ReadAllBytes(CacheFilePath);
                        _logger.LogInformation($"ReadDataCore, read '{fileData.Length}' bytes from the file");
                    });
                }
            }
            else if (SharedUtilities.IsMacPlatform())
            {
                _logger.LogInformation($"ReadDataCore, Before reading from mac keychain");
                fileData = MacKeyChain.RetrieveKey(CreationProperties.MacKeyChainServiceName, CreationProperties.MacKeyChainAccountName, _logger);
                _logger.LogInformation($"ReadDataCore, read '{fileData?.Length}' bytes from the keychain");
            }
            else if (SharedUtilities.IsLinuxPlatform())
            {
                _logger.LogInformation($"ReadDataCore, Before reading from linux keyring");

                IntPtr error = IntPtr.Zero;

                string secret = Libsecret.secret_password_lookup_sync(
                    schema: GetLibsecretSchema(),
                    cancellable: IntPtr.Zero,
                    error: out error,
                    attribute1Type: CreationProperties.KeyringAttribute1.Key,
                    attribute1Value: CreationProperties.KeyringAttribute1.Value,
                    attribute2Type: CreationProperties.KeyringAttribute2.Key,
                    attribute2Value: CreationProperties.KeyringAttribute2.Value,
                    end: IntPtr.Zero);

                if (error != IntPtr.Zero)
                {
                    try
                    {
                        GError err = (GError)Marshal.PtrToStructure(error, typeof(GError));
                        _logger.LogError($"An error was encountered while reading secret from keyring in the {nameof(AdalCacheStorage)} domain:'{err.Domain}' code:'{err.Code}' message:'{err.Message}'");
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"An exception was encountered while processing libsecret error information during reading in the {nameof(AdalCacheStorage)} ex:'{e}'");
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
            }
            else
            {
                _logger.LogError("Platform not supported");
                throw new PlatformNotSupportedException();
            }

            return fileData;
        }

        private void WriteDataCore(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            _logger.LogInformation($"Write Data core, goign to write '{data.Length}' to the storage");

            if (SharedUtilities.IsMacPlatform() || SharedUtilities.IsLinuxPlatform())
            {
                if (SharedUtilities.IsMacPlatform())
                {
                    _logger.LogInformation("Before write to mac keychain");
                    MacKeyChain.WriteKey(
                                         CreationProperties.MacKeyChainServiceName,
                                         CreationProperties.MacKeyChainAccountName,
                                         data);

                    _logger.LogInformation("After write to mac keychain");
                }
                else if (SharedUtilities.IsLinuxPlatform())
                {
                    _logger.LogInformation("Before saving to linux keyring");

                    IntPtr error = IntPtr.Zero;

                    Libsecret.secret_password_store_sync(
                        schema: GetLibsecretSchema(),
                        collection: CreationProperties.KeyringCollection,
                        label: CreationProperties.KeyringSecretLabel,
                        password: Convert.ToBase64String(data),
                        cancellable: IntPtr.Zero,
                        error: out error,
                        attribute1Type: CreationProperties.KeyringAttribute1.Key,
                        attribute1Value: CreationProperties.KeyringAttribute1.Value,
                        attribute2Type: CreationProperties.KeyringAttribute2.Key,
                        attribute2Value: CreationProperties.KeyringAttribute2.Value,
                        end: IntPtr.Zero);

                    if (error != IntPtr.Zero)
                    {
                        try
                        {
                            GError err = (GError)Marshal.PtrToStructure(error, typeof(GError));
                            _logger.LogError($"An error was encountered while saving secret to keyring in the {nameof(AdalCacheStorage)} domain:'{err.Domain}' code:'{err.Code}' message:'{err.Message}'");
                        }
                        catch (Exception e)
                        {
                            _logger.LogError($"An exception was encountered while processing libsecret error information during saving in the {nameof(AdalCacheStorage)} ex:'{e}'");
                        }
                    }

                    _logger.LogInformation("After saving to linux keyring");
                }

                // Change data to 1 byte so we can write it to the cache file to update the last write time using the same write code used for windows.
                data = new byte[] { 1 };
            }

            string directoryForCacheFile = Path.GetDirectoryName(CacheFilePath);
            if (!Directory.Exists(directoryForCacheFile))
            {
                string directory = Path.GetDirectoryName(CacheFilePath);
                _logger.LogInformation($"Creating directory '{directory}'");
                Directory.CreateDirectory(directory);
            }

            _logger.LogInformation($"Cache file directory exists. '{Directory.Exists(directoryForCacheFile)}' now writing cache file");

            TryProcessFile(() =>
            {
                File.WriteAllBytes(CacheFilePath, data);
            });
        }

        private void ClearCore()
        {
            _logger.LogInformation("Clearing adal cache");
            bool cacheFileExists = File.Exists(CacheFilePath);
            _logger.LogInformation($"ReadDataCore Cache file exists '{cacheFileExists}'");

            TryProcessFile(() =>
            {
                _logger.LogInformation("Before deleting the cache file");
                try
                {
                    File.Delete(CacheFilePath);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Problem deleting the cache file '{e}'");
                }

                _logger.LogInformation($"After deleting the cache file.");
            });

            if (SharedUtilities.IsMacPlatform())
            {
                _logger.LogInformation("Before delete mac keychain");
                MacKeyChain.DeleteKey(
                                      CreationProperties.MacKeyChainServiceName,
                                      CreationProperties.MacKeyChainAccountName);
                _logger.LogInformation("After delete mac keychain");
            }
            else if (SharedUtilities.IsLinuxPlatform())
            {
                _logger.LogInformation($"Before deletring secret from linux keyring");

                IntPtr error = IntPtr.Zero;

                Libsecret.secret_password_clear_sync(
                    schema: GetLibsecretSchema(),
                    cancellable: IntPtr.Zero,
                    error: out error,
                    attribute1Type: CreationProperties.KeyringAttribute1.Key,
                    attribute1Value: CreationProperties.KeyringAttribute1.Value,
                    attribute2Type: CreationProperties.KeyringAttribute2.Key,
                    attribute2Value: CreationProperties.KeyringAttribute2.Value,
                    end: IntPtr.Zero);

                if (error != IntPtr.Zero)
                {
                    try
                    {
                        GError err = (GError)Marshal.PtrToStructure(error, typeof(GError));
                        _logger.LogError($"An error was encountered while clearing secret from keyring in the {nameof(AdalCacheStorage)} domain:'{err.Domain}' code:'{err.Code}' message:'{err.Message}'");
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"An exception was encountered while processing libsecret error information during clearing secret in the {nameof(AdalCacheStorage)} ex:'{e}'");
                    }
                }

                _logger.LogInformation("After deleting secret from linux keyring");
            }
            else if (!SharedUtilities.IsWindowsPlatform())
            {
                _logger.LogInformation("Not supported platform");
                throw new PlatformNotSupportedException();
            }
        }

        private void TryProcessFile(Action action)
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
                    Thread.Sleep(TimeSpan.FromMilliseconds(FileLockRetryDelayInMs));

                    if (tryCount == FileLockRetryCount)
                    {
                        _logger.LogError($"An exception was encountered while processing the Adal cache file from the {nameof(AdalCacheStorage)} ex:'{e}'");
                    }
                }
            }
        }

        private IntPtr GetLibsecretSchema()
        {
            if (_libsecretSchema == IntPtr.Zero)
            {
                _logger.LogInformation("Before creating libsecret schema");

                _libsecretSchema = Libsecret.secret_schema_new(
                    name: CreationProperties.KeyringSchemaName,
                    flags: (int)Libsecret.SecretSchemaFlags.SECRET_SCHEMA_DONT_MATCH_NAME,
                    attribute1: CreationProperties.KeyringAttribute1.Key,
                    attribute1Type: (int)Libsecret.SecretSchemaAttributeType.SECRET_SCHEMA_ATTRIBUTE_STRING,
                    attribute2: CreationProperties.KeyringAttribute2.Key,
                    attribute2Type: (int)Libsecret.SecretSchemaAttributeType.SECRET_SCHEMA_ATTRIBUTE_STRING,
                    end: IntPtr.Zero);

                if (_libsecretSchema == IntPtr.Zero)
                {
                    _logger.LogError($"Failed to create libsecret schema from the {nameof(AdalCacheStorage)}");
                }

                _logger.LogInformation("After creating libsecret schema");
            }

            return _libsecretSchema;
        }
    }
}
