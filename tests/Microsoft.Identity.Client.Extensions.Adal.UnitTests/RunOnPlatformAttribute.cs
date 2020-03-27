// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xunit;

namespace Microsoft.Identity.Client.Extensions.Adal.UnitTests
{
    public class RunOnOSXAttribute : RunOnPlatformAttribute
    {
        public RunOnOSXAttribute() : base(OSPlatform.OSX)
        {
        }
    }

    public class RunOnWindowsAttribute : RunOnPlatformAttribute
    {
        public RunOnWindowsAttribute() : base(OSPlatform.Windows)
        {
        }
    }

    public class RunOnLinuxAttribute : RunOnPlatformAttribute
    {
        public RunOnLinuxAttribute() : base(OSPlatform.Linux)
        {
        }
    }

    public class DoNotRunOnLinuxAttribute : DoNotRunOnPlatformAttribute
    {
        public DoNotRunOnLinuxAttribute() : base(OSPlatform.Linux)
        {
        }
    }

    public class RunOnPlatformAttribute : TestMethodAttribute
    {
        private readonly OSPlatform _platform;

        protected RunOnPlatformAttribute(OSPlatform platform)
        {
            _platform = platform;
        }

        public override TestResult[] Execute(ITestMethod testMethod)
        {
            if ((SharedUtilities.IsLinuxPlatform() && _platform != OSPlatform.Linux) ||
                (SharedUtilities.IsMacPlatform() && _platform != OSPlatform.OSX) ||
                (SharedUtilities.IsWindowsPlatform() && _platform != OSPlatform.Windows))
            {
                return new[]
                {
                    new TestResult
                    {
                        Outcome = UnitTestOutcome.Inconclusive,
                        TestFailureException = new AssertInconclusiveException("Skipped on platform")
                    }
                };
            }

            return base.Execute(testMethod);
        }
    }

    public class DoNotRunOnPlatformAttribute : TestMethodAttribute
    {
        private readonly OSPlatform _platform;

        protected DoNotRunOnPlatformAttribute(OSPlatform platform)
        {
            _platform = platform;
        }

        public override TestResult[] Execute(ITestMethod testMethod)
        {
            if ((SharedUtilities.IsLinuxPlatform() && _platform == OSPlatform.Linux) ||
                (SharedUtilities.IsMacPlatform() && _platform == OSPlatform.OSX) ||
                (SharedUtilities.IsWindowsPlatform() && _platform == OSPlatform.Windows))
            {
                return new[]
                {
                    new TestResult
                    {
                        Outcome = UnitTestOutcome.Inconclusive,
                        TestFailureException = new AssertInconclusiveException("Skipped on platform")
                    }
                };
            }

            return base.Execute(testMethod);
        }
    }
}
