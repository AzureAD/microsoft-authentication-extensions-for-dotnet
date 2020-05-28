//------------------------------------------------------------------------------
// <copyright file="IOleCommandTarget.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Managed defintion for the native IOleCommandTarget COM interface
    // See internal source at https://referencesource.microsoft.com/#system.windows.forms/winforms/Managed/System/WinForms/NativeMethods.cs
    [ComImport]
    [ComVisible(true)]
    [Guid("B722BCCB-4E68-101B-A2BC-00AA00404770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IOleCommandTarget
    {
        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int QueryStatus(ref Guid pguidCmdGroup, int cCmds, [In, Out] OLECMD prgCmds, [In, Out] IntPtr pCmdText);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int Exec(IntPtr guid, int nCmdID, int nCmdexecopt, [In, MarshalAs(UnmanagedType.LPArray)] object[] pvaIn, IntPtr pvaOut);
    }
}
