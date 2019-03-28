// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

#if ADAL
namespace Microsoft.Identity.Client.Extensions.Adal
#elif MSAL
namespace Microsoft.Identity.Client.Extensions.Msal
#else // WEB
namespace Microsoft.Identity.Client.Extensions.Web
#endif
{
internal static class LinuxNativeMethods
    {
        public const int RootUserId = 0;

        /// <summary>
        /// Get the real user ID of the calling process.
        /// </summary>
        /// <returns>the real user ID of the calling process</returns>
        [DllImport("libc")]
        public static extern int getuid();
    }
}
