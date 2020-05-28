//------------------------------------------------------------------------------
// <copyright file="IOleInPlaceActiveObject.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Managed defintion for the native IOleInPlaceActiveObject COM interface
    // See internal source at https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/UnsafeNativeMethods.cs
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00000117-0000-0000-C000-000000000046")]
    interface IOleInPlaceActiveObject
    {
        [PreserveSig]
        int GetWindow(out IntPtr hwnd);

        void ContextSensitiveHelp(int fEnterMode);

        [PreserveSig]
        int TranslateAccelerator([In] ref MSG lpmsg);

        void OnFrameWindowActivate(bool fActivate);

        void OnDocWindowActivate(int fActivate);

        void ResizeBorder([In] COMRECT prcBorder, [In] IOleInPlaceUIWindow pUIWindow, bool fFrameWindow);

        void EnableModeless(int fEnable);
    }
}
