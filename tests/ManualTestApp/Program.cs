using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace ManualTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            string input;
            do
            {
                Console.Write("> ");
                input = Console.ReadLine();
            } while (RunCommand(input));
        }

        /// <summary>
        /// Executes a user command
        /// </summary>
        /// <param name="splitInput">The command</param>
        /// <returns>True if execution should continue, false if we should terminate.</returns>
        private static bool RunCommand(string input)
        {

            var splitInput = input.ToLowerInvariant().Split(' ');
            var command = splitInput[0];
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
                    LoginWithMsal(args);
                }
                return true;

            default:
                Console.WriteLine("Unknown command");
                PrintUsage();
                return true;
            }
        }

        private static void LoginWithMsal(string[] args)
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
                string cacheDirectory = args[6];

                string serviceName = null;
                string accountName = null;

                if (args.Length > 7)
                {
                    serviceName = args[7];
                    accountName = args[8];
                }

                (var scopes, var app, var helper) = Utilities.GetPublicClient(
                    resource,
                    tenant,
                    baseAuthority,
                    validateAuthority,
                    clientId,
                    cacheFileName,
                    cacheDirectory,
                    serviceName,
                    accountName
                    );
                var builder = app.AcquireTokenWithDeviceCode(scopes, Utilities.DeviceCodeCallbackAsync);
                var authResult = builder.ExecuteAsync().Result;
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
