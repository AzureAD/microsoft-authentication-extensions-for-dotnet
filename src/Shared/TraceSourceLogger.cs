// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

#if ADAL
namespace Microsoft.Identity.Client.Extensions.Adal
#elif MSAL
namespace Microsoft.Identity.Client.Extensions.Msal
#else // WEB
namespace Microsoft.Identity.Client.Extensions.Web
#endif
{
    /// <summary>
    /// 
    /// </summary>
    public class TraceSourceLogger
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="traceSource"></param>
        public TraceSourceLogger(TraceSource traceSource)
        {
            Source = traceSource;
        }

        /// <summary>
        /// 
        /// </summary>
        public TraceSource Source { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void LogInformation(string message)
        {
            Source.TraceEvent(TraceEventType.Information, /*id*/ 0, message);
        }

        /// <summary>
        /// 
        /// </summary>
        public void LogError(string message)
        {
            Source.TraceEvent(TraceEventType.Error, /*id*/ 0, message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void LogWarning(string message)
        {
            Source.TraceEvent(TraceEventType.Warning, /*id*/ 0, message);
        }
    }
}
