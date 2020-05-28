//------------------------------------------------------------------------------
// <copyright file="IOleDropTarget.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Managed defintion for the native IOleDropTarget COM interface
    // See internal source at https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/UnsafeNativeMethods.cs
    [ComImport]
    [Guid("00000122-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IOleDropTarget
    {
        [PreserveSig]
        int OleDragEnter([In, MarshalAs(UnmanagedType.Interface)] object pDataObj, [In, MarshalAs(UnmanagedType.U4)] int grfKeyState,
            [In] POINT pt, [In, Out] ref int pdwEffect);

        [PreserveSig]
        int OleDragOver([In, MarshalAs(UnmanagedType.U4)] int grfKeyState, [In] POINT pt, [In, Out] ref int pdwEffect);

        [PreserveSig]
        int OleDragLeave();

        [PreserveSig]
        int OleDrop([In, MarshalAs(UnmanagedType.Interface)] object pDataObj, [In, MarshalAs(UnmanagedType.U4)] int grfKeyState,
            [In] POINT pt, [In, Out] ref int pdwEffect);
    }
}
