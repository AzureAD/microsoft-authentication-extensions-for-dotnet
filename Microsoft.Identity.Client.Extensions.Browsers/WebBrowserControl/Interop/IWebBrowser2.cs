//------------------------------------------------------------------------------
// <copyright file="IWebBrowser2.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Trimmed managed defintion for the native IWebBrowser2 COM interface
    // See internal source at https://referencesource.microsoft.com/#System.Windows.Forms/winforms/Managed/System/WinForms/UnsafeNativeMethods.cs
    [ComImport]
    [Guid("D30C1661-CDAF-11D0-8A3E-00C04FC9E26E")]
#pragma warning disable CS0618
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
#pragma warning restore CS0618
    interface IWebBrowser2
    {
        [DispId(0xcb)]
        object Document { [return: MarshalAs(UnmanagedType.IDispatch)] [DispId(0xcb)] get; }

        [DispId(0x227)]
        bool Silent { [param: MarshalAs(UnmanagedType.Bool)] [DispId(0x227)] set; }
    }
}
