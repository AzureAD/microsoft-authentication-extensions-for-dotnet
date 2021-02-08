// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Identity.Extensions
{
    /// <summary>
    /// An unexpected error occurred in interop-code.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay}")]
    internal class InteropException : Exception
    {
        public InteropException()
            : base() { }

        public InteropException(string message, int errorCode)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public InteropException(string message, int errorCode, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        public InteropException(string message, Win32Exception w32Exception)
            : base(message, w32Exception)
        {
            ErrorCode = w32Exception.NativeErrorCode;
        }

        /// <summary>
        /// Native error code.
        /// </summary>
        public int ErrorCode { get; }

        private string DebuggerDisplay => $"{Message} [0x{ErrorCode:x}]";
    }
}
