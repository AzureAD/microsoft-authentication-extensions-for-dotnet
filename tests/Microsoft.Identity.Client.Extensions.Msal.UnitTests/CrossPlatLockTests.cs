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
        const int NumTasks = 2;
        private static readonly TimeSpan s_artificialContention = TimeSpan.FromMilliseconds(50);

        [TestMethod]
        public async Task MultipleProcessesUseAccessorAsync()
        {
            string dir = Directory.GetCurrentDirectory();
            string protectedFile = Path.Combine(dir, "protected_file");

            File.Delete(protectedFile);

            string consoleProj = null;
            string workingDir = Path.Combine(Directory.GetCurrentDirectory(), "AutomationApp");
            if (SharedUtilities.IsWindowsPlatform())
            {
                consoleProj = "Automation.TestApp.exe";
            }
            else
            {
                consoleProj = "Automation.TestApp";
            }

            foreach (var path in workingDir)
            {
                Trace.WriteLine("---" + path); // full path
            }


            ProcessStartInfo psi = new ProcessStartInfo(consoleProj, $" -p {protectedFile} ");
            psi.WorkingDirectory = workingDir;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = true;

            var tasks = Enumerable.Range(1, NumTasks)
                .Select((n) =>
                {
                    Process p = Process.Start(psi);
                    Trace.WriteLine($"Started process {p.Id}");
                    return p;
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
