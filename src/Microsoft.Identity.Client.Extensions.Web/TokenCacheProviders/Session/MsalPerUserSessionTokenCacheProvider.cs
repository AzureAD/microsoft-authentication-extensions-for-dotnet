// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Identity.Client.Extensions.Web.TokenCacheProviders.Session
{
    /// <summary>
    /// This is a MSAL's TokenCache implementation for one user. It uses Http session as a backend store
    /// </summary>
    public class MsalPerUserSessionTokenCacheProvider : IMsalUserTokenCacheProvider
    {
        private static readonly ReaderWriterLockSlim s_sessionLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        /// <summary>
        /// Once the user signes in, this will not be null and can be ontained via a call to ClaimsPrincipal.Current
        /// </summary>
        internal ClaimsPrincipal _signedInUser;

        /// <summary>
        /// The HTTP context being used by this app
        /// </summary>
        internal HttpContext _httpContext = null;

        /// <summary>
        /// The internal handle to the client's instance of the Cache
        /// </summary>
        private ITokenCache _userTokenCache;

        /// <summary>Initializes a new instance of the <see cref="MsalPerUserSessionTokenCacheProvider"/> class.</summary>
        public MsalPerUserSessionTokenCacheProvider()
        {
        }

        /// <summary>Initializes the cache instance</summary>
        /// <param name="tokenCache">The ITokenCache passed through the constructor</param>
        /// <param name="httpcontext">The current HttpContext</param>
        /// <param name="user">The signed in user's ClaimPrincipal, could be null.
        /// If the calling app has it available, then it should pass it themselves.</param>
        public void Initialize(ITokenCache tokenCache, HttpContext httpcontext, ClaimsPrincipal user)
        {
            _httpContext = httpcontext;

            _userTokenCache = tokenCache;

            _userTokenCache.SetBeforeAccess(UserTokenCacheBeforeAccessNotification);
            _userTokenCache.SetAfterAccess(UserTokenCacheAfterAccessNotification);
            _userTokenCache.SetBeforeWrite(UserTokenCacheBeforeWriteNotification);

            if (user == null)
            {
                // No users signed in yet, so we return
                return;
            }

            _signedInUser = user;
            LoadUserTokenCacheFromSession();
        }

        /// <summary>
        /// Loads the user token cache from http session.
        /// </summary>
        private void LoadUserTokenCacheFromSession()
        {
            _httpContext.Session.LoadAsync().Wait();

            string cacheKey = GetSignedInUsersUniqueId();

            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            s_sessionLock.EnterReadLock();
            try
            {
                byte[] blob;
                if (_httpContext.Session.TryGetValue(cacheKey, out blob))
                {
                    Debug.WriteLine($"INFO: Deserializing session {_httpContext.Session.Id}, cacheId {cacheKey}");
                    _userTokenCache.DeserializeMsalV3(blob);
                }
                else
                {
                    Debug.WriteLine($"INFO: cacheId {cacheKey} not found in session {_httpContext.Session.Id}");
                }
            }
            finally
            {
                s_sessionLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Persists the user token blob to the Http session.
        /// </summary>
        private void PersistUserTokenCache()
        {
            string cacheKey = GetSignedInUsersUniqueId();

            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            s_sessionLock.EnterWriteLock();

            try
            {
                Debug.WriteLine($"INFO: Serializing session {_httpContext.Session.Id}, cacheId {cacheKey}");

                // Reflect changes in the persistent store
                byte[] blob = _userTokenCache.SerializeMsalV3();
                _httpContext.Session.Set(cacheKey, blob);
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
            string cacheKey = GetSignedInUsersUniqueId();

            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            s_sessionLock.EnterWriteLock();

            try
            {
                Debug.WriteLine($"INFO: Clearing session {_httpContext.Session.Id}, cacheId {cacheKey}");

                // Reflect changes in the persistent store
                _httpContext.Session.Remove(cacheKey);
                _httpContext.Session.CommitAsync().Wait();
            }
            finally
            {
                s_sessionLock.ExitWriteLock();
            }

            // Nulls the currently deserialized instance
            LoadUserTokenCacheFromSession();
        }

        /// <summary>
        /// if you want to ensure that no concurrent write take place, use this notification to place a lock on the entry
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>

        private void UserTokenCacheBeforeWriteNotification(TokenCacheNotificationArgs args)
        {
            // Since we obtain and release lock right before and after we read the Http session, we need not do anything here.
        }

        /// <summary>
        /// Triggered right after MSAL accessed the cache.
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private void UserTokenCacheAfterAccessNotification(TokenCacheNotificationArgs args)
        {
            SetSignedInUserFromNotificationArgs(args);

            // if the access operation resulted in a cache update
            if (args.HasStateChanged)
            {
                PersistUserTokenCache();
            }
        }

        /// <summary>
        /// To keep the cache, ClaimsPrincipal and Sql in sync, we ensure that the user's object Id we obtained by MSAL after
        /// successful sign-in is set as the key for the cache.
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private void SetSignedInUserFromNotificationArgs(TokenCacheNotificationArgs args)
        {
            if (_signedInUser == null && args.Account != null)
            {
                _signedInUser = args.Account.ToClaimsPrincipal();
            }
        }

        /// <summary>
        /// Triggered right before MSAL needs to access the cache. Reload the cache from the persistence store in case it changed since the last access.
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private void UserTokenCacheBeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            LoadUserTokenCacheFromSession();
        }

        /// <summary>
        /// Explores the Claims of a signed-in user (if available) to populate the unique Id of this cache's instance.
        /// </summary>
        /// <returns>The signed in user's object.tenant Id , if available in the ClaimsPrincipal.Current instance</returns>
        internal string GetSignedInUsersUniqueId()
        {
            if (_signedInUser != null)
            {
                return _signedInUser.GetMsalAccountId();
            }
            return null;
        }
    }
}
