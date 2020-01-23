// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;

namespace Microsoft.Identity.Client.Extensions.Msal.UnitTests
{
    public class TraceStringListener : TraceListener
    {
        private readonly StringBuilder _log = new StringBuilder();

        public string CurrentLog => _log.ToString();

        public override void Write(string message)
        {
            _log.Append(message);
        }

        public override void WriteLine(string message)
        {
            _log.AppendLine(message);
        }
    }
}
