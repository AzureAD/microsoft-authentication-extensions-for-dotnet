// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Microsoft.Identity.Client.Extensions.Adal
{
    /// <summary>
    /// Override Adal token cache
    /// </summary>
    public sealed class AdalCache : TokenCache
    {
        /// <summary>
        /// A default logger for use if the user doesn't want to provide their own.
        /// </summary>
        private static readonly Lazy<TraceSource> s_staticLogger = new Lazy<TraceSource>(() =>
        {
            return (TraceSource)EnvUtils.GetNewTraceSource(nameof(AdalCache) + "Singleton");
        });

        /// <summary>
        /// Storage that handles the storing of the adal cache file on disk.
        /// </summary>
        private readonly AdalCacheStorage _store;

        /// <summary>
        /// Logger to log events to.
        /// </summary>
        private readonly TraceSource _logger;
        private CrossPlatLock _cacheLock;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdalCache"/> class.
        /// </summary>
        /// <param name="storage">Adal cache storage</param>
        /// <param name="logger">Logger</param>
        public AdalCache(AdalCacheStorage storage, TraceSource logger)
        {
            _logger = logger ?? s_staticLogger.Value;
            _store = storage ?? throw new ArgumentNullException(nameof(storage));

            AfterAccess = AfterAccessNotification;
            BeforeAccess = BeforeAccessNotification;

            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Initializing adal cache");

            byte[] data = _store.ReadData();

            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Read '{data?.Length}' bytes from storage");

            if (data != null && data.Length > 0)
            {
                try
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Deserializing data into memory");
                    DeserializeAdalV3(data);
                }
                catch (Exception e)
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"An exception was encountered while deserializing the data during initialization of {nameof(AdalCache)} : {e}");
                    DeserializeAdalV3(null);
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

            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Acquiring lock for token cache");
            _cacheLock = new CrossPlatLock(Path.Combine(_store.CreationProperties.CacheDirectory, _store.CreationProperties.CacheFileName) + ".lockfile");

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
                        DeserializeAdalV3(fileData);
                    }
                    catch (Exception e)
                    {
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An exception was encountered while deserializing the {nameof(AdalCache)} : {e}");
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                        // Clear the memory cache
                        DeserializeAdalV3(null);
                        _store.Clear();
                        throw;
                    }
                }
                else
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                    // Clear the memory cache
                    DeserializeAdalV3(null);
                }
            }
        }

        // Triggered right after ADAL accessed the cache.
        private void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After access");

            try
            {
                // if the access operation resulted in a cache update
                if (HasStateChanged)
                {
                    _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After access, cache in memory HasChanged");
                    try
                    {
                        _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before Write Store");
                        byte[] data = SerializeAdalV3();
                        _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Serializing '{data.Length}' bytes");
                        _store.WriteData(data);

                        _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After write store");
                        HasStateChanged = false;
                    }
                    catch (Exception e)
                    {
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"An exception was encountered while serializing the {nameof(AdalCache)} : {e}");
                        _logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                        // The cache is corrupt clear it out
                        DeserializeAdalV3(null);
                        _store.Clear();
                        throw;
                    }
                }
            }
            finally
            {
                _logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Releasing lock");
                _cacheLock?.Dispose();
                _cacheLock = null;
            }
        }
    }
}
