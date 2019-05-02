// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client.Extensions.Msal.Providers;

namespace ConsoleTestForTokenProviderChains
{
    class Program
    {
        static void Main(string[] args)
        {
            var lf = new LoggerFactory();
            lf.AddProvider(new ConsoleLoggerProvider(
                new OptionsMonitor<ConsoleLoggerOptions>(
                    new OptionsFactory<ConsoleLoggerOptions>(new List<IConfigureOptions<ConsoleLoggerOptions>>(), new List<IPostConfigureOptions<ConsoleLoggerOptions>>()),
                    new List<IOptionsChangeTokenSource<ConsoleLoggerOptions>>(),
                    new OptionsCache<ConsoleLoggerOptions>())));

            var logger = lf.CreateLogger<DefaultTokenProviderChain>();
            var chain = new DefaultTokenProviderChain(logger: logger);
            var available = chain.IsAvailableAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            logger.Log(LogLevel.Information, available ? "Available" : "Not Available");

            if (available)
            {
                var scopes = new List<string>{Constants.AzureResourceManagerDefaultScope};
                var token = chain.GetTokenAsync(scopes).ConfigureAwait(false).GetAwaiter().GetResult();
                logger.Log(LogLevel.Information, token.AccessToken);
            }
        }
    }
}
