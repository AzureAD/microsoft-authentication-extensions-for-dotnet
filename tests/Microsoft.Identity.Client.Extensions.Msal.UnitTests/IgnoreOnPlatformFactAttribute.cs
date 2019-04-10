using System;
using Xunit;

namespace Microsoft.Identity.Client.Extensions.Msal.UnitTests
{
    public class RunOnPlatformFactAttribute : FactAttribute
    {
        public RunOnPlatformFactAttribute(PlatformID platformId)
        {
            if (Environment.OSVersion.Platform != platformId)
            {
                Skip = $"test only runs on {platformId}";
            }
        }
    }
}
