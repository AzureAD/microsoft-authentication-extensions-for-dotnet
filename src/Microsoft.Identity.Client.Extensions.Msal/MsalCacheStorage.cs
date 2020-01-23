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
    public class MsalCacheStorage 
    {
        private readonly TraceSourceLogger _logger;

        private readonly ICacheAccessor _cacheAccessor;

        internal StorageCreationProperties CreationProperties { get; }


        /// <summary>
        /// A default logger for use if the user doesn't want to provide their own.
        /// </summary>
        private static readonly Lazy<TraceSourceLogger> s_staticLogger = new Lazy<TraceSourceLogger>(() =>
        {
            return new TraceSourceLogger(EnvUtils.GetNewTraceSource(nameof(MsalCacheHelper) + "Singleton"));
        });

        /// <summary>
        /// The name of the Default KeyRing collection. Secrets stored in this collection are persisted to disk
        /// </summary>
        public const string LinuxKeyRingDefaultCollection = "default";

        /// <summary>
        /// The name of the Session KeyRing collection. Secrets stored in this collection are not persisted to disk, but
        /// will be avaiable for the duration of the user session.
        /// </summary>
        public const string LinuxKeyRingSessionCollection = "session";

        /// <summary>
        /// Initializes a new instance of the <see cref="MsalCacheStorage"/> class.
        /// The actual cache reading and writing is OS specific:
        /// <list type="bullet">
        /// <item>
        ///     <term>Windows</term>
        ///     <description>DPAPI encyrpted file on behalf of the user. </description>
        /// </item>
        /// <item>
        ///     <term>Mac</term>
        ///     <description>Cache is stored in KeyChain.  </description>
        /// </item>
        /// <item>
        ///     <term>Linux</term>
        ///     <description>Cache is stored in Gnone KeyRing - https://developer.gnome.org/libsecret/0.18/  </description>
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="creationProperties">Properties for creating the cache storage on disk</param>
        /// <param name="logger">logger</param>
        /// <returns></returns>
        public static MsalCacheStorage Create(StorageCreationProperties creationProperties, TraceSource logger = null)
        {
            TraceSourceLogger actualLogger = logger == null ? s_staticLogger.Value : new TraceSourceLogger(logger);

            ICacheAccessor cacheAccessor;
            if (SharedUtilities.IsWindowsPlatform())
            {
                cacheAccessor = new CacheAccessorWindows(creationProperties.CacheFilePath, actualLogger);
            }
            else if (SharedUtilities.IsMacPlatform())
            {
                cacheAccessor = new CacheAccessorMac(
                    creationProperties.CacheFilePath,
                    creationProperties.MacKeyChainServiceName,
                    creationProperties.MacKeyChainAccountName,
                    actualLogger);
            }
            else if (SharedUtilities.IsLinuxPlatform())
            {
                cacheAccessor = new CacheAccessorLinux(
                   creationProperties,
                   actualLogger);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            return new MsalCacheStorage(creationProperties, cacheAccessor, actualLogger);
        }

        internal /* internal for test, otherwise private */ MsalCacheStorage(
            StorageCreationProperties creationProperties,
            ICacheAccessor cacheAccessor,
            TraceSourceLogger logger)
        {
            CreationProperties = creationProperties;
            _logger = logger;
            _cacheAccessor = cacheAccessor;
            _logger.LogInformation($"Initialized '{nameof(MsalCacheStorage)}'");
        }

        /// <summary>
        /// Gets cache file path
        /// </summary>
        public string CacheFilePath => CreationProperties.CacheFilePath;

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
                data = _cacheAccessor.Read();
                _logger.LogInformation($"Got '{data?.Length}' bytes from file storage");
            }
            catch (Exception e)
            {
                _logger.LogError($"An exception was encountered while reading data from the {nameof(MsalCacheStorage)} : {e}");
                _cacheAccessor.Clear();
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
                _cacheAccessor.Write(data);
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
            _cacheAccessor.Clear();
        }
    }
}
