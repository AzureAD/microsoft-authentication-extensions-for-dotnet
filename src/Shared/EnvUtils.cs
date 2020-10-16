﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;

#if ADAL
namespace Microsoft.Identity.Client.Extensions.Adal
#else //MSAL
namespace Microsoft.Identity.Client.Extensions.Msal
#endif

{
    internal static class EnvUtils
    {
        internal const string TraceLevelEnvVarName = "IDENTITYEXTENSIONTRACELEVEL";

        internal static TraceSource GetNewTraceSource(string sourceName)
        {
#if DEBUG
            var level = SourceLevels.Verbose;
#else
            var level = SourceLevels.Warning;
#endif
            string traceSourceLevelEnvVar = Environment.GetEnvironmentVariable(EnvUtils.TraceLevelEnvVarName);
            if (!string.IsNullOrEmpty(traceSourceLevelEnvVar) &&
                Enum.TryParse(traceSourceLevelEnvVar, ignoreCase: true, result: out SourceLevels result))
            {
                level = result;
            }

            return new TraceSource("Microsoft.Identity.Client.Extensions.TraceSource", level);
        }
    }
}
