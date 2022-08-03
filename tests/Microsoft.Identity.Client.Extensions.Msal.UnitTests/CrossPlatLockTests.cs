// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Client.Extensions.Msal.UnitTests
{
    [TestClass]
    public class CrossPlatLockTests
    {
        const int NumTasks = 100;

        public TestContext TestContext { get; set; }

        [RunOnWindows]
        [TestCategory(TestCategories.Regression)]
        [WorkItem(187)] // https://github.com/AzureAD/microsoft-authentication-extensions-for-dotnet/issues/187
        public void DirNotExists()
        {
            // Arrange

            string cacheFileDir;

            // ensure the cache directory does not exist
            do
            {
                string tempDirName = System.IO.Path.GetRandomFileName();
                cacheFileDir = Path.Combine(Path.GetTempPath(), tempDirName);

            } while (Directory.Exists(cacheFileDir));

            using (var crossPlatLock = new CrossPlatLock(
              Path.Combine(cacheFileDir, "file.lockfile"), // the directory is guaranteed to not exist
              100,
              1))
            {
                // no-op
            }

            // before fixing the bug, an exception would occur here: 
            // System.InvalidOperationException: Could not get access to the shared lock file.
            // ---> System.IO.DirectoryNotFoundException: Could not find a part of the path 'C:\Users\....
        }




#if NETCOREAPP
        [TestMethod]
        [Ignore] // Could not get this to run on CI build due to small differences in where the App file gets dropped       
        public async Task MultipleProcessesUseAccessorAsync()
        {
            Trace.WriteLine("Starting test on " + TestHelper.GetOs());
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "AutomationApp");

            if (!Directory.Exists(dir))
            {
                Assert.Fail("Directory does not exist!" + dir);
            }

            string protectedFile = Path.Combine(dir, "protected_file.txt");

            File.Delete(protectedFile);

    
            Trace.WriteLine($"Working dir: {dir}");

            ProcessStartInfo psi = new ProcessStartInfo();


            psi.FileName = "dotnet";
            string dll = Path.Combine(dir, "Automation.TestApp.dll");
            psi.Arguments = dll + " " + protectedFile;

            psi.WorkingDirectory = dir;
            psi.UseShellExecute = false;

            
            var procs = Enumerable.Range(1, NumTasks)
                .Select((n) =>
                {
                    Process proc = Process.Start(psi);
                    Trace.WriteLine($"Process start {proc.Id}");
                    return proc;
                })
                .ToList();

             var tasks = procs   .Select(pr => pr.WaitForExitAsync());

            //await Task.Delay(30 * 1000).ConfigureAwait(false);
            await Task.WhenAll(tasks).ConfigureAwait(false);
            

            ValidateResult(protectedFile, NumTasks);
        }
    
        private void ValidateResult(string protectedFile, int expectedNumberOfOperations)
        {
            Trace.WriteLine("Protected File Content:");
            Trace.WriteLine(File.ReadAllText(protectedFile));

            var lines = File.ReadAllLines(protectedFile);

            string previousThread = null;

            foreach (var line in lines)
            {
                var tokens = line.Split(' ');
                string inOutToken = tokens[0];
                string payload = tokens[1];

                Assert.IsTrue(!string.IsNullOrWhiteSpace(payload));
                if (previousThread != null)
                {
                    Assert.AreEqual(payload, previousThread);
                    Assert.AreEqual(">", inOutToken);
                    previousThread = null;
                }
                else
                {
                    Assert.AreEqual("<", inOutToken);
                    previousThread = payload;
                }
            }

            Assert.AreEqual(expectedNumberOfOperations * 2, lines.Count());

        }
#endif
    }
}
