//------------------------------------------------------------------------------
// <copyright file="COMRECT.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

using System.Drawing;
using System.Runtime.InteropServices;

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Managed defintion for the native COMRECT structure
    // See internal source at https://referencesource.microsoft.com/#system.windows.forms/winforms/Managed/System/WinForms/NativeMethods.cs
    [StructLayout(LayoutKind.Sequential)]
    sealed class COMRECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;

        public COMRECT()
        {
        }
        public COMRECT(Rectangle r)
        {
            left = r.X;
            top = r.Y;
            right = r.Right;
            bottom = r.Bottom;
        }
        public COMRECT(int left, int top, int right, int bottom)
        {
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
        }

        public static COMRECT FromXYWH(int x, int y, int width, int height)
        {
            return new COMRECT(x, y, x + width, y + height);
        }
    }
}
