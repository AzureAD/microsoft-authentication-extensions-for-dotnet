using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Extensions.Msal;

namespace FileLockApp
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                PrintUsage();
                Console.Read();
                return;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                File.Create(filePath);
            }
            int delay = int.Parse(args[1], CultureInfo.InvariantCulture);

            // this object tries to acquire the file lock every 100ms and gives up after 600 attempts (about 1 min)
            using (var crossPlatLock = new CrossPlatLock(filePath + ".lockfile")) 
            {
                Console.WriteLine("Acquired the lock...");

                Console.WriteLine("Writing...");

                File.WriteAllText(filePath, "< " + Process.GetCurrentProcess().Id);
                Console.WriteLine($"Waiting for {delay}s");

                await Task.Delay(delay * 1000).ConfigureAwait(false);

                Console.WriteLine("Writing...");
                File.WriteAllText(filePath, "> " + Process.GetCurrentProcess().Id);
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: FileLockApp.exe <file_path> <delay_in_seconds>");
            Console.WriteLine("");
            Console.WriteLine("This app will acquire a file lock on the file_path ");
            Console.WriteLine("Will write '< process_id' ");
            Console.WriteLine("Sleep for delay_in_seconds");
            Console.WriteLine("Will write '> process_id' ");

        }
    }
}
