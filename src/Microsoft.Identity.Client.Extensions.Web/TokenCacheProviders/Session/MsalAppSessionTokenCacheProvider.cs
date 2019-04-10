// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Microsoft.Identity.Client.Extensions.Web.TokenCacheProviders.Session
{
    /// <summary>
    /// An implementation of token cache for Confidential clients backed by Http session.
    /// </summary>
    /// <remarks>https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/token-cache-serialization</remarks>
    public class MsalAppSessionTokenCacheProvider : IMsalAppTokenCacheProvider
    {
        /// <summary>
        /// The application cache key
        /// </summary>
        internal string _appCacheId;

        /// <summary>
        /// The HTTP context being used by this app
        /// </summary>
        internal HttpContext _httpContext = null;

        /// <summary>
        /// The duration till the tokens are kept in memory cache. In production, a higher value , upto 90 days is recommended.
        /// </summary>
        private readonly DateTimeOffset _cacheDuration = DateTimeOffset.Now.AddHours(12);

        /// <summary>
        /// The internal handle to the client's instance of the Cache
        /// </summary>
        private ITokenCache _apptokenCache;

        private static readonly ReaderWriterLockSlim s_sessionLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        /// <summary>
        /// The App's whose cache we are maintaining.
        /// </summary>
        private readonly string _appId;

        /// <summary>Initializes a new instance of the <see cref="MsalAppSessionTokenCacheProvider"/> class.</summary>
        /// <param name="azureAdOptionsAccessor">The azure ad options accessor.</param>
        /// <exception cref="ArgumentNullException">AzureADOptions - The app token cache needs {nameof(AzureADOptions)}</exception>
        public MsalAppSessionTokenCacheProvider(IOptionsMonitor<AzureADOptions> azureAdOptionsAccessor)
        {
            if (azureAdOptionsAccessor.CurrentValue == null && string.IsNullOrWhiteSpace(azureAdOptionsAccessor.CurrentValue.ClientId))
            {
                throw new ArgumentNullException(nameof(AzureADOptions), $"The app token cache needs {nameof(AzureADOptions)}, populated with clientId to initialize.");
            }

            _appId = azureAdOptionsAccessor.CurrentValue.ClientId;
        }

        /// <summary>Initializes this instance of TokenCacheProvider with essentials to initialize themselves.</summary>
        /// <param name="tokenCache">The token cache instance of MSAL application</param>
        /// <param name="httpcontext">The Httpcontext whose Session will be used for caching.This is required by some providers.</param>
        public void Initialize(ITokenCache tokenCache, HttpContext httpcontext)
        {
            _appCacheId = _appId + "_AppTokenCache";
            _httpContext = httpcontext;

            _apptokenCache = tokenCache;
            _apptokenCache.SetBeforeAccess(AppTokenCacheBeforeAccessNotification);
            _apptokenCache.SetAfterAccess(AppTokenCacheAfterAccessNotification);
            _apptokenCache.SetBeforeWrite(AppTokenCacheBeforeWriteNotification);

            LoadAppTokenCacheFromSession();
        }

        /// <summary>
        /// if you want to ensure that no concurrent write take place, use this notification to place a lock on the entry
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private void AppTokenCacheBeforeWriteNotification(TokenCacheNotificationArgs args)
        {
            // Since we are using a SessionCache ,whose methods are threads safe, we need not to do anything in this handler.
        }

        /// <summary>
        /// Loads the application's tokens from session cache.
        /// </summary>
        private void LoadAppTokenCacheFromSession()
        {
            _httpContext.Session.LoadAsync().Wait();

            s_sessionLock.EnterReadLock();
            try
            {
                byte[] blob;
                if (_httpContext.Session.TryGetValue(_appCacheId, out blob))
                {
                    Debug.WriteLine($"INFO: Deserializing session {_httpContext.Session.Id}, cacheId {_appCacheId}");
                    _apptokenCache.DeserializeMsalV3(blob);
                }
                else
                {
                    Debug.WriteLine($"INFO: cacheId {_appCacheId} not found in session {_httpContext.Session.Id}");
                }
            }
            finally
            {
                s_sessionLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Persists the application token's to session cache.
        /// </summary>
        private void PersistAppTokenCache()
        {
            s_sessionLock.EnterWriteLock();

            try
            {
                Debug.WriteLine($"INFO: Serializing session {_httpContext.Session.Id}, cacheId {_appCacheId}");

                // Reflect changes in the persistent store
                byte[] blob = _apptokenCache.SerializeMsalV3();
                _httpContext.Session.Set(_appCacheId, blob);
                _httpContext.Session.CommitAsync().Wait();
            }
            finally
            {
                s_sessionLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Clears the TokenCache's copy of this user's cache.
        /// </summary>
        public void Clear()
        {
            s_sessionLock.EnterWriteLock();

            try
            {
                Debug.WriteLine($"INFO: Clearing session {_httpContext.Session.Id}, cacheId {_appCacheId}");

                // Reflect changes in the persistent store
                _httpContext.Session.Remove(_appCacheId);
                _httpContext.Session.CommitAsync().Wait();
            }
            finally
            {
                s_sessionLock.ExitWriteLock();
            }

            // Nulls the currently deserialized instance
            LoadAppTokenCacheFromSession();
        }

        /// <summary>
        /// Triggered right before MSAL needs to access the cache. Reload the cache from the persistence store in case it changed since the last access.
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private void AppTokenCacheBeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            LoadAppTokenCacheFromSession();
        }

        /// <summary>
        /// Triggered right after MSAL accessed the cache.
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private void AppTokenCacheAfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if the access operation resulted in a cache update
            if (args.HasStateChanged)
            {
                PersistAppTokenCache();
            }
        }
    }
}
