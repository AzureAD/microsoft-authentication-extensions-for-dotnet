// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

#if ADAL
namespace Microsoft.Identity.Extensions.Adal
#elif MSAL
namespace Microsoft.Identity.Extensions.Msal
#else // WEB
namespace Microsoft.Identity.Extensions.Web
#endif
{
internal static class EnvUtils
    {
        // Trace level environment variable. Must be in sync with TraceLevelEnvironmentVariable in src/Node/logger.ts
        internal const string TraceLevelEnvVarName = "SERVICEHUBTRACELEVEL";

        internal static TraceSource GetNewTraceSource(string sourceName)
        {
#if DEBUG
            var level = SourceLevels.Verbose;
#else
            var level = SourceLevels.Warning;
#endif
            string traceSourceLevelEnvVar = Environment.GetEnvironmentVariable(EnvUtils.TraceLevelEnvVarName);
            if (!string.IsNullOrEmpty(traceSourceLevelEnvVar))
            {
                if (Enum.TryParse<SourceLevels>(traceSourceLevelEnvVar, ignoreCase: true, result: out SourceLevels result))
                {
                    level = result;
                }
            }

            return new TraceSource("TemporaryTraceSource_DoNotFinishPRWithThisStillHere", level);

            /*

            var traceSource = new ServiceHubTraceSource(sourceName, level);
            traceSource.Listeners.Remove("Default");
            bool rollingEnabled = !(traceSource.Switch.Level == SourceLevels.Verbose || traceSource.Switch.Level == SourceLevels.All);
            traceSource.Listeners.Add(new FileTraceListener(sourceName, rollingEnabled));

            return traceSource;

             */
        }
    }
}
