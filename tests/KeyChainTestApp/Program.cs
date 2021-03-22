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
                        2. Test KeyChain entry different location
                        
                        3. Test iOS-style KeyChain entry, location like PowserShell
                        4. Test iOS-style KeyChain entry, different location

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
                                    keyChainAccountName: "MSALCache",
                                    s_logger);

                            TestAccessors(macKeychainAccessor1);


                            break;
                        case '2':

                            Console.WriteLine("Type a keychain service or Enter to use `Microsoft.Developer.Test.<Guid>` ");
                            string service = Console.ReadLine();
                            if (string.IsNullOrEmpty(service))
                            {
                                service = $"Microsoft.Developer.Test.{Guid.NewGuid()}";
                           
                            }

                            Console.WriteLine("Type a keychain account or Enter to use `MSALCache` ");
                            string account = Console.ReadLine();
                            if (string.IsNullOrEmpty(account))
                            {
                                account = $"MSALCache";
                            }

                            Console.WriteLine($"Using Account {account} and Service: {service}");

                            MacKeychainAccessor macKeychainAccessor2 =
                                new MacKeychainAccessor(
                                    cacheFilePath: "~/.local/microsoft.test.txt",
                                    keyChainServiceName: service,
                                    keyChainAccountName: account,
                                    s_logger);

                            ReadWrite(macKeychainAccessor2);

                            break;

                        case '3':
                            MacKeychainAccessor macKeychainAccessor3 =
                               new MacKeychainAccessor(
                                   cacheFilePath: "~/.local/microsoft.test.txt",
                                   keyChainServiceName: "Microsoft.Developer.IdentityService",
                                   keyChainAccountName: "MSALCache",
                                   s_logger);

                            //TestAccessors(macKeychainAccessor1);

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
                ReadWrite(persistenceValidator);

            }
            catch (Exception e)
            {
                PrintException(e);
            }

            try
            {
                Console.WriteLine("Trying the real location");                
                ReadWrite(persistenceValidator);
            }
            catch (Exception e)
            {
                PrintException(e);
            }
        }

        private static void ReadWrite(ICacheAccessor accessor)
        {
            Console.WriteLine(accessor.ToString());

            accessor.Clear();
            accessor.Write(s_payload);
            var bytes = accessor.Read();
            accessor.Clear();

            Console.WriteLine($"Clear/Write/Read/Clear cycle complete. " +
                $"Read: `{ Encoding.UTF8.GetString(bytes) }`");
        }
    }
}
