//------------------------------------------------------------------------------
// <copyright file="POINT.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Managed defintion for the native POINT structure
    // See internal source at https://referencesource.microsoft.com/#system.windows.forms/winforms/Managed/System/WinForms/NativeMethods.cs
    [StructLayout(LayoutKind.Sequential)]
    sealed class POINT
    {
        public int x;
        public int y;

        public POINT()
        {
        }
        public POINT(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }
}
