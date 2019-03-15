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
    internal sealed class IdentityServiceAdalCache : TokenCache
    {
        /// <summary>
        /// Storage that handles the storing of the adal cache file on disk.
        /// </summary>
        private readonly IdentityServiceAdalCacheStorage _store;

        /// <summary>
        /// Logger to log events to.
        /// </summary>
        private readonly TraceSource _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="IdentityServiceAdalCache"/> class.
        /// </summary>
        /// <param name="storage">Adal cache storage</param>
        /// <param name="logger">Logger</param>
        internal IdentityServiceAdalCache(IdentityServiceAdalCacheStorage storage, TraceSource logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            this.AfterAccess = this.AfterAccessNotification;
            this.BeforeAccess = this.BeforeAccessNotification;

            this._logger = logger;
            this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Initializing adal cache");
            this._store = storage;

            byte[] data = this._store.ReadData();

            this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Read '{data?.Length}' bytes from storage"));

            if (data != null && data.Length > 0)
            {
                try
                {
                    this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Deserializing data into memory");
                    this.DeserializeMsalV3(data);
                }
                catch (Exception e)
                {
                    this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while deserializing the data during initialization of {nameof(IdentityServiceAdalCache)} : {e}"));
                    this.DeserializeMsalV3(null);
                    this._store.Clear();
                }
            }

            this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Done initializing");
        }

        // Triggered right before ADAL needs to access the cache.
        // Reload the cache from the persistent store in case it changed since the last access.
        private void BeforeAccessNotification(TokenCacheNotificationArgs args)
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
                        this.DeserializeMsalV3(fileData);
                    }
                    catch (Exception e)
                    {
                        this._logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while deserializing the {nameof(IdentityServiceAdalCache)} : {e}"));
                        this._logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                        // Clear the memory cache
                        this.DeserializeMsalV3(null);
                        this._store.Clear();
                        throw;
                    }
                }
                else
                {
                    this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                    // Clear the memory cache
                    this.DeserializeMsalV3(null);
                }
            }
        }

        // Triggered right after ADAL accessed the cache.
        private void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After access");

            // if the access operation resulted in a cache update
            if (this.HasStateChanged)
            {
                this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After access, cache in memory HasChanged");
                try
                {
                    this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"Before Write Store");
                    byte[] data = this.SerializeMsalV3();
                    this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, FormattableString.Invariant($"Serializing '{data.Length}' bytes"));
                    this._store.WriteData(data);

                    this._logger.TraceEvent(TraceEventType.Information, /*id*/ 0, $"After write store");
                    this.HasStateChanged = false;
                }
                catch (Exception e)
                {
                    this._logger.TraceEvent(TraceEventType.Error, /*id*/ 0, FormattableString.Invariant($"An exception was encountered while serializing the {nameof(IdentityServiceAdalCache)} : {e}"));
                    this._logger.TraceEvent(TraceEventType.Error, /*id*/ 0, $"No data found in the store, clearing the cache in memory.");

                    // The cache is corrupt clear it out
                    this.DeserializeMsalV3(null);
                    this._store.Clear();
                    throw;
                }
            }
        }
    }
}
