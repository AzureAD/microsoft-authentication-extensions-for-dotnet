// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using Microsoft;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Microsoft.Identity.Extensions.Adal
{
    /// <summary>
    /// Override Adal token cache
    /// </summary>
    public sealed class AdalCache : TokenCache
    {
        /// <summary>
        /// Storage that handles the storing of the adal cache file on disk.
        /// </summary>
        private readonly AdalCacheStorage _store;

        /// <summary>
        /// Logger to log events to.
        /// </summary>
        private readonly TraceSource _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdalCache"/> class.
        /// </summary>
        /// <param name="storage">Adal cache storage</param>
        /// <param name="logger">Logger</param>
        internal AdalCache(AdalCacheStorage storage, TraceSource logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            AfterAccess = AfterAccessNotification;
            BeforeAccess = BeforeAccessNotification;

            _logger = logger;
            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Initializing adal cache");
            _store = storage;

            byte[] data = _store.ReadData();

            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Read '{data?.Length}' bytes from storage"));

            if (data != null && data.Length > 0)
            {
                try
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Deserializing data into memory");
                    DeserializeMsalV3(data);
                }
                catch (Exception e)
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while deserializing the data during initialization of {nameof(AdalCache)} : {e}"));
                    DeserializeMsalV3(null);
                    _store.Clear();
                }
            }

            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Done initializing");
        }

        // Triggered right before ADAL needs to access the cache.
        // Reload the cache from the persistent store in case it changed since the last access.
        private void BeforeAccessNotification(TokenCacheNotificationArgs args)
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
                        DeserializeMsalV3(fileData);
                    }
                    catch (Exception e)
                    {
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while deserializing the {nameof(AdalCache)} : {e}"));
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                        // Clear the memory cache
                        DeserializeMsalV3(null);
                        _store.Clear();
                        throw;
                    }
                }
                else
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                    // Clear the memory cache
                    DeserializeMsalV3(null);
                }
            }
        }

        // Triggered right after ADAL accessed the cache.
        private void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After access");

            // if the access operation resulted in a cache update
            if (HasStateChanged)
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After access, cache in memory HasChanged");
                try
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before Write Store");
                    byte[] data = SerializeMsalV3();
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Serializing '{data.Length}' bytes"));
                    _store.WriteData(data);

                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After write store");
                    HasStateChanged = false;
                }
                catch (Exception e)
                {
                    _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while serializing the {nameof(AdalCache)} : {e}"));
                    _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                    // The cache is corrupt clear it out
                    DeserializeMsalV3(null);
                    _store.Clear();
                    throw;
                }
            }
        }
    }
}
