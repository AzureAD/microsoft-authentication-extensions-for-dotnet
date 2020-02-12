// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace ManualTestApp
{

    class Program
    {

#pragma warning disable UseAsyncSuffix // Use Async suffix
        static async Task Main(string[] args)
#pragma warning restore UseAsyncSuffix // Use Async suffix
        {

            var pca = await CreatePublicClientWithCacheAsync().ConfigureAwait(false);
            AuthenticationResult result;

            while (true)
            {

                //await DisplayAccountsAsync(pca).ConfigureAwait(false);
                // Display menu
                Console.WriteLine($@"
                        1. Acquire Token using Username and Password - for TEST only, do not use in production!
                        2. Acquire Token using Device Code Flow
                        3. Acquire Token Interactive
                        4. Acquire Token Silent
                        5. Display Accounts (reads the cache)
                        6. Acquire Token U/P and Silent in a loop
                        c. Clear cache
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

                        while (true)
                        {
                            Console.WriteLine("Acquiring token by ROPC...");
                            result = await AcquireTokenROPCAsync(pca).ConfigureAwait(false);
                            Console.WriteLine("OK. Now getting the accounts");

                            var accounts3 = await pca.GetAccountsAsync().ConfigureAwait(false);

                            Console.WriteLine("Acquiring token silent");

                            result = await pca.AcquireTokenSilent(Config.Scopes, accounts3.First())
                                .ExecuteAsync()
                                .ConfigureAwait(false);

                            Console.WriteLine("Deleting the account");
                            foreach (var acc in accounts3)
                            {
                                await pca.RemoveAsync(acc).ConfigureAwait(false);
                            }

                            Console.WriteLine("Waiting for 3 seconds");
                            await Task.Delay(3000).ConfigureAwait(false);
                        }

                    case 'c':
                        var accounts4 = await pca.GetAccountsAsync().ConfigureAwait(false);
                        foreach (var acc in accounts4)
                        {
                            Console.WriteLine($"Removing account for {acc.Username}");
                            await pca.RemoveAsync(acc).ConfigureAwait(false);
                        }
                        Console.Clear();

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

        private static async Task<AuthenticationResult> AcquireTokenROPCAsync(IPublicClientApplication pca)
        {
            if (string.IsNullOrEmpty(Config.Username) ||
                string.IsNullOrEmpty(Config.Password))
            {
                throw new InvalidOperationException("Please configure a username and password!");
            }

            SecureString securePassword = new SecureString();
            foreach (char c in Config.Password)
            {
                securePassword.AppendChar(c);
            }

            var result = await pca.AcquireTokenByUsernamePassword(
                Config.Scopes,
                Config.Username,
                securePassword)
                .ExecuteAsync()
                .ConfigureAwait(false);

            return result;
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


        private static async Task<IPublicClientApplication> CreatePublicClientWithCacheAsync()
        {
            var storageProperties =
                 new StorageCreationPropertiesBuilder(Config.CacheFileName, Config.CacheDir, Config.ClientId)
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

            IPublicClientApplication pca = await CreatePublicClientAndBindCacheAsync(
                Config.Authority,
                Config.ClientId,
                storageProperties)
                .ConfigureAwait(false);

            return pca;
        }

        private static async Task<IPublicClientApplication> CreatePublicClientAndBindCacheAsync(
            string authority,
            string clientId,
            StorageCreationProperties storageCreationProperties)
        {

            var appBuilder = PublicClientApplicationBuilder.Create(clientId)
                .WithAuthority(authority)
                .WithRedirectUri("http://localhost"); // make sure to register this redirect URI for the interactive login to work

            var app = appBuilder.Build();
            Console.WriteLine($"Built public client");

            // This hooks up the cross-platform cache into  MSAL
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageCreationProperties).ConfigureAwait(false);

            CheckPersistence(cacheHelper);

            cacheHelper.RegisterCache(app.UserTokenCache);

            Console.WriteLine($"Cache registered");

            // The token cache helper's main feature is this event
            cacheHelper.CacheChanged += (s, e) =>
            {
                Console.BackgroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"Cache Changed, Added: {e.AccountsAdded.Count()} Removed: {e.AccountsRemoved.Count()}");
                Console.ResetColor();
            };

            Console.WriteLine($"Cache event wired up");

            return app;
        }

        private static void CheckPersistence(MsalCacheHelper cacheHelper)
        {
            try
            {
                cacheHelper.VerifyPersistence();
            }
            catch (MsalCachePersistenceException ex)
            {
                Console.WriteLine("WARNING: Cannot persist the token cache. Tokens will be held in memory only.");
                Console.WriteLine($"Detailed error:  {ex}");
            }
        }
    }
}
