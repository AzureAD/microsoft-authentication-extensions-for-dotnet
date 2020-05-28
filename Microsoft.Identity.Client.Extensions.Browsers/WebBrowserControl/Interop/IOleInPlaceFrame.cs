//------------------------------------------------------------------------------
// <copyright file="IOleInPlaceFrame.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Managed defintion for the native IOleInPlaceFrame COM interface
    // See internal source at https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/UnsafeNativeMethods.cs
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00000116-0000-0000-C000-000000000046")]
    interface IOleInPlaceFrame
    {
        IntPtr GetWindow();

        [PreserveSig]
        int ContextSensitiveHelp(int fEnterMode);

        [PreserveSig]
        int GetBorder([Out] COMRECT lprectBorder);

        [PreserveSig]
        int RequestBorderSpace([In] COMRECT pborderwidths);

        [PreserveSig]
        int SetBorderSpace([In] COMRECT pborderwidths);

        [PreserveSig]
        int SetActiveObject([In, MarshalAs(UnmanagedType.Interface)] IOleInPlaceActiveObject pActiveObject, [In, MarshalAs(UnmanagedType.LPWStr)] string pszObjName);

        [PreserveSig]
        int InsertMenus([In] IntPtr hmenuShared, [In, Out] tagOleMenuGroupWidths lpMenuWidths);

        [PreserveSig]
        int SetMenu([In] IntPtr hmenuShared, [In] IntPtr holemenu, [In] IntPtr hwndActiveObject);

        [PreserveSig]
        int RemoveMenus([In] IntPtr hmenuShared);

        [PreserveSig]
        int SetStatusText([In, MarshalAs(UnmanagedType.LPWStr)] string pszStatusText);

        [PreserveSig]
        int EnableModeless(bool fEnable);

        [PreserveSig]
        int TranslateAccelerator([In] ref MSG lpmsg, [In, MarshalAs(UnmanagedType.U2)] short wID);
    }
}
