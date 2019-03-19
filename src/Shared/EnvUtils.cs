using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Identity.Extensions
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
                SourceLevels result;
                if (Enum.TryParse<SourceLevels>(traceSourceLevelEnvVar, ignoreCase: true, result: out result))
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
