//------------------------------------------------------------------------------
// <copyright file="DWebBrowserEvents2.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Managed defintion for the native DWebBrowserEvents2 COM interface
    // See internal source at https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/UnsafeNativeMethods.cs
    [ComImport]
    [Guid("34A715A0-6587-11D0-924A-0020AFC7AC4D")]
    [TypeLibType(TypeLibTypeFlags.FHidden)]
#pragma warning disable CS0618
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
#pragma warning restore CS0618
    interface DWebBrowserEvents2
    {
        [DispId(0x66)]
        void StatusTextChange([In] string text);

        [DispId(0x6c)]
        void ProgressChange([In] int progress, [In] int progressMax);

        [DispId(0x69)]
        void CommandStateChange([In] long command, [In] bool enable);

        [DispId(0x6a)]
        void DownloadBegin();

        [DispId(0x68)]
        void DownloadComplete();

        [DispId(0x71)]
        void TitleChange([In] string text);

        [DispId(0x70)]
        void PropertyChange([In] string szProperty);

        [DispId(250)]
        void BeforeNavigate2([In, MarshalAs(UnmanagedType.IDispatch)] object pDisp, [In] ref object URL,
            [In] ref object flags, [In] ref object targetFrameName, [In] ref object postData,
            [In] ref object headers, [In, Out] ref bool cancel);

        [DispId(0xfb)]
        void NewWindow2([In, Out, MarshalAs(UnmanagedType.IDispatch)] ref object pDisp, [In, Out] ref bool cancel);

        [DispId(0xfc)]
        void NavigateComplete2([In, MarshalAs(UnmanagedType.IDispatch)] object pDisp, [In] ref object URL);

        [DispId(0x103)]
        void DocumentComplete([In, MarshalAs(UnmanagedType.IDispatch)] object pDisp, [In] ref object URL);

        [DispId(0xfd)]
        void OnQuit();

        [DispId(0xfe)]
        void OnVisible([In] bool visible);

        [DispId(0xff)]
        void OnToolBar([In] bool toolBar);

        [DispId(0x100)]
        void OnMenuBar([In] bool menuBar);

        [DispId(0x101)]
        void OnStatusBar([In] bool statusBar);

        [DispId(0x102)]
        void OnFullScreen([In] bool fullScreen);

        [DispId(260)]
        void OnTheaterMode([In] bool theaterMode);

        [DispId(0x106)]
        void WindowSetResizable([In] bool resizable);

        [DispId(0x108)]
        void WindowSetLeft([In] int left);

        [DispId(0x109)]
        void WindowSetTop([In] int top);

        [DispId(0x10a)]
        void WindowSetWidth([In] int width);

        [DispId(0x10b)]
        void WindowSetHeight([In] int height);

        [DispId(0x107)]
        void WindowClosing([In] bool isChildWindow, [In, Out] ref bool cancel);

        [DispId(0x10c)]
        void ClientToHostWindow([In, Out] ref long cx, [In, Out] ref long cy);

        [DispId(0x10d)]
        void SetSecureLockIcon([In] int secureLockIcon);

        [DispId(270)]
        void FileDownload([In, Out] ref bool cancel);

        [DispId(0x10f)]
        void NavigateError([In, MarshalAs(UnmanagedType.IDispatch)] object pDisp, [In] ref object URL,
            [In] ref object frame, [In] ref object statusCode, [In, Out] ref bool cancel);

        [DispId(0xe1)]
        void PrintTemplateInstantiation([In, MarshalAs(UnmanagedType.IDispatch)] object pDisp);

        [DispId(0xe2)]
        void PrintTemplateTeardown([In, MarshalAs(UnmanagedType.IDispatch)] object pDisp);

        [DispId(0xe3)]
        void UpdatePageStatus([In, MarshalAs(UnmanagedType.IDispatch)] object pDisp, [In] ref object nPage,
            [In] ref object fDone);

        [DispId(0x110)]
        void PrivacyImpactedStateChange([In] bool bImpacted);
    }
}
