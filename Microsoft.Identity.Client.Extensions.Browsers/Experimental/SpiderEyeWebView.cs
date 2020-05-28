using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client.Extensibility;
using SpiderEye;
using SpiderEye.Linux;
using SpiderEye.Mac;
using SpiderEye.Windows;

namespace Microsoft.Identity.Client.Extensions.Browsers.Experimental
{
    /// <summary>
    /// 
    /// </summary>
    public class SpiderEyeWebView : ICustomWebUi
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="authorizationUri"></param>
        /// <param name="redirectUri"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<Uri> AcquireAuthorizationCodeAsync(Uri authorizationUri, Uri redirectUri, CancellationToken cancellationToken)
        {
            Init();
            using (var window = new SpiderEye.Window())
            {
                window.UseBrowserTitle = true;
                window.CanResize = true;
                window.EnableDevTools = false;


                Application.Run(window, authorizationUri.AbsoluteUri);
            }

            return null;
        }

        private static void Init()
        {
            if (OperatingSystem.IsWindows())
            {
                WindowsApplication.Init();
            }
            if (OperatingSystem.IsMacOS())
            {
                MacApplication.Init();
            }
            if (OperatingSystem.IsLinux())
            {
                LinuxApplication.Init();
            }
        }
    }

    internal static class OperatingSystem
    {
        public static bool IsWindows() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static bool IsMacOS() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static bool IsLinux() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }
}
