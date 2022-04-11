using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Identity.Client.Extensions.Msal.Accessors
{
    internal static class FileWithPermissions
    {
        #region Unix specific

        // See https://ss64.com/bash/chmod.html 

        [DllImport("libc", SetLastError = true)]
        private static extern int chmod(string pathname, int mode);

        // user permissions
        const int S_IRUSR = 0x100;
        const int S_IWUSR = 0x80;
        const int S_IXUSR = 0x40;

        // group permission
        const int S_IRGRP = 0x20;
        const int S_IWGRP = 0x10;
        const int S_IXGRP = 0x8;

        // other permissions
        const int S_IROTH = 0x4;
        const int S_IWOTH = 0x2;
        const int S_IXOTH = 0x1;
        #endregion


        /// <summary>
        /// Creates a new file with "600" permissions (i.e. read / write only by the owner) and writes some data to it.
        /// On Windows, file security is more complex, but an equivalent is achieved.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException"></exception>
        public static void WriteToNewFileWithOwnerRWPermissions(string path, byte[] data)
        {
            if (SharedUtilities.IsWindowsPlatform())
            {
                WriteToNewFileWithOwnerRWPermissionsWindows(path, data);
            }
            else if (SharedUtilities.IsMacPlatform() || SharedUtilities.IsLinuxPlatform())
            {
                WriteToNewFileWithOwnerRWPermissionsUnix(path, data);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        /// <summary>
        /// Based on https://stackoverflow.com/questions/45132081/file-permissions-on-linux-unix-with-net-core
        /// </summary>
        private static void WriteToNewFileWithOwnerRWPermissionsUnix(string filePath, byte[] data)
        {
            using (File.Create(filePath))
            {
            }

            // Setting permissions to 0600 
            const int _0600 = S_IRUSR | S_IWUSR; // read & write only for user
            int result = chmod(filePath, _0600);

            if (result != 0)
            {                
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception)
                {
                    // ignored
                }

                throw new IOException($"Failed to set permissions on file {filePath} Error code: {result} Last error: {Marshal.GetLastWin32Error()}");
            }

            using (var stream = new FileStream(filePath, FileMode.Append))
            {
                stream.Write(data, 0, data.Length);
            }
        }

        /// <summary>
        /// Windows has a more complex file security system. "600" mode, i.e. read/write for owner translates to this in Windows.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="data"></param>
        private static void WriteToNewFileWithOwnerRWPermissionsWindows(string filePath, byte[] data)
        {
            FileSecurity security = new FileSecurity();
            var rights = FileSystemRights.Read | FileSystemRights.Write;

            // https://stackoverflow.com/questions/39480255/c-sharp-how-to-grant-access-only-to-current-user-and-restrict-access-to-others
            security.AddAccessRule(
                new FileSystemAccessRule(
                        WindowsIdentity.GetCurrent().Name,
                        rights,
                        InheritanceFlags.None,
                        PropagationFlags.NoPropagateInherit,
                        AccessControlType.Allow));

            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            FileStream fs = null;

            try
            {
#if NET452_OR_GREATER
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                fs = File.Create(filePath, data.Length, FileOptions.None, security);
#else
                FileInfo info = new FileInfo(filePath);
                fs = info.Create(FileMode.Create, rights, FileShare.Read, data.Length, FileOptions.None, security);
#endif


                fs.Write(data, 0, data.Length);
            }
            finally
            {
                fs?.Dispose();
            }
        }
    }
}
