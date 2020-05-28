﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;

namespace Microsoft.Identity.Client.Extensions.Browsers.Experimental.DefaultOSBrowser
{
    internal class MessageAndHttpCode
    {
        public MessageAndHttpCode(HttpStatusCode httpCode, string message)
        {
            HttpCode = httpCode;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public HttpStatusCode HttpCode { get; }
        public string Message { get; }
    }
}
