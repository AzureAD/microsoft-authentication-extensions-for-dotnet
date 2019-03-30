// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Identity.Client.Extensions.Msal.Providers;

namespace ConsoleTestForTokenProviderChains
{
    class Program
    {
        private static readonly TraceSource s_TraceSource = new TraceSource("ConsoleTestForTokenProviderChains")
        {
            Switch =
            {
                Level = SourceLevels.All
            }
        };

        static void Main(string[] args)
        {
            var consoleTracer = new TextWriterTraceListener(Console.Out)
            {
                Filter = new EventTypeFilter(SourceLevels.All)
            };
            Trace.Listeners.Clear();
            Trace.Listeners.Add(consoleTracer);
            s_TraceSource.Listeners.Add(consoleTracer);

            var chain = new DefaultTokenProviderChain(logger: s_TraceSource);
            var available = chain.AvailableAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            TraceEvent(TraceEventType.Information, available ? "Available" : "Not Available");

            if (available)
            {
                var scopes = new List<string>{Constants.AzureResourceManagerDefaultScope};
                var token = chain.GetTokenAsync(scopes).ConfigureAwait(false).GetAwaiter().GetResult();
                Console.Out.WriteLine(token.AccessToken);
            }
            Trace.Flush();
        }

        private static void TraceEvent(TraceEventType type, string message, [CallerMemberName] string memberName = "")
        {
            s_TraceSource.TraceEvent(type, 0, $"ConsoleTestForTokenProviderChains:{memberName} :: {message}");
        }
    }
}
