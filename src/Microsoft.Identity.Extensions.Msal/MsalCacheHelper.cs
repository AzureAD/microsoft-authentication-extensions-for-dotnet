// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using Microsoft.Identity.Client;

namespace Microsoft.Identity.Extensions.Msal
{
    /// <summary>
    /// Helper to create the token cache
    /// </summary>
    public class MsalCacheHelper
    {
        /// <summary>
        /// A lock object for serialization
        /// </summary>
        private static readonly object s_lockObject = new object();

        private static readonly Lazy<TraceSource> s_staticLogger = new Lazy<TraceSource>(() =>
        {
            return (TraceSource)EnvUtils.GetNewTraceSource(nameof(MsalCacheHelper) + "Singleton");
        });

        private static readonly Lazy<MsalCacheStorage> s_staticStore = new Lazy<MsalCacheStorage>(() =>
        {
            return new MsalCacheStorage(s_storageCreationProperties, cacheFilePath: null, instanceName: null, logger: s_staticLogger.Value);
        });

        private static MsalStorageCreationProperties s_storageCreationProperties;

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
        private readonly ITokenCache _userTokenCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="MsalCacheHelper"/> class.
        ///
        /// FOR TESTING ONLY!
        /// </summary>
        /// <param name="tokenCache">token cache</param>
        /// <param name="storage">Adal cache storage</param>
        /// <param name="logger">Logger</param>
        internal MsalCacheHelper(ITokenCache tokenCache, MsalCacheStorage storage, TraceSource logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _store = storage ?? throw new ArgumentNullException(nameof(storage));
            _userTokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));

            _userTokenCache.SetBeforeAccess(BeforeAccessNotification);
            _userTokenCache.SetAfterAccess(AfterAccessNotification);

            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Initializing adal cache");

            byte[] data = _store.ReadData();

            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Read '{data?.Length}' bytes from storage"));

            if (data != null && data.Length > 0)
            {
                try
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Deserializing data into memory");
                    _userTokenCache.DeserializeMsalV3(data);
                }
                catch (Exception e)
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while deserializing the data during initialization of {nameof(MsalCacheHelper)} : {e}"));
                    _store.Clear();
                }
            }

            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Done initializing");
        }

        /// <summary>
        /// Gets a singleton instance of the TokenCacheHelper
        /// </summary>
        /// <param name="tokenCache">Token Cache</param>
        /// <param name="storageProperties">Properties to use when creating the storage on disk</param>
        /// <returns>token cache helper</returns>
        public static MsalCacheHelper RegisterCache(ITokenCache tokenCache, MsalStorageCreationProperties storageProperties)
        {
            lock (s_lockObject)
            {
                try
                {
                    s_storageCreationProperties = storageProperties;
                    s_staticLogger.Value.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Creating '{nameof(RegisterCache)}'"));
                    return new MsalCacheHelper(tokenCache, s_staticStore.Value, s_staticLogger.Value);
                }
                catch (Exception e)
                when (SharedUtilities.LogExceptionAndDoNotHandler(() => s_staticLogger.Value.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"Problem creating '{nameof(RegisterCache)}' : '{e}'"))))
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Clears the token store
        /// </summary>
        public static void Clear()
        {
            s_staticStore.Value.Clear();
        }

        /// <summary>
        /// Before cache access
        /// </summary>
        /// <param name="args">Callback parameters from MSAL</param>
        internal void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before access");

            if (_store.HasChanged)
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before access, the store has changed");
                byte[] fileData = _store.ReadData();
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Read '{fileData?.Length}' bytes from storage"));

                if (fileData != null && fileData.Length > 0)
                {
                    try
                    {
                        _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Deserializing the store");
                        args.TokenCache.DeserializeMsalV3(fileData);
                    }
                    catch (Exception e)
                    {
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while deserializing the {nameof(MsalCacheHelper)} : {e}"));
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                        // Clear the memory cache
                        _store.Clear();
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

            // if the access operation resulted in a cache update
            if (args.HasStateChanged)
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After access, cache in memory HasChanged");
                try
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before Write Store");
                    byte[] data = args.TokenCache.SerializeMsalV3();
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Serializing '{data.Length}' bytes"));
                    _store.WriteData(data);

                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After write store");
                }
                catch (Exception e)
                {
                    _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while serializing the {nameof(MsalCacheHelper)} : {e}"));
                    _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                    // The cache is corrupt clear it out
                    _store.Clear();
                    throw;
                }
            }
        }
    }
}
