using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Automation.TestApp
{



    /// <summary>
    /// 
    /// </summary>
    public static class Program
    {
        private static readonly TimeSpan s_artificialContention = TimeSpan.FromMilliseconds(1000);

#pragma warning disable UseAsyncSuffix // Use Async suffix
        internal static async Task<int> Main(string[] args)
#pragma warning restore UseAsyncSuffix // Use Async suffix
        {
            Console.Out.WriteLine("Helloo!");
            string protectedFile;
            if (args == null || args.Length == 0 || string.IsNullOrEmpty(args[0]))
            {
                protectedFile = Path.Combine(Directory.GetCurrentDirectory(), "fileX.txt");
            }
            else
            {
                protectedFile = args[0];
            }

            string lockFile = protectedFile + ".lock";
            await WritePayloadToSyncFileAsync(lockFile, protectedFile)
                .ConfigureAwait(false);

            return 0;
        }


        private async static Task WritePayloadToSyncFileAsync(string lockFile, string protectedFile)
        {
            string pid = Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture);
            Console.WriteLine("Starting process: " + pid);
            CrossPlatLock crossPlatLock = null;
            try
            {
                crossPlatLock = new CrossPlatLock(lockFile);
                using (StreamWriter sw = new StreamWriter(protectedFile, true))
                {
                    sw.WriteLine($"< {pid} {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}");

                    // increase contention by simulating a slow writer
                    await Task.Delay(s_artificialContention).ConfigureAwait(false);

                    sw.WriteLine($"> {pid} {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}");
                    Console.WriteLine("Process finished: " + pid);

                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                throw;
            }
            finally
            {
                crossPlatLock.Dispose();
            }
        }
    }
}
