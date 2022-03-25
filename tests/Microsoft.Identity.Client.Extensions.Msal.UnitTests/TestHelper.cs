// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Client.Extensions.Msal.UnitTests
{
    public static class TestHelper
    {
        /// <summary>
        /// Waits asynchronously for the process to exit.
        /// </summary>
        /// <param name="process">The process to wait for cancellation.</param>
        /// <param name="cancellationToken">A cancellation token. If invoked, the task will return 
        /// immediately as canceled.</param>
        /// <returns>A Task representing waiting for the process to end.</returns>
        public static Task WaitForExitAsync(this Process process,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) =>
            {
                Trace.WriteLine($"Process finished {process.Id}");
                tcs.TrySetResult(null);
            };

            if (cancellationToken != default(CancellationToken))
            {
                cancellationToken.Register(tcs.SetCanceled);
            }

            return tcs.Task;
        }
        public static string GetOs()
        {
            if (SharedUtilities.IsLinuxPlatform())
            {
                return "Linux";
            }

            if (SharedUtilities.IsMacPlatform())
            {
                return "Mac";
            }

            if (SharedUtilities.IsWindowsPlatform())
            {
                return "Windows";
            }

            throw new InvalidOperationException("Unknown");
        }
    }

    public static class FileHelper
    {
        /// <summary>
        /// Checks that file permissions are set to 600.
        /// </summary>
        /// <param name="filePath"></param>
        public static void AssertChmod600(string filePath)
        {
            if (SharedUtilities.IsWindowsPlatform())
            {
                FileInfo fi = new FileInfo(filePath);
                var acl = fi.GetAccessControl();
                var accessRules = acl.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));

                Assert.AreEqual(1, accessRules.Count);

                var rule = accessRules.Cast<FileSystemAccessRule>().Single();

                Assert.AreEqual(FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Synchronize, rule.FileSystemRights);
                Assert.AreEqual(AccessControlType.Allow, rule.AccessControlType);
                Assert.AreEqual(System.Security.Principal.WindowsIdentity.GetCurrent().User, rule.IdentityReference);
                Assert.IsFalse(rule.IsInherited);
                Assert.AreEqual(InheritanceFlags.None, rule.InheritanceFlags);
            }
            else
            {
                // e.g. -rw------ 1 user1 user1 1280 Mar 23 08:39 /home/user1/g/Program.cs
                var output = ExecuteAndCaptureOutput($"ls -l {filePath}");
                Assert.IsTrue(output.StartsWith("-rw------")); // 600
            }
        }

        private static string ExecuteAndCaptureOutput(string cmd)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\""
                }
            };

            string output = string.Empty;

            process.OutputDataReceived += (sender, args) =>
            {
                output += args.Data;
            };

            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();

            return output;

        }
    }
}
