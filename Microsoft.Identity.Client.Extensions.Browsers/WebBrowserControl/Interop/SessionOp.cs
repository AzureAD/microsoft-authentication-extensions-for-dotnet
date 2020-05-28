//------------------------------------------------------------------------------
// <copyright file="NativeCodeHelper.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">orfishe</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Identity.Client.Extensions.Browsers.WebBrowserControl.Interop
{
    // Managed enum wrapping the native values to dwSessionOp param of SetQueryNetSessionCount
    // See definition at https://docs.microsoft.com/en-us/previous-versions/windows/internet-explorer/ie-developer/platform-apis/dn720860(v=vs.85)
    enum SessionOp
    {
        SESSION_QUERY = 0,
        SESSION_INCREMENT,
        SESSION_DECREMENT
    }
}
