﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Automation.TestApp
{



    /// <summary>
    /// 
    /// </summary>
    public static class Program
    {
        private static readonly TimeSpan s_artificialContention = TimeSpan.FromMilliseconds(500);

#pragma warning disable UseAsyncSuffix // Use Async suffix
        internal static async Task<int> Main(string[] args)
#pragma warning restore UseAsyncSuffix // Use Async suffix
        {

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
            string errorFile = protectedFile + $"{pid}.e.txt";
            string pidFIle = Path.Combine(Path.GetDirectoryName(protectedFile), pid + ".txt");

            Console.WriteLine("Starting process: " + pid);
            CrossPlatLock crossPlatLock = null;
            try
            {
                crossPlatLock = new CrossPlatLock(lockFile);
                using (StreamWriter sw = new StreamWriter(protectedFile, true))
                {
                    await sw.WriteLineAsync($"< {pid} {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}").ConfigureAwait(false);

                    // increase contention by simulating a slow writer
                    await Task.Delay(s_artificialContention).ConfigureAwait(false);

                    await sw.WriteLineAsync($"> {pid} {DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}").ConfigureAwait(false);
                    ;
                    Console.WriteLine("Process finished: " + pid);

                }
            }
            catch (Exception e)
            {
                File.WriteAllText(errorFile, e.ToString());
                throw;
            }
            finally
            {
                File.WriteAllText(pidFIle, "done");
                crossPlatLock.Dispose();
            }
        }
    }
}
