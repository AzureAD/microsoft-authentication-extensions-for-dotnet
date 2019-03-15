// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace Microsoft.Identity.Extensions.Msal
{
    /// <summary>
    /// Persist cache to file
    /// </summary>
    public sealed class IdentityServiceTokenCacheStorage
    {
        private const string MSALCacheFileName = "msal.cache";
        private const int FileLockRetryCount = 20;
        private const int FileLockRetryWaitInMs = 200;
        private const string MacKeyChainServiceName = "Microsoft.Developer.IdentityService";
        private const string MacKeyChainAccountName = "MSALCache";
        private const string KeyringSchemaName = "msal.cache";
        private const string KeyringCollection = "default";
        private const string KeyringSecretLabel = "MSALCache";
        private const string KeyringAttribute1 = "Microsoft.Developer.IdentityService";
        private const string KeyringAttribute2 = "1.0.0.0";

        private readonly TraceSource logger;

        // When the file is not found when calling get last writetimeUtc it does not return the minimum date time or datetime offset
        // lets make sure we get what the actual value is on the runtime we are executing under.
        private readonly DateTimeOffset fileNotFoundOffset;

        private DateTimeOffset lastWriteTime;

        private IntPtr libsecretSchema = IntPtr.Zero;

        /// <summary>
        /// Initializes a new instance of the <see cref="IdentityServiceTokenCacheStorage"/> class.
        /// </summary>
        /// <param name="cacheFilePath">cache file path for acal cache storage</param>
        /// <param name="instanceName">instance name for acal cache storage</param>
        /// <param name="logger">logger</param>
        public IdentityServiceTokenCacheStorage(string cacheFilePath, string instanceName, TraceSource logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Initializing '{nameof(IdentityServiceTokenCacheStorage)}' with cacheFilePath '{cacheFilePath}' and instance name '{instanceName}"));

            try
            {
                logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Getting last write file time for a missing file in localappdata"));
                this.fileNotFoundOffset = File.GetLastWriteTimeUtc(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FormattableString.Invariant($"{Guid.NewGuid().FormatGuidAsString()}.dll")));
            }
            catch (Exception e)
            {
                logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Problem getting last file write time for missing file, trying temp path('{Path.GetTempPath()}'). {e.Message}"));
                this.fileNotFoundOffset = File.GetLastWriteTimeUtc(Path.Combine(Path.GetTempPath(), FormattableString.Invariant($"{Guid.NewGuid().FormatGuidAsString()}.dll")));
            }

            if (string.IsNullOrWhiteSpace(cacheFilePath))
            {
                try
                {
                    string localDataPath = SharedUtilities.GetDefaultArtifactPath();
                    logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Using the home directory '{localDataPath}'"));

                    string basepath = string.IsNullOrEmpty(instanceName) ? string.Empty : instanceName;
                    logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Using the BasePath '{basepath}'"));
                    this.CacheFilePath = Path.Combine(localDataPath, basepath, IdentityServiceTokenCacheStorage.MSALCacheFileName);
                    logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Generated cache file path '{this.CacheFilePath}'"));
                }
                catch (Exception e)
                {
                    logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"There was a problem generating the cache file path '{e}'"));
                    throw;
                }
            }
            else
            {
                this.CacheFilePath = cacheFilePath;
            }

            this.lastWriteTime = this.fileNotFoundOffset;
            logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Finished initializing '{nameof(IdentityServiceTokenCacheStorage)}'"));
        }

        /// <summary>
        /// Gets cache file path
        /// </summary>
        public string CacheFilePath
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a value indicating whether the persist file has changed before load it to cache
        /// </summary>
        public bool HasChanged
        {
            get
            {
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Has the store changed");
                bool cacheFileExists = File.Exists(this.CacheFilePath);
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Cache file exists '{cacheFileExists}'"));

                DateTimeOffset currentWriteTime = File.GetLastWriteTimeUtc(this.CacheFilePath);
                bool hasChanged = currentWriteTime != this.lastWriteTime;

                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"CurrentWriteTime '{currentWriteTime}' LastWriteTime '{this.lastWriteTime}'"));
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Cache has changed '{hasChanged}'"));
                return hasChanged;
            }
        }

        /// <summary>
        /// Read and unprotect cache data
        /// </summary>
        /// <returns>Unprotected cache data</returns>
        public byte[] ReadData()
        {
            bool cacheFileExists = File.Exists(this.CacheFilePath);
            this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"ReadData Cache file exists '{cacheFileExists}'"));

            byte[] data = null;

            bool alreadyLoggedException = false;
            try
            {
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Reading Data");
                byte[] fileData = this.ReadDataCore();

                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Got '{fileData?.Length}' bytes from file storage"));

                if (fileData != null && fileData.Length > 0)
                {
                    this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Unprotecting the data");
#if NET45
                    if (SharedUtilities.IsWindowsPlatform())
                    {
                        data = ProtectedData.Unprotect(fileData, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
                    }
#else
                    data = fileData;
#endif
                }
                else if (fileData == null || fileData.Length == 0)
                {
                    data = new byte[0];
                    this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Empty data does not need to be unprotected");
                }
                else
                {
                    this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Data does not need to be unprotected");
                    return fileData;
                }
            }
            catch (Exception e)
            {
                if (!alreadyLoggedException)
                {
                    this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while reading data from the {nameof(IdentityServiceTokenCacheStorage)} : {e}"));
                }

                this.ClearCore();
            }

            // If the file does not exist this
            this.lastWriteTime = File.GetLastWriteTimeUtc(this.CacheFilePath);
            return data;
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
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Got '{data?.Length}' bytes to write to storage"));
#if NET45
                if (SharedUtilities.IsWindowsPlatform() && data.Length != 0)
                {
                    this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Protecting the data");
                    data = ProtectedData.Protect(data, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
                }
#endif

                this.WriteDataCore(data);
            }
            catch (Exception e)
            {
                this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while writing data from the {nameof(IdentityServiceTokenCacheStorage)} : {e}"));
            }
        }

        /// <summary>
        /// Delete cache file
        /// </summary>
        public void Clear()
        {
            this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Clearing the cache file");
            this.ClearCore();
        }

        private byte[] ReadDataCore()
        {
            this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "ReadDataCore");

            byte[] fileData = null;

            bool cacheFileExists = File.Exists(this.CacheFilePath);
            this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"ReadDataCore Cache file exists '{cacheFileExists}'"));

            if (SharedUtilities.IsWindowsPlatform())
            {
                if (cacheFileExists)
                {
                    this.TryProcessFile(() =>
                    {
                        fileData = File.ReadAllBytes(this.CacheFilePath);
                        this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"ReadDataCore, read '{fileData.Length}' bytes from the file"));
                    });
                }
            }
            else if (SharedUtilities.IsMacPlatform())
            {
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"ReadDataCore, Before reading from mac keychain");
                fileData = MacKeyChain.RetrieveKey(MacKeyChainServiceName, MacKeyChainAccountName, this.logger);

                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"ReadDataCore, read '{fileData?.Length}' bytes from the keychain"));
            }
            else if (SharedUtilities.IsLinuxPlatform())
            {
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"ReadDataCore, Before reading from linux keyring");

                IntPtr error = IntPtr.Zero;

                string secret = Libsecret.secret_password_lookup_sync(
                    schema: this.GetLibsecretSchema(),
                    cancellable: IntPtr.Zero,
                    error: out error,
                    attribute1Type: "string1",
                    attribute1Value: KeyringAttribute1,
                    attribute2Type: "string2",
                    attribute2Value: KeyringAttribute2,
                    end: IntPtr.Zero);

                if (error != IntPtr.Zero)
                {
                    try
                    {
                        GError err = (GError)Marshal.PtrToStructure(error, typeof(GError));
                        this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An error was encountered while reading secret from keyring in the {nameof(IdentityServiceTokenCacheStorage)} domain:'{err.Domain}' code:'{err.Code}' message:'{err.Message}'"));
                    }
                    catch (Exception e)
                    {
                        this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while processing libsecret error information during reading in the {nameof(IdentityServiceTokenCacheStorage)} ex:'{e}'"));
                    }
                }
                else if (string.IsNullOrEmpty(secret))
                {
                    this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, "No matching secret found in the keyring");
                }
                else
                {
                    this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Base64 decoding the secret string");
                    fileData = Convert.FromBase64String(secret);
                    this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"ReadDataCore, read '{fileData?.Length}' bytes from the keyring"));
                }
            }
            else
            {
                this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, "Platform not supported");
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

            this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Write Data core, goign to write '{data.Length}' to the storage"));

            if (SharedUtilities.IsMacPlatform() || SharedUtilities.IsLinuxPlatform())
            {
                if (SharedUtilities.IsMacPlatform())
                {
                    this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Before write to mac keychain");
                    MacKeyChain.WriteKey(
                                         MacKeyChainServiceName,
                                         MacKeyChainAccountName,
                                         data);

                    this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "After write to mac keychain");
                }
                else if (SharedUtilities.IsLinuxPlatform())
                {
                    this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Before saving to linux keyring");

                    IntPtr error = IntPtr.Zero;

                    Libsecret.secret_password_store_sync(
                        schema: this.GetLibsecretSchema(),
                        collection: KeyringCollection,
                        label: KeyringSecretLabel,
                        password: Convert.ToBase64String(data),
                        cancellable: IntPtr.Zero,
                        error: out error,
                        attribute1Type: "string1",
                        attribute1Value: KeyringAttribute1,
                        attribute2Type: "string2",
                        attribute2Value: KeyringAttribute2,
                        end: IntPtr.Zero);

                    if (error != IntPtr.Zero)
                    {
                        try
                        {
                            GError err = (GError)Marshal.PtrToStructure(error, typeof(GError));
                            this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An error was encountered while saving secret to keyring in the {nameof(IdentityServiceTokenCacheStorage)} domain:'{err.Domain}' code:'{err.Code}' message:'{err.Message}'"));
                        }
                        catch (Exception e)
                        {
                            this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while processing libsecret error information during saving in the {nameof(IdentityServiceTokenCacheStorage)} ex:'{e}'"));
                        }
                    }

                    this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "After saving to linux keyring");
                }

                // Change data to 1 byte so we can write it to the cache file to update the last write time using the same write code used for windows.
                data = new byte[] { 1 };
            }

            string directoryForCacheFile = Path.GetDirectoryName(this.CacheFilePath);
            if (!Directory.Exists(directoryForCacheFile))
            {
                string directory = Path.GetDirectoryName(this.CacheFilePath);
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Creating directory '{directory}'"));
                Directory.CreateDirectory(directory);
            }

            this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Cache file directory exists. '{Directory.Exists(directoryForCacheFile)}' now writing cache file"));

            this.TryProcessFile(() =>
            {
                File.WriteAllBytes(this.CacheFilePath, data);
                this.lastWriteTime = File.GetLastWriteTimeUtc(this.CacheFilePath);
            });
        }

        private void ClearCore()
        {
            this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Clearing cache");
            bool cacheFileExists = File.Exists(this.CacheFilePath);
            this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"ReadDataCore Cache file exists '{cacheFileExists}'"));

            this.TryProcessFile(() =>
            {
                    this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Before deleting the cache file");
                    try
                    {
                        File.Delete(this.CacheFilePath);
                    }
                    catch (Exception e)
                    {
                        this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"Problem deleting the cache file '{e}'"));
                    }

                    this.lastWriteTime = File.GetLastWriteTimeUtc(this.CacheFilePath);
                    this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"After deleting the cache file. Last write time is '{this.lastWriteTime}'"));
            });

            if (SharedUtilities.IsMacPlatform())
            {
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Before delete mac keychain");
                MacKeyChain.DeleteKey(
                                      MacKeyChainServiceName,
                                      MacKeyChainAccountName);
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "After delete mac keychain");
            }
            else if (SharedUtilities.IsLinuxPlatform())
            {
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before deletring secret from linux keyring");

                IntPtr error = IntPtr.Zero;

                Libsecret.secret_password_clear_sync(
                    schema: this.GetLibsecretSchema(),
                    cancellable: IntPtr.Zero,
                    error: out error,
                    attribute1Type: "string1",
                    attribute1Value: KeyringAttribute1,
                    attribute2Type: "string2",
                    attribute2Value: KeyringAttribute2,
                    end: IntPtr.Zero);

                if (error != IntPtr.Zero)
                {
                    try
                    {
                        GError err = (GError)Marshal.PtrToStructure(error, typeof(GError));
                        this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An error was encountered while clearing secret from keyring in the {nameof(IdentityServiceTokenCacheStorage)} domain:'{err.Domain}' code:'{err.Code}' message:'{err.Message}'"));
                    }
                    catch (Exception e)
                    {
                        this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while processing libsecret error information during clearing secret in the {nameof(IdentityServiceTokenCacheStorage)} ex:'{e}'"));
                    }
                }

                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "After deleting secret from linux keyring");
            }
            else if (!SharedUtilities.IsWindowsPlatform())
            {
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Not supported platform");
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
                        this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while processing the cache file from the {nameof(IdentityServiceTokenCacheStorage)} ex:'{e}'"));
                    }
                }
            }
        }

        private IntPtr GetLibsecretSchema()
        {
            if (this.libsecretSchema == IntPtr.Zero)
            {
                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "Before creating libsecret schema");

                this.libsecretSchema = Libsecret.secret_schema_new(
                    name: KeyringSchemaName,
                    flags: (int)Libsecret.SecretSchemaFlags.SECRET_SCHEMA_DONT_MATCH_NAME,
                    attribute1: "string1",
                    attribute1Type: (int)Libsecret.SecretSchemaAttributeType.SECRET_SCHEMA_ATTRIBUTE_STRING,
                    attribute2: "string2",
                    attribute2Type: (int)Libsecret.SecretSchemaAttributeType.SECRET_SCHEMA_ATTRIBUTE_STRING,
                    end: IntPtr.Zero);

                if (this.libsecretSchema == IntPtr.Zero)
                {
                    this.logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"Failed to create libsecret schema from the {nameof(IdentityServiceTokenCacheStorage)}"));
                }

                this.logger.TraceEvent(TraceEventType.Information, /*id*/ 0, "After creating libsecret schema");
            }

            return this.libsecretSchema;
        }
    }
}
