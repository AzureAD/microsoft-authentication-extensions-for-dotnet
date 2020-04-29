using System;
using System.Diagnostics;
using System.Globalization;
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
        private static readonly TimeSpan s_artificialContention = TimeSpan.FromMilliseconds(1000);

        [TestMethod]
        public async Task MultipleThreadsUseAccessorAsync()
        {
            string dir = Directory.GetCurrentDirectory();
            string protectedFile = Path.Combine(dir, "protected_file");
            string lockFile = Path.Combine(dir, "protected_file.lock");

            File.Delete(protectedFile);
            File.Delete(lockFile);

            var tasks = Enumerable.Range(1, NumTasks)
                .Select(id => WritePayloadToSyncFileAsync(
                    lockFile,
                    protectedFile,
                        id.ToString(CultureInfo.InvariantCulture) +
                        " " +
                        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)))
                .ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            ValidateResult(protectedFile, NumTasks);
        }

        private async static Task WritePayloadToSyncFileAsync(string lockFile, string protectedFile, string payload)
        {
            CrossPlatLock crossPlatLock = null;
            try
            {
                crossPlatLock = new CrossPlatLock(lockFile);
                using (StreamWriter sw = new StreamWriter(protectedFile, true))
                {
                    sw.WriteLine($"< {payload}");

                    // increase contantion by simulating a slow writer
                    await Task.Delay(1000).ConfigureAwait(false);
                    sw.WriteLine($"> {payload}");
                }
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine("File write failure! " + ex);
            }
            finally
            {
                try
                {
                    crossPlatLock.Dispose();
                }
                catch (IOException ex2)
                {
                    Console.Error.WriteLine("Lock release failure! " + ex2);
                }
            }
        }

        private void ValidateResult(string protectedFile, int expectedNumberOfOperations)
        {
            Trace.WriteLine("Protected File Content:");
            Trace.WriteLine(File.ReadAllText(protectedFile));

            var lines = File.ReadAllLines(protectedFile);
            Assert.AreEqual(expectedNumberOfOperations * 2, lines.Count());

            string previousPayload = null;

            foreach (var line in lines)
            {
                var tokens = line.Split(' ');
                string inOutToken = tokens[0];
                string payload = tokens[1];

                Assert.IsTrue(!string.IsNullOrWhiteSpace(payload));
                if (previousPayload != null)
                {
                    Assert.AreEqual(payload, previousPayload);
                    Assert.AreEqual(">", inOutToken);
                    previousPayload = null;
                }
                else
                {
                    Assert.AreEqual("<", inOutToken);
                    previousPayload = payload;
                }
            }
        }
    }
}
