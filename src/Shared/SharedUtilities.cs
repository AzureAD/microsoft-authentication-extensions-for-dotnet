// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#if ADAL
namespace Microsoft.Identity.Client.Extensions.Adal
#elif MSAL
namespace Microsoft.Identity.Client.Extensions.Msal
#else // WEB
namespace Microsoft.Identity.Client.Extensions.Web
#endif
{
    /// <summary>
    /// A set of utilities shared between service and client
    /// </summary>
    public static class SharedUtilities
    {
        /// <summary>
        /// default base cache path
        /// </summary>
        private static readonly string s_homeEnvVar = Environment.GetEnvironmentVariable("HOME");
        private static readonly string s_lognameEnvVar = Environment.GetEnvironmentVariable("LOGNAME");
        private static readonly string s_userEnvVar = Environment.GetEnvironmentVariable("USER");
        private static readonly string s_lNameEnvVar = Environment.GetEnvironmentVariable("LNAME");
        private static readonly string s_usernameEnvVar = Environment.GetEnvironmentVariable("USERNAME");

        /// <summary>
        ///  Is this a windows platform
        /// </summary>
        /// <returns>A  value indicating if we are running on windows or not</returns>
        public static bool IsWindowsPlatform()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        /// <summary>
        /// Is this a MAC platform
        /// </summary>
        /// <returns>A value indicating if we are running on mac or not</returns>
        public static bool IsMacPlatform()
        {
#if NET45
            // we have to also check for PlatformID.Unix because Mono can sometimes return Unix as the platform on a Mac machine.
            // see http://www.mono-project.com/docs/faq/technical/
            return Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Unix;
#else
            return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
#endif
        }

        /// <summary>
        /// Is this a linux platform
        /// </summary>
        /// <returns>A  value indicating if we are running on linux or not</returns>
        public static bool IsLinuxPlatform()
        {
#if NET45
            return Environment.OSVersion.Platform == PlatformID.Unix;
#else
            return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
#endif
        }

        /// <summary>
        /// Generate the default file location
        /// </summary>
        /// <returns>Root directory</returns>
        public static string GetUserRootDirectory()
        {
            return !IsWindowsPlatform()
                ? SharedUtilities.GetUserHomeDirOnUnix()
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }       

        private static string GetUserHomeDirOnUnix()
        {
            if (SharedUtilities.IsWindowsPlatform())
            {
                throw new NotSupportedException();
            }

            if (!string.IsNullOrEmpty(SharedUtilities.s_homeEnvVar))
            {
                return SharedUtilities.s_homeEnvVar;
            }

            string username = null;
            if (!string.IsNullOrEmpty(SharedUtilities.s_lognameEnvVar))
            {
                username = s_lognameEnvVar;
            }
            else if (!string.IsNullOrEmpty(SharedUtilities.s_userEnvVar))
            {
                username = s_userEnvVar;
            }
            else if (!string.IsNullOrEmpty(SharedUtilities.s_lNameEnvVar))
            {
                username = s_lNameEnvVar;
            }
            else if (!string.IsNullOrEmpty(SharedUtilities.s_usernameEnvVar))
            {
                username = s_usernameEnvVar;
            }

            if (SharedUtilities.IsMacPlatform())
            {
                return !string.IsNullOrEmpty(username) ? Path.Combine("/Users", username) : null;
            }
            else if (SharedUtilities.IsLinuxPlatform())
            {
                if (LinuxNativeMethods.getuid() == LinuxNativeMethods.RootUserId)
                {
                    return "/root";
                }
                else
                {
                    return !string.IsNullOrEmpty(username) ? Path.Combine("/home", username) : null;
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
