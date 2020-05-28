// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensibility;
#if NETCOREAPP
using Microsoft.Identity.Client.Extensions.Browsers;
#endif
using Microsoft.Identity.Client.Extensions.Msal;

namespace ManualTestApp
{

    class Program
    {
#pragma warning disable UseAsyncSuffix // Use Async suffix
        static async Task Main(string[] args)
#pragma warning restore UseAsyncSuffix // Use Async suffix
        {
            // It's recommended to create a separate PublicClient Application for each tenant
            // but only one CacheHelper object
            var pca = CreatePublicClient("https://login.microsoftonline.com/organizations");
            var cacheHelper = await CreateCacheHelperAsync().ConfigureAwait(false);
            cacheHelper.RegisterCache(pca.UserTokenCache);

            // The token cache helper's main feature is this event
            cacheHelper.CacheChanged += (s, e) =>
            {
                Console.BackgroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"Cache Changed, Added: {e.AccountsAdded.Count()} Removed: {e.AccountsRemoved.Count()}");
                Console.ResetColor();
            };

            AuthenticationResult result;

            while (true)
            {

                // Display menu
                Console.WriteLine($@"
                        1. Acquire Token using Username and Password - for TEST only, do not use in production!
                        2. Acquire Token using Device Code Flow
                        3. Acquire Token Interactive
                        4. Acquire Token Silent
                        5. Display Accounts (reads the cache)
                        6. Acquire Token U/P and Silent in a loop
                        7. Acquire Token Interactive with Embedded Browser
                        c. Clear cache
                        e. Expire Access Tokens (TEST only!)
                        x. Exit app
                    Enter your Selection: ");
                char.TryParse(Console.ReadLine(), out var selection);
                try
                {
                    switch (selection)
                    {
                    case '1': //  Acquire Token using Username and Password (requires config)
                        result = await AcquireTokenROPCAsync(pca).ConfigureAwait(false);
                        DisplayResult(result);

                        break;

                    case '2': // Device Code Flow
                        result = await pca.AcquireTokenWithDeviceCode(Config.Scopes, (dcr) =>
                        {
                            Console.BackgroundColor = ConsoleColor.Cyan;
                            Console.WriteLine(dcr.Message);
                            Console.ResetColor();

                            return Task.FromResult(1);
                        }).ExecuteAsync().ConfigureAwait(false);
                        DisplayResult(result);

                        break;
                    case '3': // Interactive
                        result = await pca.AcquireTokenInteractive(Config.Scopes)
                            .ExecuteAsync()
                            .ConfigureAwait(false);
                        DisplayResult(result);
                        break;
                    case '4': // Silent
                        Console.WriteLine("Getting all the accounts. This reads the cache");
                        var accounts = await pca.GetAccountsAsync().ConfigureAwait(false);
                        var firstAccount = accounts.FirstOrDefault();

                        // this is expected to fail when account is null
                        result = await pca.AcquireTokenSilent(Config.Scopes, firstAccount)
                            .ExecuteAsync()
                            .ConfigureAwait(false);
                        DisplayResult(result);
                        break;
                    case '5': // Display Accounts
                        Console.Clear();
                        var accounts2 = await pca.GetAccountsAsync().ConfigureAwait(false);
                        if (!accounts2.Any())
                        {
                            Console.WriteLine("No accounts were found in the cache.");
                        }

                        foreach (var acc in accounts2)
                        {
                            Console.WriteLine($"Account for {acc.Username}");
                        }
                        break;
                    case '6': // U/P and Silent in a loop
                        Console.WriteLine("CTRL-C to stop...");

                        cacheHelper.Clear();

                        // TODO: try with different authorities
                        var pca2 = CreatePublicClient("https://login.microsoftonline.com/organizations");
                        var pca3 = CreatePublicClient("https://login.microsoftonline.com/organizations");
                        cacheHelper.RegisterCache(pca2.UserTokenCache);
                        cacheHelper.RegisterCache(pca3.UserTokenCache);

                        while (true)
                        {
                            Task.WaitAll(
                                RunRopcAndSilentAsync("PCA_1", pca),
                                RunRopcAndSilentAsync("PCA_2", pca2),
                                RunRopcAndSilentAsync("PCA_3", pca3)
                            );

                            await Task.Delay(2000).ConfigureAwait(false);
                        }

                    case '7':
#if NETCOREAPP
                        result = await pca
                            .AcquireTokenInteractive(Config.Scopes)
                            .WithCustomWebUi(new WebBrowserControlWebUi())
                            .ExecuteAsync()
                            .ConfigureAwait(false);
                        DisplayResult(result);
#else
                        Console.WriteLine("This implementation is not available on .net classic. But you can use WithEmbeddedWebUi instead.");
#endif
                        break;
                    case 'c':
                        var accounts4 = await pca.GetAccountsAsync().ConfigureAwait(false);
                        foreach (var acc in accounts4)
                        {
                            Console.WriteLine($"Removing account for {acc.Username}");
                            await pca.RemoveAsync(acc).ConfigureAwait(false);
                        }
                        Console.Clear();

                        break;

                    case 'e':

                        // do smth that loads the cache first
                        await pca.GetAccountsAsync().ConfigureAwait(false);

                        string expiredValue = ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds)
                                .ToString(CultureInfo.InvariantCulture);

                        var accessor = pca.UserTokenCache.GetType()
                            .GetRuntimeProperties()
                            .Single(p => p.Name == "Microsoft.Identity.Client.ITokenCacheInternal.Accessor")
                            .GetValue(pca.UserTokenCache);

                        var internalAccessTokens = accessor.GetType().GetMethod("GetAllAccessTokens").Invoke(accessor, null) as IEnumerable<object>;

                        foreach (var internalAt in internalAccessTokens)
                        {
                            internalAt.GetType().GetRuntimeMethods().Single(m => m.Name == "set_ExpiresOnUnixTimestamp").Invoke(internalAt, new[] { expiredValue });
                            accessor.GetType().GetMethod("SaveAccessToken").Invoke(accessor, new[] { internalAt });
                        }

                        var ctor = typeof(TokenCacheNotificationArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();

                        var argz = ctor.Invoke(new object[] { pca.UserTokenCache, Config.ClientId, null, true, false });
                        var task = pca.UserTokenCache.GetType().GetRuntimeMethods()
                            .Single(m => m.Name == "Microsoft.Identity.Client.ITokenCacheInternal.OnAfterAccessAsync")
                            .Invoke(pca.UserTokenCache, new[] { argz });

                        await (task as Task).ConfigureAwait(false);
                        break;

                    case 'x':
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Exception : " + ex);
                    Console.ResetColor();
                    Console.WriteLine("Hit Enter to continue");

                    Console.Read();
                }
            }
        }

        private static async Task<AuthenticationResult> RunRopcAndSilentAsync(
            string logPrefix,
            IPublicClientApplication pca)
        {
            Console.WriteLine($"{logPrefix} Acquiring token by ROPC...");
            var result = await AcquireTokenROPCAsync(pca).ConfigureAwait(false);

            Console.WriteLine($"{logPrefix} OK. Now getting the accounts");
            var accounts = await pca.GetAccountsAsync().ConfigureAwait(false);

            Console.WriteLine($"{logPrefix} Acquiring token silent");

            result = await pca.AcquireTokenSilent(Config.Scopes, accounts.First())
                .ExecuteAsync()
                .ConfigureAwait(false);

            Console.WriteLine($"{logPrefix} Deleting the account");
            foreach (var acc in accounts)
            {
                await pca.RemoveAsync(acc).ConfigureAwait(false);
            }

            return result;
        }

        private static async Task<AuthenticationResult> AcquireTokenROPCAsync(
            IPublicClientApplication pca)
        {
            if (string.IsNullOrEmpty(Config.Username) ||
                string.IsNullOrEmpty(Config.Password))
            {
                throw new InvalidOperationException("Please configure a username and password!");
            }

            using (SecureString securePassword = new SecureString())
            {
                foreach (char c in Config.Password)
                {
                    securePassword.AppendChar(c);
                }

                return await pca.AcquireTokenByUsernamePassword(
                    Config.Scopes,
                    Config.Username,
                    securePassword)
                    .ExecuteAsync()
                    .ConfigureAwait(false);
            }
        }

        private static void DisplayResult(AuthenticationResult result)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Token Acquisition Success! Got a token for: " + result.Account.Username);
            Console.WriteLine(result.AccessToken);
            Console.ResetColor();
            Console.WriteLine("Press ENTER to continue");
            Console.Read();
            Console.Clear();
        }



        private static async Task<MsalCacheHelper> CreateCacheHelperAsync()
        {

            StorageCreationProperties storageProperties;

            try
            {
                storageProperties = new StorageCreationPropertiesBuilder(
                    Config.CacheFileName,
                    Config.CacheDir,
                    Config.ClientId)
                .WithLinuxKeyring(
                    Config.LinuxKeyRingSchema,
                    Config.LinuxKeyRingCollection,
                    Config.LinuxKeyRingLabel,
                    Config.LinuxKeyRingAttr1,
                    Config.LinuxKeyRingAttr2)
                .WithMacKeyChain(
                    Config.KeyChainServiceName,
                    Config.KeyChainAccountName)
                .Build();

                var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties).ConfigureAwait(false);

                cacheHelper.VerifyPersistence();
                return cacheHelper;

            }
            catch (Exception e)
            {
                Console.WriteLine($"WARNING! Libsecret is not usable. " +
                    $"Secrets will be stored in plaintext at {Path.Combine(Config.CacheDir, Config.CacheFileName)} !");
                Console.WriteLine($"Libsecret exception: " + e);

                storageProperties =
                    new StorageCreationPropertiesBuilder(
                        Config.CacheFileName,
                        Config.CacheDir,
                        Config.ClientId)
                    .WithLinuxUnprotectedFile()
                    .WithMacKeyChain(
                        Config.KeyChainServiceName,
                        Config.KeyChainAccountName)
                     .Build();

                var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties).ConfigureAwait(false);
                cacheHelper.VerifyPersistence();

                return cacheHelper;
            }

        }

        private static IPublicClientApplication CreatePublicClient(string authority)
        {
            var appBuilder = PublicClientApplicationBuilder.Create(Config.ClientId)
                .WithAuthority(authority)
                .WithRedirectUri("http://localhost"); // make sure to register this redirect URI for the interactive login to work

            var app = appBuilder.Build();
            Console.WriteLine($"Built public client");

            return app;
        }

    }
}
