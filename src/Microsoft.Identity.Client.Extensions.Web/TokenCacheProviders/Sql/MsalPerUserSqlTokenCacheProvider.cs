// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Identity.Client.Extensions.Web.TokenCacheProviders.Sql
{
    /// <summary>
    /// This is a MSAL's TokenCache implementation for one user. It uses Sql server as a backend store and uses the Entity Framework to read and write to that database.
    /// </summary>
    /// <remarks>https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/token-cache-serialization</remarks>
    public class MsalPerUserSqlTokenCacheProvider : IMsalUserTokenCacheProvider
    {
        /// <summary>
        /// The EF's DBContext object to be used to read and write from the Sql server database.
        /// </summary>
        private readonly TokenCacheDbContext _tokenCacheDb;

        /// <summary>
        /// This keeps the latest copy of the token in memory to save calls to DB, if possible.
        /// </summary>
        private UserTokenCache _inMemoryCache;

        /// <summary>
        /// The private MSAL's TokenCache instance.
        /// </summary>
        private ITokenCache _userTokenCache;

        /// <summary>
        /// Once the user signes in, this will not be null and can be ontained via a call to Thread.CurrentPrincipal
        /// </summary>
        internal ClaimsPrincipal _signedInUser;

        /// <summary>
        /// The data protector
        /// </summary>
        private readonly IDataProtector _dataProtector;

        /// <summary>Initializes a new instance of the <see cref="MsalPerUserSqlTokenCacheProvider"/> class.</summary>
        /// <param name="protectionProvider">The data protection provider. Requires the caller to have used serviceCollection.AddDataProtection();</param>
        /// <param name="tokenCacheDbContext">The DbContext to the database where tokens will be cached.</param>
        public MsalPerUserSqlTokenCacheProvider(
            TokenCacheDbContext tokenCacheDbContext,
            IDataProtectionProvider protectionProvider)
            : this(tokenCacheDbContext, protectionProvider, ClaimsPrincipal.Current)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MsalPerUserSqlTokenCacheProvider"/> class.</summary>
        /// <param name="tokenCacheDbContext">The token cache database context.</param>
        /// <param name="protectionProvider">The protection provider.</param>
        /// <param name="user">The current user .</param>
        /// <exception cref="ArgumentNullException">protectionProvider - The app token cache needs an {nameof(IDataProtectionProvider)}</exception>
        public MsalPerUserSqlTokenCacheProvider(
            TokenCacheDbContext tokenCacheDbContext,
            IDataProtectionProvider protectionProvider,
            ClaimsPrincipal user)
        {
            if (protectionProvider == null)
            {
                throw new ArgumentNullException(nameof(protectionProvider), $"The app token cache needs an {nameof(IDataProtectionProvider)} to operate. Please use 'serviceCollection.AddDataProtection();' to add the data protection provider to the service collection");
            }

            _dataProtector = protectionProvider.CreateProtector("MSAL");
            _tokenCacheDb = tokenCacheDbContext;
            _signedInUser = user;
        }

        /// <summary>Initializes this instance of TokenCacheProvider with essentials to initialize themselves.</summary>
        /// <param name="tokenCache">The token cache instance of MSAL application</param>
        /// <param name="httpcontext">The Httpcontext whose Session will be used for caching.This is required by some providers.</param>
        /// <param name="user">The signed-in user for whom the cache needs to be established. Not needed by all providers.</param>
        public void Initialize(ITokenCache tokenCache, HttpContext httpcontext, ClaimsPrincipal user)
        {
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
            ReadCacheForSignedInUser();
        }

        /// <summary>
        /// Explores the Claims of a signed-in user (if available) to populate the unique Id of this cache's instance.
        /// </summary>
        /// <returns>The signed in user's object.tenant Id , if available in the ClaimsPrincipal.Current instance</returns>
        private string GetSignedInUsersUniqueId()
        {
            if (_signedInUser != null)
            {
                return _signedInUser.GetMsalAccountId();
            }
            return null;
        }

        /// <summary>
        /// if you want to ensure that no concurrent write take place, use this notification to place a lock on the entry
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private void UserTokenCacheBeforeWriteNotification(TokenCacheNotificationArgs args)
        {
            // Since we are using a Rowversion for concurrency, we need not to do anything in this handler.
        }

        /// <summary>
        /// Right before it reads the cache, a call is made to BeforeAccess notification. Here, you have the opportunity of retrieving your persisted cache blob
        /// from the Sql database. We pick it from the database, save it in the in-memory copy, and pass it to the base class by calling the Deserialize().
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private void UserTokenCacheBeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            ReadCacheForSignedInUser();
        }

        /// <summary>
        /// Raised AFTER MSAL added the new token in its in-memory copy of the cache.
        /// This notification is called every time MSAL accessed the cache, not just when a write took place:
        /// If MSAL's current operation resulted in a cache change, the property TokenCacheNotificationArgs.HasStateChanged will be set to true.
        /// If that is the case, we call the TokenCache.Serialize() to get a binary blob representing the latest cache content – and persist it.
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private void UserTokenCacheAfterAccessNotification(TokenCacheNotificationArgs args)
        {
            SetSignedInUserFromNotificationArgs(args);

            // if state changed, i.e. new token obtained
            if (args.HasStateChanged && !string.IsNullOrWhiteSpace(GetSignedInUsersUniqueId()))
            {
                if (_inMemoryCache == null)
                {
                    _inMemoryCache = new UserTokenCache
                    {
                        WebUserUniqueId = GetSignedInUsersUniqueId()
                    };
                }

                _inMemoryCache.CacheBits = _dataProtector.Protect(_userTokenCache.SerializeMsalV3());
                _inMemoryCache.LastWrite = DateTime.Now;

                try
                {
                    // Update the DB and the lastwrite
                    _tokenCacheDb.Entry(_inMemoryCache).State = _inMemoryCache.UserTokenCacheId == 0
                        ? EntityState.Added
                        : EntityState.Modified;

                    _tokenCacheDb.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Record already updated on a different thread, so just read the updated record
                    ReadCacheForSignedInUser();
                }
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
        /// Reads the cache data from the backend database.
        /// </summary>
        private void ReadCacheForSignedInUser()
        {
            if (_inMemoryCache == null) // first time access
            {
                _inMemoryCache = GetLatestUserRecordQuery().FirstOrDefault();
            }
            else
            {
                // retrieve last written record from the DB
                var lastwriteInDb = GetLatestUserRecordQuery().Select(n => n.LastWrite).FirstOrDefault();

                // if the persisted copy is newer than the in-memory copy
                if (lastwriteInDb > _inMemoryCache.LastWrite)
                {
                    // read from from storage, update in-memory copy
                    _inMemoryCache = GetLatestUserRecordQuery().FirstOrDefault();
                }
            }

            // Send data to the TokenCache instance
            _userTokenCache.DeserializeMsalV3((_inMemoryCache == null) ? null : _dataProtector.Unprotect(_inMemoryCache.CacheBits));
        }

        /// <summary>
        /// Clears the TokenCache's copy and the database copy of this user's cache.
        /// </summary>
        public void Clear()
        {
            // Delete from DB
            var cacheEntries = _tokenCacheDb.UserTokenCache.Where(c => c.WebUserUniqueId == GetSignedInUsersUniqueId());
            _tokenCacheDb.UserTokenCache.RemoveRange(cacheEntries);
            _tokenCacheDb.SaveChanges();

            // Nulls the currently deserialized instance
            ReadCacheForSignedInUser();
        }

        private IOrderedQueryable<UserTokenCache> GetLatestUserRecordQuery()
        {
            return _tokenCacheDb.UserTokenCache.Where(c => c.WebUserUniqueId == GetSignedInUsersUniqueId()).OrderByDescending(d => d.LastWrite);
        }
    }

    /// <summary>
    /// Represents a user's token cache entry in database
    /// </summary>
    public class UserTokenCache
    {
        /// <summary>
        /// 
        /// </summary>
        [Key]
        public int UserTokenCacheId { get; set; }

        /// <summary>
        /// The objectId of the signed-in user's object in Azure AD
        /// </summary>
        public string WebUserUniqueId { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public byte[] CacheBits { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public DateTime LastWrite { get; set; }

        /// <summary>
        /// Provided here as a precaution against concurrent updates by multiple threads.
        /// </summary>
        [Timestamp]
        public byte[] RowVersion { get; set; }
    }
}
