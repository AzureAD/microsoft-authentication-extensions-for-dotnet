// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace Microsoft.Identity.Client.Extensions.Web.TokenCacheProviders.InMemory
{
    /// <summary>
    /// An implementation of token cache for both Confidential and Public clients backed by MemoryCache.
    /// MemoryCache is useful in Api scenarios where there is no HttpContext.Session to cache data.
    /// </summary>
    /// <remarks>https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/token-cache-serialization</remarks>
    public class MsalPerUserMemoryTokenCacheProvider : IMsalUserTokenCacheProvider
    {
        /// <summary>
        /// The backing MemoryCache instance
        /// </summary>
        internal IMemoryCache _memoryCache;

        /// <summary>
        /// The internal handle to the client's instance of the Cache
        /// </summary>
        private ITokenCache _userTokenCache;

        /// <summary>
        /// Once the user signes in, this will not be null and can be ontained via a call to Thread.CurrentPrincipal
        /// </summary>
        internal ClaimsPrincipal _signedInUser;

        private readonly MsalMemoryTokenCacheOptions _cacheOptions;

        /// <summary>Initializes a new instance of the <see cref="MsalPerUserMemoryTokenCacheProvider"/> class.</summary>
        /// <param name="cache">The memory cache instance</param>
        /// <param name="option"></param>
        public MsalPerUserMemoryTokenCacheProvider(IMemoryCache cache, MsalMemoryTokenCacheOptions option)
        {
            _memoryCache = cache;

            if (option != null)
            {
                _cacheOptions = new MsalMemoryTokenCacheOptions();
            }
            else
            {
                _cacheOptions = option;
            }
        }

        /// <summary>Initializes this instance of TokenCacheProvider with essentials to initialize themselves.</summary>
        /// <param name="tokenCache">The token cache instance of MSAL application</param>
        /// <param name="httpcontext">The Httpcontext whose Session will be used for caching.This is required by some providers.</param>
        /// <param name="user">The signed-in user for whom the cache needs to be established. Not needed by all providers.</param>
        public void Initialize(ITokenCache tokenCache, HttpContext httpcontext, ClaimsPrincipal user)
        {
            _signedInUser = user;

            _userTokenCache = tokenCache;
            _userTokenCache.SetBeforeAccess(UserTokenCacheBeforeAccessNotification);
            _userTokenCache.SetAfterAccess(UserTokenCacheAfterAccessNotification);
            _userTokenCache.SetBeforeWrite(UserTokenCacheBeforeWriteNotification);

            if (_signedInUser == null)
            {
                // No users signed in yet, so we return
                return;
            }

            LoadUserTokenCacheFromMemory();
        }

        /// <summary>
        /// Explores the Claims of a signed-in user (if available) to populate the unique Id of this cache's instance.
        /// </summary>
        /// <returns>The signed in user's object.tenant Id , if available in the ClaimsPrincipal.Current instance</returns>
        internal string GetMsalAccountId()
        {
            if (_signedInUser != null)
            {
                return _signedInUser.GetMsalAccountId();
            }
            return null;
        }

        /// <summary>Loads the user token cache from memory.</summary>
        private void LoadUserTokenCacheFromMemory()
        {
            string cacheKey = GetMsalAccountId();

            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            byte[] tokenCacheBytes = (byte[])_memoryCache.Get(GetMsalAccountId());
            _userTokenCache.DeserializeMsalV3(tokenCacheBytes);
        }

        /// <summary>
        /// Persists the user token blob to the memoryCache.
        /// </summary>
        private void PersistUserTokenCache()
        {
            // Ideally, methods that load and persist should be thread safe.MemoryCache.Get() is thread safe.
            _memoryCache.Set(GetMsalAccountId(), _userTokenCache.SerializeMsalV3(), _cacheOptions.AbsoluteExpiration);
        }

        /// <summary>
        /// Clears the TokenCache's copy of this user's cache.
        /// </summary>
        public void Clear()
        {
            _memoryCache.Remove(GetMsalAccountId());

            // Nulls the currently deserialized instance
            LoadUserTokenCacheFromMemory();
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
        /// Triggered right before MSAL needs to access the cache. Reload the cache from the persistence store in case it
        /// changed since the last access.
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private void UserTokenCacheBeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            LoadUserTokenCacheFromMemory();
        }

        /// <summary>
        /// if you want to ensure that no concurrent write take place, use this notification to place a lock on the entry
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private void UserTokenCacheBeforeWriteNotification(TokenCacheNotificationArgs args)
        {
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
    }
}
