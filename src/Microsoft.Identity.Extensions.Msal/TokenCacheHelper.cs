// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using Microsoft.Identity.Client;

namespace Microsoft.Identity.Extensions.Msal
{
    /// <summary>
    /// helper to create the token cache
    /// </summary>
    internal class TokenCacheHelper
    {
        /// <summary>
        /// A lock object for serialization
        /// </summary>
        private static readonly object s_lockObject = new object();

        private static readonly Lazy<TraceSource> s_staticLogger = new Lazy<TraceSource>(() =>
        {
            return (TraceSource)EnvUtils.GetNewTraceSource(nameof(TokenCacheHelper) + "Singleton");

        });

        private static readonly Lazy<IdentityServiceTokenCacheStorage> s_staticStore = new Lazy<IdentityServiceTokenCacheStorage>(() =>
        {
            return new IdentityServiceTokenCacheStorage(cacheFilePath: null, instanceName: null, logger: s_staticLogger.Value);
        });

        /// <summary>
        /// Storage that handles the storing of the adal cache file on disk.
        /// </summary>
        private readonly IdentityServiceTokenCacheStorage _store;

        /// <summary>
        /// Logger to log events to.
        /// </summary>
        private readonly TraceSource _logger;

        /// <summary>
        /// Gets the token cache
        /// </summary>
        private readonly ITokenCache _userTokenCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenCacheHelper"/> class.
        ///
        /// FOR TESTING ONLY!
        /// </summary>
        /// <param name="tokenCache">token cache</param>
        /// <param name="storage">Adal cache storage</param>
        /// <param name="logger">Logger</param>
        internal TokenCacheHelper(ITokenCache tokenCache, IdentityServiceTokenCacheStorage storage, TraceSource logger)
        {
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._store = storage ?? throw new ArgumentNullException(nameof(storage));
            this._userTokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));

            this._userTokenCache.SetBeforeAccess(this.BeforeAccessNotification);
            this._userTokenCache.SetAfterAccess(this.AfterAccessNotification);

            this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Initializing adal cache");

            byte[] data = this._store.ReadData();

            this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Read '{data?.Length}' bytes from storage"));

            if (data != null && data.Length > 0)
            {
                try
                {
                    this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Deserializing data into memory");
                    this._userTokenCache.DeserializeMsalV3(data);
                }
                catch (Exception e)
                {
                    this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while deserializing the data during initialization of {nameof(TokenCacheHelper)} : {e}"));
                    this._store.Clear();
                }
            }

            this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Done initializing");
        }

        /// <summary>
        /// Gets a singleton instance of the TokenCacheHelper
        /// </summary>
        /// <param name="tokenCache">Token Cache</param>
        /// <returns>token cache helper</returns>
        public static TokenCacheHelper RegisterCache(ITokenCache tokenCache)
        {
            lock (s_lockObject)
            {
                try
                {
                    s_staticLogger.Value.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Creating '{nameof(RegisterCache)}'"));
                    return new TokenCacheHelper(tokenCache, s_staticStore.Value, s_staticLogger.Value);
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
        public void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before access");

            if (this._store.HasChanged)
            {
                this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before access, the store has changed");
                byte[] fileData = this._store.ReadData();
                this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Read '{fileData?.Length}' bytes from storage"));

                if (fileData != null && fileData.Length > 0)
                {
                    try
                    {
                        this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Deserializing the store");
                        args.TokenCache.DeserializeMsalV3(fileData);
                    }
                    catch (Exception e)
                    {
                        this._logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while deserializing the {nameof(TokenCacheHelper)} : {e}"));
                        this._logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                        // Clear the memory cache
                        this._store.Clear();
                        throw;
                    }
                }
                else
                {
                    this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                    // Clear the memory cache
                }
            }
        }

        /// <summary>
        /// After cache access
        /// </summary>
        /// <param name="args">Callback parameters from MSAL</param>
        public void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After access");

            // if the access operation resulted in a cache update
            if (args.HasStateChanged)
            {
                this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After access, cache in memory HasChanged");
                try
                {
                    this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before Write Store");
                    byte[] data = args.TokenCache.SerializeMsalV3();
                    this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Serializing '{data.Length}' bytes"));
                    this._store.WriteData(data);

                    this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After write store");
                }
                catch (Exception e)
                {
                    this._logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while serializing the {nameof(TokenCacheHelper)} : {e}"));
                    this._logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                    // The cache is corrupt clear it out
                    this._store.Clear();
                    throw;
                }
            }
        }
    }
}
