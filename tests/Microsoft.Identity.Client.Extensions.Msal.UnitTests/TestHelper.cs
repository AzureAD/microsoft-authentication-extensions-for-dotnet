// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Identity.Client.Extensions.Msal.UnitTests
{
    public statuc class TestHelper
    {

        public static string GetOs()
        {
            if (SharedUtilities.IsLinuxPlatform())
            {
                return "Linux";
            }

            if (SharedUtilities.IsMacPlatform())
            {
                return "Mac";
            }

            if (SharedUtilities.IsWindowsPlatform())
            {
                return "Windows";
            }

            throw new InvalidOperationException("Unknown");
        }
    }
}
