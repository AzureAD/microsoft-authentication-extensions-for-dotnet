using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Automation.TestApp
{

    internal class Options
    {
        [Option('p', "protectedFile", Required = true, HelpText = "Protected file")]
        public string ProtectedFile { get; set; }

    }

    /// <summary>
    /// 
    /// </summary>
    public static class Program
    {
        private static readonly TimeSpan s_artificialContention = TimeSpan.FromMilliseconds(1000);

#pragma warning disable UseAsyncSuffix // Use Async suffix
        internal static async Task Main(string[] args)
#pragma warning restore UseAsyncSuffix // Use Async suffix
        {
            Options options = null;
            Parser.Default.ParseArguments<Options>(args).WithParsed(o => options = o);
            string protectedFile = options.ProtectedFile;
            string lockFile = protectedFile + ".lock";
            await WritePayloadToSyncFileAsync(lockFile, protectedFile)
                .ConfigureAwait(false);
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
                }
            }
            finally
            {
                crossPlatLock.Dispose();
            }
        }
    }
}
