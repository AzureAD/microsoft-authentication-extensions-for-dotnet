using System;
using System.Text;
using Microsoft.Identity.Client.Extensions.Msal;

namespace KeyChainTestApp
{
    class Program
    {
        private static TraceSourceLogger s_logger =
            new TraceSourceLogger(new System.Diagnostics.TraceSource("CacheExt.TestApp"));

        private static byte[] s_payload = Encoding.UTF8.GetBytes("Hello world from the MSAL cache test app");
        private static byte[] s_payload2 = Encoding.UTF8.GetBytes("UPDATED");
        static void Main(string[] args)
        {
            if (!SharedUtilities.IsMacPlatform())
            {
                Console.WriteLine("This app should run on a Mac");
                Console.ReadLine();
                return;
            }

            while (true)
            {
                // Display menu
                Console.WriteLine($@"
                        1. Test KeyChain entry similar to PowerShell
                        2. Test KeyChain entry different location (read - write - read - delete)
                        
                    Enter your Selection: ");
                char.TryParse(Console.ReadLine(), out var selection);
                try
                {
                    switch (selection)
                    {
                        case '1':
                            MacKeychainAccessor macKeychainAccessor1 =
                                new MacKeychainAccessor(
                                    cacheFilePath: "~/.local/.IdentityService",
                                    keyChainServiceName: "Microsoft.Developer.IdentityService",
                                    keyChainAccountName: "msal.cache",
                                    s_logger);

                            TestAccessors(macKeychainAccessor1);


                            break;
                        case '2':

                            Console.WriteLine("Type a keychain service or Enter to use `Microsoft.Developer.IdentityService` ");
                            string service = Console.ReadLine();
                            if (string.IsNullOrEmpty(service))
                            {
                                service = "Microsoft.Developer.IdentityService";

                            }

                            Console.WriteLine("Type a keychain account or Enter to use `msal.cache.2` ");
                            string account = Console.ReadLine();
                            if (string.IsNullOrEmpty(account))
                            {
                                account = $"msal.cache.2";
                            }

                            Console.WriteLine($"Using Account {account} and Service: {service}");

                            MacKeychainAccessor macKeychainAccessor2 =
                                new MacKeychainAccessor(
                                    cacheFilePath: "~/.local/microsoft.test.txt",
                                    keyChainServiceName: service,
                                    keyChainAccountName: account,
                                    s_logger);

                            ReadOrReadWriteClear(macKeychainAccessor2);

                            break;


                        case '3':
                            var chain = new MacOSKeychain();

                            service = "Microsoft.Developer.IdentityService3";
                            account = $"msal.cache.3";

                            //chain.Add2(service, account, s_payload);
                            chain.AddOrUpdate(service, account, s_payload);
                            chain.AddOrUpdate(service, account, s_payload2);
                            

                            break;
                    }
                }
                catch (Exception ex)
                {
                    PrintException(ex);
                }
            }
        }

        private static void PrintException(Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Exception : " + ex);
            Console.ResetColor();
            Console.WriteLine("Hit Enter to continue");

            Console.Read();
        }

        private static void TestAccessors(ICacheAccessor macKeychainAccessor1)
        {
            var persistenceValidator = macKeychainAccessor1.CreateForPersistenceValidation();
            try
            {
                Console.WriteLine("Trying the location used for validation first .. ");
                ReadOrReadWriteClear(persistenceValidator);

            }
            catch (Exception e)
            {
                PrintException(e);
            }

            try
            {
                Console.WriteLine("Trying the real location");
                ReadOrReadWriteClear(macKeychainAccessor1);
            }
            catch (Exception e)
            {
                PrintException(e);
            }
        }



        private static void ReadOrReadWriteClear(ICacheAccessor accessor)
        {

            Console.WriteLine(accessor.ToString());
            var bytes = accessor.Read();
            if (bytes == null || bytes.Length == 0)
            {
                Console.WriteLine("No data found, writing some");
                accessor.Write(s_payload);
                var bytes2 = accessor.Read();
                accessor.Clear();
                Console.WriteLine("All good");

            }
            else
            {
                string s = Encoding.UTF8.GetString(bytes);
                Console.WriteLine($"Found some data ... {s.Substring(0, 20)}...");

                Console.WriteLine("Stopping");

            }


        }
    }
}
