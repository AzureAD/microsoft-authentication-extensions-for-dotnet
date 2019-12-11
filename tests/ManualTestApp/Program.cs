// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace ManualTestApp
{
    public static class DefaultConfig
    {
        public const string Resource = "https://graph.microsoft.com/";
        public const string TenantId = "49f548d0-12b7-4169-a390-bb5304d24462";
        public static readonly Uri Environment = new Uri("https://login.microsoftonline.com");
        public const bool ValidateAuthority = true;
        public const string ClientId = "1d18b3b0-251b-4714-a02a-9956cec86c2d";
        public const string CacheFileName = "msal.ext.cache";
        public const string CacheDir = "msal_exp";

        public const string KeyChainServiceName = "msal_service";
        public const string KeyChainAccountName = "msal_account";

        public const string LinuxKeyRingSchema = "schema";
        public const string LinuxKeyRingCollection = "collection";
        public const string LinuxKeyRingLabel = "label";
        public static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new KeyValuePair<string, string>("k1", "v1");
        public static readonly KeyValuePair<string, string> LinuxKeyRingAttr2 = new KeyValuePair<string, string>("k2", "v2");
    }

    // Example usage:
    // login-msal https://graph.microsoft.com/ 49f548d0-12b7-4169-a390-bb5304d24462 https://login.microsoftonline.com true 1d18b3b0-251b-4714-a02a-9956cec86c2d msal.ext.cache msal_exp
    class Program
    {
        private static MsalCacheHelper s_helper;

#pragma warning disable UseAsyncSuffix // Use Async suffix
        static async Task Main(string[] args)
#pragma warning restore UseAsyncSuffix // Use Async suffix
        {
            string input;
            do
            {
                Console.Write("> ");
                input = Console.ReadLine();
            } while (await RunCommandAsync(input).ConfigureAwait(true));
        }

        /// <summary>
        /// Executes a user command
        /// </summary>
        /// <param name="splitInput">The command</param>
        /// <returns>True if execution should continue, false if we should terminate.</returns>
        private static async Task<bool> RunCommandAsync(string input)
        {

            var splitInput = input.Split(' ');
            var command = splitInput[0].ToLowerInvariant();
            var args = splitInput.Skip(1).ToArray();

            switch (command)
            {
            case "quit":
            case "exit":
                return false;

            case "help":
                PrintUsage();
                return true;

            case "login-msal":
                if (args.Length < 7)
                {
                    Console.WriteLine("Incorrect format");
                    PrintUsage();
                }
                else
                {
                    await LoginWithCommandLineArgsAsync(args).ConfigureAwait(false);
                }
                return true;

            case "login-msal-defaults":
                await LoginWithDefaultsAsync().ConfigureAwait(false);
                return true;
            default:
                Console.WriteLine("Unknown command");
                PrintUsage();
                return true;
            }
        }

        private static async Task LoginWithDefaultsAsync()
        {
            var storagePropertiesBuilder =
                 new StorageCreationPropertiesBuilder(DefaultConfig.CacheFileName, DefaultConfig.CacheDir, DefaultConfig.ClientId)
                 .WithLinuxKeyring(DefaultConfig.LinuxKeyRingSchema, DefaultConfig.LinuxKeyRingCollection, DefaultConfig.LinuxKeyRingLabel, DefaultConfig.LinuxKeyRingAttr1, DefaultConfig.LinuxKeyRingAttr2)
                 .WithMacKeyChain(DefaultConfig.KeyChainServiceName, DefaultConfig.KeyChainAccountName);

            await LoginInternalAsync(
                DefaultConfig.Resource,
                DefaultConfig.TenantId,
                DefaultConfig.Environment,
                DefaultConfig.ClientId,
                storagePropertiesBuilder.Build()).ConfigureAwait(false);

        }

        private static async Task LoginWithCommandLineArgsAsync(string[] args) // No Linux support
        {
            try
            {
                string resource = args[0];
                string tenant = args[1];
                Uri baseAuthority = new Uri(args[2]);
#pragma warning disable CA1305 // Specify IFormatProvider
                bool validateAuthority = Convert.ToBoolean(args[3]);
#pragma warning restore CA1305 // Specify IFormatProvider
                string clientId = args[4];
                string cacheFileName = args[5];
                string cacheDirectory = Path.Combine(MsalCacheHelper.UserRootDirectory, args[6]);

                string serviceName = null;
                string accountName = null;

                var storagePropertiesBuilder =
                    new StorageCreationPropertiesBuilder(cacheFileName, cacheDirectory, clientId);


                if (args.Length > 7)
                {
                    serviceName = args[7];
                    accountName = args[8];
                    storagePropertiesBuilder.WithMacKeyChain(serviceName, accountName);
                }

                var storageProperties = storagePropertiesBuilder.Build();

                await LoginInternalAsync(resource, tenant, baseAuthority, clientId, storageProperties).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging in with Msal: {ex}");
                PrintUsage();
            }
        }

        private static async Task LoginInternalAsync(string resource, string tenant, Uri baseAuthority, string clientId, StorageCreationProperties storageProperties)
        {
            (var scopes, var app, var helper) = await CreatePublicClientAndBindCacheAsync(
                resource,
                tenant,
                baseAuthority,
                clientId,
                storageProperties
                ).ConfigureAwait(false);

            // The token cache helper's main feature is this event
            if (s_helper == null)
            {
                s_helper = helper;
                s_helper.CacheChanged += (s, e) =>
                {
                    Console.WriteLine($"Cache Changed, Added: {e.AccountsAdded.Count()} Removed: {e.AccountsRemoved.Count()}");
                };
            }

            // Note: in a real app, you should ALWAYS call AcquireTokenSilent first!
            var authResult = await app
                .AcquireTokenInteractive(scopes)
                .ExecuteAsync()
                .ConfigureAwait(false);

            var accounts = await app.GetAccountsAsync().ConfigureAwait(false);
            foreach (var acc in accounts)
            {
                await app.AcquireTokenSilent(scopes, acc).ExecuteAsync().ConfigureAwait(false);
                await app.RemoveAsync(acc).ConfigureAwait(false);
            }
        }

        private static void PrintUsage()
        {
            var usageString = @"USAGE: {quit,exit,help,login-msal, login-msal-defaults}
login-msal takes args [resource tenant baseAuthority validateAuthority clientId cacheFileName cacheDirectory {macServiceName macAccountName}]";

            Console.WriteLine(usageString);
        }

        private static async Task<(string[], IPublicClientApplication, MsalCacheHelper)> CreatePublicClientAndBindCacheAsync(
            string resource,
            string tenant,
            Uri baseAuthority,
            string clientId,
            StorageCreationProperties storageCreationProperties)
        {
            // tenant can be null
            resource = resource ?? throw new ArgumentNullException(nameof(resource));

            Console.WriteLine($"Using resource: '{resource}', tenant:'{tenant}'");

            var scopes = new string[] { resource + "/.default" };

            Console.WriteLine($"Using scopes: '{string.Join(",", scopes)}'");

            var authority = $"{baseAuthority.AbsoluteUri}{tenant}";
            Console.WriteLine($"GetPublicClient for authority: '{authority}'");

            Uri authorityUri = new Uri(authority);
            var appBuilder = PublicClientApplicationBuilder.Create(clientId)
                .WithAuthority(authorityUri)
                .WithRedirectUri("http://localhost");
            var app = appBuilder.Build();
            Console.WriteLine($"Built public client");

            // This hooks up our custom cache onto the one used by MSAL
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageCreationProperties).ConfigureAwait(false);
            cacheHelper.RegisterCache(app.UserTokenCache);

            Console.WriteLine($"Cache registered");

            return (scopes, app, cacheHelper);
        }
    }
}
