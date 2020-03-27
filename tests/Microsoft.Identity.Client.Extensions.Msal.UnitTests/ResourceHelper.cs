// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.IO;

namespace Microsoft.Identity.Client.Extensions.Msal.UnitTests
{
    public class ResourceHelper
    {
        /// <summary>
        /// Gets the relative path to a test resource. Resource should be using DeploymentItem (desktop) or
        /// by setting Copy to Output Directory to Always (other platforms)
        /// </summary>
        /// <remarks>
        /// This is just a simple workaround for DeploymentItem not being implemented in mstest on netcore
        /// Tests seems to run from the bin directory and not from a TestRun dir on netcore
        /// Assumes resources are in a Resources dir.
        /// </remarks>
        public static string GetTestResourceRelativePath(string resourceName)
        {

#if NET472
            return resourceName;
#else
            return Path.Combine("Resources", resourceName);
#endif
        }
    }
}
