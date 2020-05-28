//------------------------------------------------------------------------------
// <copyright file="OLECMD.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Managed defintion for the native OLECMD structure
    // See internal source at https://referencesource.microsoft.com/#system.windows.forms/winforms/Managed/System/WinForms/NativeMethods.cs
    [StructLayout(LayoutKind.Sequential)]
    sealed class OLECMD
    {
        [MarshalAs(UnmanagedType.U4)] public int cmdID;
        [MarshalAs(UnmanagedType.U4)] public int cmdf;
    }
}
