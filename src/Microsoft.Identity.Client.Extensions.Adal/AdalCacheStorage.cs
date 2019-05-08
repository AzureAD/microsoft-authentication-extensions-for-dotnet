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
        private static readonly Lazy<TraceSource> s_staticLogger = new Lazy<TraceSource>(() =>
        {
            return (TraceSource)EnvUtils.GetNewTraceSource(nameof(AdalCacheStorage) + "Singleton");
        });

        private const int FileLockRetryCount = 20;
        private const int FileLockRetryWaitInMs = 200;

        private readonly TraceSource _logger;

        internal StorageCreationProperties CreationProperties { get; }

        // This is set to empty string here. During a file read, if the token isn't available, we will create a new guid, so we will always read the first time.
        private string _lastVersionToken = string.Empty;

        private IntPtr _libsecretSchema = IntPtr.Zero;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdalCacheStorage"/> class.
        /// </summary>
        /// <param name="creationProperties">Creation properties for storage on disk</param>
        /// <param name="logger">logger</param>
        public AdalCacheStorage(StorageCreationProperties creationProperties, TraceSource logger)
        {
            _logger = logger ?? s_staticLogger.Value;
            CreationProperties = creationProperties ?? throw new ArgumentNullException(nameof(creationProperties));
            logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Initialized '{nameof(AdalCacheStorage)}' with cacheFilePath '{creationProperties.CacheDirectory}'");
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
        /// Gets the guid representing the last time the cache was changed on disk.
        /// </summary>
        internal string LastVersionToken => _lastVersionToken;

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
                bool versionFileExists = File.Exists(VersionFilePath);
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Has the store changed");
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"VersionFileExists '{versionFileExists}'");

                if (!versionFileExists)
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"The version file does not exist, treat as 'changed'.");
                    return true;
                }

                string currentVersion = File.ReadAllText(VersionFilePath);
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Current version '{currentVersion}' Last version '{_lastVersionToken}'");

                bool hasChanged = !currentVersion.Equals(_lastVersionToken, StringComparison.OrdinalIgnoreCase);

                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Cache has changed '{hasChanged}'");
                return hasChanged;
            }
        }

        /// <summary>
        /// Writes a new guid to the file at <see cref="VersionFilePath"/>, and updates <see cref="LastVersionToken"/>.
        /// </summary>
        private void WriteVersionFile()
        {
            try
            {
                string newVersion = Guid.NewGuid().ToString();
                File.WriteAllText(VersionFilePath, newVersion);
                _lastVersionToken = newVersion;
            }
            catch (IOException ex)
            {
                _logger.TraceEvent(TraceEventType.Warning, /*id*/ 0, $"Unable to write version file due to exception: '{ex.Message}'");
            }
        }

        /// <summary>
        /// Read and unprotect adal cache data
        /// </summary>
        /// <returns>Unprotected adal cache data</returns>
        public byte[] ReadData()
        {
            bool cacheFileExists = File.Exists(CacheFilePath);
            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"ReadData Cache file exists '{cacheFileExists}'");

            byte[] data = null;

            bool alreadyLoggedException = false;
            try
            {
                // Guarantee that the version file exists so that we can know if it changes.
                if (!File.Exists(VersionFilePath))
                {
                    WriteVersionFile();
                }

                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Reading Data");
                byte[] fileData = ReadDataCore();

                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Got '{fileData?.Length}' bytes from file storage");

                if (fileData != null && fileData.Length > 0)
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Unprotecting the data");
                    if (SharedUtilities.IsWindowsPlatform())
                    {
                        data = ProtectedData.Unprotect(fileData, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
                    }
                    else
                    {
                        data = fileData;
                    }
                }
                else if (fileData == null || fileData.Length == 0)
                {
                    data = new byte[0];
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Empty data does not need to be unprotected");
                }
                else
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Data does not need to be unprotected");
                    return fileData;
                }
            }
            catch (Exception e)
            {
                if (!alreadyLoggedException)
                {
                    _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An exception was encountered while reading data from the {nameof(AdalCacheStorage)} : {e}");
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
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Got '{data?.Length}' bytes to write to storage");

                if (SharedUtilities.IsWindowsPlatform() && data.Length != 0)
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Protecting the data");
                    data = ProtectedData.Protect(data, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
                }

                WriteDataCore(data);
            }
            catch (Exception e)
            {
                _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An exception was encountered while writing data from the {nameof(AdalCacheStorage)} : {e}");
            }
        }

        /// <summary>
        /// Delete Adal cache file
        /// </summary>
        public void Clear()
        {
            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Clearing the adal cache file");
            ClearCore();
        }

        private byte[] ReadDataCore()
        {
            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "ReadDataCore");

            byte[] fileData = null;

            bool cacheFileExists = File.Exists(CacheFilePath);
            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"ReadDataCore Cache file exists '{cacheFileExists}'");

            if (SharedUtilities.IsWindowsPlatform())
            {
                if (cacheFileExists)
                {
                    TryProcessFile(() =>
                    {
                        fileData = File.ReadAllBytes(CacheFilePath);
                        _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"ReadDataCore, read '{fileData.Length}' bytes from the file");
                    });
                }
            }
            else if (SharedUtilities.IsMacPlatform())
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"ReadDataCore, Before reading from mac keychain");
                fileData = MacKeyChain.RetrieveKey(CreationProperties.MacKeyChainServiceName, CreationProperties.MacKeyChainAccountName, _logger);

                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"ReadDataCore, read '{fileData?.Length}' bytes from the keychain");
            }
            else if (SharedUtilities.IsLinuxPlatform())
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"ReadDataCore, Before reading from linux keyring");

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
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An error was encountered while reading secret from keyring in the {nameof(AdalCacheStorage)} domain:'{err.Domain}' code:'{err.Code}' message:'{err.Message}'");
                    }
                    catch (Exception e)
                    {
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An exception was encountered while processing libsecret error information during reading in the {nameof(AdalCacheStorage)} ex:'{e}'");
                    }
                }
                else if (string.IsNullOrEmpty(secret))
                {
                    _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, "No matching secret found in the keyring");
                }
                else
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Base64 decoding the secret string");
                    fileData = Convert.FromBase64String(secret);
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"ReadDataCore, read '{fileData?.Length}' bytes from the keyring");
                }
            }
            else
            {
                _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, "Platform not supported");
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

            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Write Data core, goign to write '{data.Length}' to the storage");

            if (SharedUtilities.IsMacPlatform() || SharedUtilities.IsLinuxPlatform())
            {
                if (SharedUtilities.IsMacPlatform())
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Before write to mac keychain");
                    MacKeyChain.WriteKey(
                                         CreationProperties.MacKeyChainServiceName,
                                         CreationProperties.MacKeyChainAccountName,
                                         data);

                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "After write to mac keychain");
                }
                else if (SharedUtilities.IsLinuxPlatform())
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Before saving to linux keyring");

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
                            _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An error was encountered while saving secret to keyring in the {nameof(AdalCacheStorage)} domain:'{err.Domain}' code:'{err.Code}' message:'{err.Message}'");
                        }
                        catch (Exception e)
                        {
                            _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An exception was encountered while processing libsecret error information during saving in the {nameof(AdalCacheStorage)} ex:'{e}'");
                        }
                    }

                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "After saving to linux keyring");
                }

                // Change data to 1 byte so we can write it to the cache file to update the last write time using the same write code used for windows.
                data = new byte[] { 1 };
            }

            string directoryForCacheFile = Path.GetDirectoryName(CacheFilePath);
            if (!Directory.Exists(directoryForCacheFile))
            {
                string directory = Path.GetDirectoryName(CacheFilePath);
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Creating directory '{directory}'");
                Directory.CreateDirectory(directory);
            }

            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Cache file directory exists. '{Directory.Exists(directoryForCacheFile)}' now writing cache file");

            TryProcessFile(() =>
            {
                File.WriteAllBytes(CacheFilePath, data);
                WriteVersionFile();
            });
        }

        private void ClearCore()
        {
            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Clearing adal cache");
            bool cacheFileExists = File.Exists(CacheFilePath);
            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"ReadDataCore Cache file exists '{cacheFileExists}'");

            TryProcessFile(() =>
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Before deleting the cache file");
                try
                {
                    File.Delete(CacheFilePath);
                }
                catch (Exception e)
                {
                    _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"Problem deleting the cache file '{e}'");
                }

                WriteVersionFile();
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After deleting the cache file. Last write time is '{_lastVersionToken}'");
            });

            if (SharedUtilities.IsMacPlatform())
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Before delete mac keychain");
                MacKeyChain.DeleteKey(
                                      CreationProperties.MacKeyChainServiceName,
                                      CreationProperties.MacKeyChainAccountName);
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "After delete mac keychain");
            }
            else if (SharedUtilities.IsLinuxPlatform())
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before deletring secret from linux keyring");

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
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An error was encountered while clearing secret from keyring in the {nameof(AdalCacheStorage)} domain:'{err.Domain}' code:'{err.Code}' message:'{err.Message}'");
                    }
                    catch (Exception e)
                    {
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An exception was encountered while processing libsecret error information during clearing secret in the {nameof(AdalCacheStorage)} ex:'{e}'");
                    }
                }

                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "After deleting secret from linux keyring");
            }
            else if (!SharedUtilities.IsWindowsPlatform())
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Not supported platform");
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
                    Thread.Sleep(TimeSpan.FromMilliseconds(FileLockRetryWaitInMs));

                    if (tryCount == FileLockRetryCount)
                    {
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An exception was encountered while processing the Adal cache file from the {nameof(AdalCacheStorage)} ex:'{e}'");
                    }
                }
            }
        }

        private IntPtr GetLibsecretSchema()
        {
            if (_libsecretSchema == IntPtr.Zero)
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Before creating libsecret schema");

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
                    _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"Failed to create libsecret schema from the {nameof(AdalCacheStorage)}");
                }

                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "After creating libsecret schema");
            }

            return _libsecretSchema;
        }
    }
}
