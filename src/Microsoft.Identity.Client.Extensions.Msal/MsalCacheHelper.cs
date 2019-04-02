// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Identity.Client;

namespace Microsoft.Identity.Client.Extensions.Msal
{
    /// <summary>
    /// Helper to create the token cache
    /// </summary>
    public class MsalCacheHelper
    {
        /// <summary>
        /// A default logger for use if the user doesn't want to provide their own.
        /// </summary>
        private static readonly Lazy<TraceSource> s_staticLogger = new Lazy<TraceSource>(() =>
        {
            return (TraceSource)EnvUtils.GetNewTraceSource(nameof(MsalCacheHelper) + "Singleton");
        });

        /// <summary>
        /// A lock object for serialization
        /// </summary>
        private readonly object _lockObject = new object();

        /// <summary>
        /// Properties used to create storage on disk.
        /// </summary>
        private readonly StorageCreationProperties _storageCreationProperties;

        /// <summary>
        /// Holds a lock object when this helper is accessing the cache. Null otherwise.
        /// </summary>
        internal CrossPlatLock CacheLock { get; private set; }

        /// <summary>
        /// Storage that handles the storing of the adal cache file on disk.
        /// </summary>
        private readonly MsalCacheStorage _store;

        /// <summary>
        /// Logger to log events to.
        /// </summary>
        private readonly TraceSource _logger;

        /// <summary>
        /// Gets the token cache
        /// </summary>
        private ITokenCache _userTokenCache;

        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        /// <param name="storageCreationProperties">Properties to use when creating storage on disk.</param>
        /// <param name="logger">Passing null uses the default logger</param>
        public MsalCacheHelper(StorageCreationProperties storageCreationProperties, TraceSource logger = null)
        {
            _logger = logger ?? s_staticLogger.Value;
            _storageCreationProperties = storageCreationProperties;
            _store = new MsalCacheStorage(_storageCreationProperties, logger);

        }

        /// <summary>
        /// An internal constructor allowing unit tests to data explicitly rather than initializing here.
        /// </summary>
        /// <param name="userTokenCache">The token cache to synchronize with the backing store</param>
        /// <param name="store">The backing store to use.</param>
        /// <param name="logger">Passing null uses the default logger</param>
        internal MsalCacheHelper(ITokenCache userTokenCache, MsalCacheStorage store, TraceSource logger = null)
        {
            _logger = logger ?? s_staticLogger.Value;
            _store = store;
            _storageCreationProperties = store._creationProperties;

            RegisterCache(userTokenCache);
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
        /// Gets a singleton instance of the TokenCacheHelper
        /// </summary>
        /// <param name="tokenCache">Token Cache</param>
        /// <returns>token cache helper</returns>
        public void RegisterCache(ITokenCache tokenCache)
        {
            lock (_lockObject)
            {
                _userTokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));

                _userTokenCache.SetBeforeAccess(BeforeAccessNotification);
                _userTokenCache.SetAfterAccess(AfterAccessNotification);

                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Initializing msal cache");

                byte[] data = _store.ReadData();

                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Read '{data?.Length}' bytes from storage");

                if (data != null && data.Length > 0)
                {
                    try
                    {
                        _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Deserializing data into memory");
                        _userTokenCache.DeserializeMsalV3(data);
                    }
                    catch (Exception e)
                    {
                        _logger.TraceEvent(TraceEventType.Warning, /*id*/ 0, $"An exception was encountered while deserializing the data during initialization of {nameof(MsalCacheHelper)} : {e}");

                        Clear();
                    }
                }

                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Done initializing");
            }
        }

        /// <summary>
        /// Clears the token store
        /// </summary>
        public void Clear()
        {
            _store.Clear();
        }

        /// <summary>
        /// Before cache access
        /// </summary>
        /// <param name="args">Callback parameters from MSAL</param>
        internal void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before access");

            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Acquiring lock for token cache");
            CacheLock = new CrossPlatLock(
                Path.GetFileNameWithoutExtension(_storageCreationProperties.CacheFileName),
                Path.Combine(_storageCreationProperties.CacheDirectory, _storageCreationProperties.CacheFileName) + ".lockfile");

            if (_store.HasChanged)
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before access, the store has changed");
                byte[] fileData = _store.ReadData();
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Read '{fileData?.Length}' bytes from storage");

                if (fileData != null && fileData.Length > 0)
                {
                    try
                    {
                        _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Deserializing the store");
                        args.TokenCache.DeserializeMsalV3(fileData);
                    }
                    catch (Exception e)
                    {
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An exception was encountered while deserializing the {nameof(MsalCacheHelper)} : {e}");
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                        // Clear the memory cache
                        Clear();
                        throw;
                    }
                }
                else
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                    // Clear the memory cache
                }
            }
        }

        /// <summary>
        /// After cache access
        /// </summary>
        /// <param name="args">Callback parameters from MSAL</param>
        internal void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After access");

            try
            {
                // if the access operation resulted in a cache update
                if (args.HasStateChanged)
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After access, cache in memory HasChanged");
                    try
                    {
                        _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before Write Store");
                        byte[] data = args.TokenCache.SerializeMsalV3();
                        _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Serializing '{data.Length}' bytes");
                        _store.WriteData(data);

                        _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After write store");
                    }
                    catch (Exception e)
                    {
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An exception was encountered while serializing the {nameof(MsalCacheHelper)} : {e}");
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                        // The cache is corrupt clear it out
                        Clear();
                        throw;
                    }
                }
            }
            finally
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Releasing lock");
                CacheLock?.Dispose();
                CacheLock = null;
            }
        }
    }
}
