// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
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
        const int NumTasks = 30;

        public TestContext TestContext { get; set; }


        [TestMethod]
        public async Task MultipleProcessesUseAccessorAsync()
        {
            Trace.WriteLine("Starting test on " + TestHelper.GetOs());
            string dir = Directory.GetCurrentDirectory();
            string protectedFile = Path.Combine(dir, "protected_file");

            File.Delete(protectedFile);

            string consoleProj = null;
            string workingDir = Path.Combine(dir, "AutomationApp");

            Trace.WriteLine($"Working dir: {workingDir}");

            if (SharedUtilities.IsWindowsPlatform())
            {
                consoleProj = "Automation.TestApp.exe";
            }
            else
            {
                consoleProj = "Automation.TestApp";
            }

            ProcessStartInfo psi = new ProcessStartInfo(consoleProj, $" -p {protectedFile} ");
            psi.WorkingDirectory = workingDir;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            var tasks = Enumerable.Range(1, NumTasks)
                .Select((n) =>
                {
                    Process proc = Process.Start(psi);
                    proc.OutputDataReceived += (sender, e) => { Trace.WriteLine(e.Data); };
                    proc.ErrorDataReceived += (sender, e) => {
                        Trace.WriteLine("ERROR " + e.Data);
                        Assert.Fail("Error occured in a process " + e.Data);
                    };
                    return proc;
                })
                .Select(pr => pr.WaitForExitAsync());

            await Task.WhenAll(tasks).ConfigureAwait(false);

            ValidateResult(protectedFile, NumTasks);
        }

        private void ValidateResult(string protectedFile, int expectedNumberOfOperations)
        {
            Trace.WriteLine("Protected File Content:");
            Trace.WriteLine(File.ReadAllText(protectedFile));

            var lines = File.ReadAllLines(protectedFile);
            Assert.AreEqual(expectedNumberOfOperations * 2, lines.Count());

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
        }

    }
}
