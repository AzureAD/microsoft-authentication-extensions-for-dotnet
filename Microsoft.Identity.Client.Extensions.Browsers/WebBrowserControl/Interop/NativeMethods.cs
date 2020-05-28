//------------------------------------------------------------------------------
// <copyright file="NativeMethods.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Managed PInvoke definitions for various native methods
    static class NativeMethods
    {
        // User32.dll
        // See definition at https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdc
        [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern IntPtr GetDC(IntPtr hWnd);
        // See definition at https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-releasedc
        [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        // See definition at https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-isprocessdpiaware
        [DllImport("User32.dll", CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        public static extern bool IsProcessDPIAware();

        // Gdi32.dll
        // See definition at https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-getdevicecaps
        [DllImport("Gdi32.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        // IEFrame.dll
        // See definition at https://docs.microsoft.com/en-us/previous-versions/windows/internet-explorer/ie-developer/platform-apis/dn720860(v=vs.85)
        [DllImport("IEFRAME.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        public static extern int SetQueryNetSessionCount(SessionOp sessionOp);
    }
}
