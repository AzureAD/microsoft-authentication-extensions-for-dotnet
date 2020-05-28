//------------------------------------------------------------------------------
// <copyright file="DOCHOSTUIINFO.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Managed defintion for the native DOCHOSTUIINFO structure
    // See internal source at https://referencesource.microsoft.com/#system.windows.forms/winforms/Managed/System/WinForms/NativeMethods.cs
    [StructLayout(LayoutKind.Sequential)]
    [ComVisible(true)]
    sealed class DOCHOSTUIINFO
    {
        [MarshalAs(UnmanagedType.U4)] public int cbSize = Marshal.SizeOf(typeof(DOCHOSTUIINFO));
        [MarshalAs(UnmanagedType.I4)] public int dwFlags;
        [MarshalAs(UnmanagedType.I4)] public int dwDoubleClick;
        [MarshalAs(UnmanagedType.I4)] public int dwReserved1;
        [MarshalAs(UnmanagedType.I4)] public int dwReserved2;
    }
}
