// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Identity.Extensions
{
    /// <summary>
    /// A set of utilities shared between service and client
    /// </summary>
    internal static class SharedUtilities
    {
        /// <summary>
        /// default base cache path
        /// </summary>
        private const string DefaultBaseCachePath = @".IdentityService";

        private const int ConnectRetryCount = 1;
        private const int ConnectRetryWaitInMs = 100;

        /// <summary>
        /// Aad configuration file extension
        /// </summary>
        private const string ConfigFileExtension = ".Configuration.json";

        /// <summary>
        /// Provider for github accounts
        /// </summary>
        private const string GitHubProvider = "github.com";

        private static readonly string s_homeEnvVar = Environment.GetEnvironmentVariable("HOME");
        private static readonly string s_lognameEnvVar = Environment.GetEnvironmentVariable("LOGNAME");
        private static readonly string s_userEnvVar = Environment.GetEnvironmentVariable("USER");
        private static readonly string s_lNameEnvVar = Environment.GetEnvironmentVariable("LNAME");
        private static readonly string s_usernameEnvVar = Environment.GetEnvironmentVariable("USERNAME");

        /// <summary>
        /// For the case where we want to log an exception but not handle it in a when clause
        /// </summary>
        /// <param name="loggingAction">Logging action</param>
        /// <returns>false always in order to skip the exception filter</returns>
        public static bool LogExceptionAndDoNotHandler(Action loggingAction)
        {
            loggingAction();
            return false;
        }

        /// <summary>
        /// Format the guid as a string
        /// </summary>
        /// <param name="guid">Guid to format</param>
        /// <returns>Formatted guid in string format</returns>
        public static string FormatGuidAsString(this Guid guid)
        {
            return guid.ToString("D", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Generate the default artifact location
        /// </summary>
        /// <returns>Default artifact location</returns>
        public static string GetDefaultArtifactPath()
        {
            return Path.Combine(SharedUtilities.GetIdentityServiceRootDirectory(), SharedUtilities.DefaultBaseCachePath);
        }

        /// <summary>
        /// Generate the default artifact location
        /// </summary>
        /// <param name="windowsOS">Is the operating system windows or not</param>
        /// <returns>Default artifact location</returns>
        public static string GetDefaultArtifactPath(bool windowsOS)
        {
            return Path.Combine(SharedUtilities.GetIdentityServiceUserRootDirectory(windowsOS), SharedUtilities.DefaultBaseCachePath);
        }

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
#if NET46
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
#if NET46
            return Environment.OSVersion.Platform == PlatformID.Unix;
#else
            return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
#endif
        }

        /// <summary>
        /// Gets the users home directory
        /// </summary>
        /// <returns>Home directory</returns>
        public static string GetIdentityServiceRootDirectory()
        {
            bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            return GetIdentityServiceUserRootDirectory(isWindows);
        }

        /// <summary>
        /// Generate the default file location
        /// </summary>
        /// <param name="windowsOS">Is the operating system windows or not</param>
        /// <returns>Home directory</returns>
        internal static string GetIdentityServiceUserRootDirectory(bool windowsOS)
        {
            return !windowsOS ? SharedUtilities.GetUserHomeDirOnUnix() : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        /// <summary>
        /// Get the based directory for the service hub
        /// </summary>
        /// <returns>base directory for theservice hub</returns>
        internal static string GetServiceHubBaseDirforUnix()
        {
            return Path.Combine(SharedUtilities.GetUserHomeDirOnUnix(), ".ServiceHub");
        }

        /// <summary>
        /// Determines if the authority is an ADFS authority
        /// </summary>
        /// <param name="authority">Uri to check if it is ADFS or not</param>
        /// <returns>true if the authority is an adfs authority, false otherwise</returns>
        internal static bool IsADFSAuthority(Uri authority)
        {
            // Taken from ADAL src/ADAL.PCL/Authority/Authenticator.cs
            if (authority == null)
            {
                return false;
            }

            try
            {
                string path = authority.AbsolutePath.Substring(1);
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                if (!path.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    path = path + "/";
                }

                string firstPath = path.Substring(0, path.IndexOf("/", StringComparison.Ordinal));
                return string.Compare(firstPath, "adfs", StringComparison.OrdinalIgnoreCase) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Execute a function within a file lock
        /// </summary>
        /// <param name="function">Function to execute within the filelock</param>
        /// <param name="lockFileLocation">Full path of the file to be locked</param>
        /// <param name="lockRetryCount">Number of retry attempts for acquiring the file lock</param>
        /// <param name="lockRetryWaitInMs">Interval to wait for before retrying to acquire the file lock</param>
        /// <param name="cancellationToken">cancellationToken</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        internal static async Task ExecuteWithinLockAsync(Func<Task> function, string lockFileLocation, int lockRetryCount, int lockRetryWaitInMs, CancellationToken cancellationToken = default(CancellationToken))
        {
            Exception exception = null;
            FileStream fileStream = null;
            for (int tryCount = 0; tryCount < lockRetryCount; tryCount++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Create lock file dir if it doesn't already exist
                Directory.CreateDirectory(Path.GetDirectoryName(lockFileLocation));
                try
                {
                    // We are using the file locking to synchronize the store, do not allow multiple writers or readers for the file.
                    fileStream = new FileStream(lockFileLocation, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    break;
                }
                catch (IOException ex)
                {
                    exception = ex;
                    await Task.Delay(TimeSpan.FromMilliseconds(lockRetryWaitInMs)).ConfigureAwait(false);
                }
            }

            if (fileStream == null && exception != null)
            {
                throw new InvalidOperationException("Could not get access to the shared lock file.", exception);
            }

            using (fileStream)
            {
                await function().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Remove prompt=login
        /// </summary>
        /// <param name="queryParameters">query parameters</param>
        /// <returns>returns string with prompt=login removed</returns>
        internal static string RemovePromptQueryParameter(string queryParameters)
        {
            if(string.IsNullOrWhiteSpace(queryParameters))
            {
                return queryParameters;
            }

            foreach (string stringToRemove in new string[] { "&prompt=login", "prompt=login" })
            {
                queryParameters = queryParameters.ToLowerInvariant().Replace(stringToRemove, string.Empty);
            }

            return queryParameters;
        }

        /// <summary>
        /// Execute a function within a file lock
        /// </summary>
        /// <param name="function">Function to execute within the filelock</param>
        /// <param name="lockFileLocation">Full path of the file to be locked</param>
        /// <param name="lockRetryCount">Number of retry attempts for acquiring the file lock</param>
        /// <param name="lockRetryWaitInMs">Interval to wait for before retrying to acquire the file lock</param>
        /// <param name="cancellationToken">cancellationToken</param>
        internal static void ExecuteWithinLock(Func<bool> function, string lockFileLocation, int lockRetryCount, int lockRetryWaitInMs, CancellationToken cancellationToken = default(CancellationToken))
        {
            Exception exception = null;
            FileStream fileStream = null;
            for (int tryCount = 0; tryCount < lockRetryCount; tryCount++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Create lock file dir if it doesn't already exist
                Directory.CreateDirectory(Path.GetDirectoryName(lockFileLocation));
                try
                {
                    // We are using the file locking to synchronize the store, do not allow multiple writers or readers for the file.
                    fileStream = new FileStream(lockFileLocation, FileMode.OpenOrCreate, FileAccess.Read, FileShare.None);
                    break;
                }
                catch (IOException ex)
                {
                    exception = ex;
                    Task.Delay(TimeSpan.FromMilliseconds(lockRetryWaitInMs));
                }
            }

            if (fileStream == null && exception != null)
            {
                throw new InvalidOperationException("Could not get access to the shared lock file.", exception);
            }

            using (fileStream)
            {
                function();
            }
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
