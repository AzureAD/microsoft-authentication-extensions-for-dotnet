// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Identity.Extensions
{
    #pragma warning disable CS0649

    /// <summary>
    /// Error returned by libsecret library if saving or retrieving fails
    /// https://developer.gnome.org/glib/stable/glib-Error-Reporting.html
    /// </summary>
    internal struct GError
    {
        /// <summary>
        /// error domain
        /// </summary>
        public uint Domain;

        /// <summary>
        /// error code
        /// </summary>
        public int Code;

        /// <summary>
        /// detailed error message
        /// </summary>
        public string Message;
    }
}
