// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace ManualTestApp
{

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
                    await LoginWithMsalAsync(args).ConfigureAwait(false);
                }
                return true;

            default:
                Console.WriteLine("Unknown command");
                PrintUsage();
                return true;
            }
        }

        private static async Task LoginWithMsalAsync(string[] args)
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

                if (args.Length > 7)
                {
                    serviceName = args[7];
                    accountName = args[8];
                }

                (var scopes, var app, var helper) = await Utilities.GetPublicClientAsync(
                    resource,
                    tenant,
                    baseAuthority,
                    validateAuthority,
                    clientId,
                    cacheFileName,
                    cacheDirectory,
                    serviceName,
                    accountName
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

                var builder = app.AcquireTokenInteractive(scopes);
                var authResult = await builder.ExecuteAsync().ConfigureAwait(false);

                var accounts = await app.GetAccountsAsync().ConfigureAwait(false);
                foreach (var acc in accounts)
                {
                    await app.AcquireTokenSilent(scopes, acc).ExecuteAsync().ConfigureAwait(false);

                    await app.RemoveAsync(acc).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging in with Msal: {ex}");
                PrintUsage();
            }
        }

        private static void PrintUsage()
        {
            var usageString = @"USAGE: {quit,exit,help,login-msal}
login-msal takes args [resource tenant baseAuthority validateAuthority clientId cacheFileName cacheDirectory {macServiceName macAccountName}]";

            Console.WriteLine(usageString);
        }
    }
}
