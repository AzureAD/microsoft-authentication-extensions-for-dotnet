//------------------------------------------------------------------------------
// <copyright file="IDocHostUIHandler.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Managed defintion for the native IDocHostUIHandler COM interface
    // See internal source at https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/UnsafeNativeMethods.cs
    [ComImport]
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("BD3F23C0-D43E-11CF-893B-00AA00BDCE1A")]
    interface IDocHostUIHandler
    {
        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int ShowContextMenu([In, MarshalAs(UnmanagedType.U4)] int dwID, [In] POINT pt, [In, MarshalAs(UnmanagedType.Interface)] object pcmdtReserved,
            [In, MarshalAs(UnmanagedType.Interface)] object pdispReserved);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int GetHostInfo([In, Out] DOCHOSTUIINFO info);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int ShowUI([In, MarshalAs(UnmanagedType.I4)] int dwID, [In] IOleInPlaceActiveObject activeObject, [In] IOleCommandTarget commandTarget,
            [In] IOleInPlaceFrame frame, [In] IOleInPlaceUIWindow doc);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int HideUI();

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int UpdateUI();

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int EnableModeless([In, MarshalAs(UnmanagedType.Bool)] bool fEnable);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int OnDocWindowActivate([In, MarshalAs(UnmanagedType.Bool)] bool fActivate);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int OnFrameWindowActivate([In, MarshalAs(UnmanagedType.Bool)] bool fActivate);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int ResizeBorder([In] COMRECT rect, [In] IOleInPlaceUIWindow doc, bool fFrameWindow);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int TranslateAccelerator([In] ref MSG msg, [In] ref Guid group, [In, MarshalAs(UnmanagedType.I4)] int nCmdID);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int GetOptionKeyPath([Out, MarshalAs(UnmanagedType.LPArray)] string[] pbstrKey, [In, MarshalAs(UnmanagedType.U4)] int dw);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int GetDropTarget([In, MarshalAs(UnmanagedType.Interface)] IOleDropTarget pDropTarget,
            [MarshalAs(UnmanagedType.Interface)] out IOleDropTarget ppDropTarget);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int GetExternal([MarshalAs(UnmanagedType.Interface)] out object ppDispatch);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        int TranslateUrl([In, MarshalAs(UnmanagedType.U4)] int dwTranslate, [In, MarshalAs(UnmanagedType.LPWStr)] string strURLIn,
            [MarshalAs(UnmanagedType.LPWStr)] out string pstrURLOut);

        [return: MarshalAs(UnmanagedType.I4)]
        [PreserveSig]
        // Please, don't use System.Windows.Forms.IDataObject it is wrong one.
        int FilterDataObject(System.Runtime.InteropServices.ComTypes.IDataObject pDO, out System.Runtime.InteropServices.ComTypes.IDataObject ppDORet);
    }
}
