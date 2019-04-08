// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Claims;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Client.Extensions.Web.UnitTests
{
    [TestClass]
    public class ClaimsPrincipalFactoryTests
    {
        [TestMethod]
        public void ValidateFromTenantIdAndObjectIdHaveExpectedClaims()
        {
            ClaimsPrincipal principal = ClaimsPrincipalFactory.FromTenantIdAndObjectId(TestConstants.TenantId, TestConstants.ObjectId);

            Assert.IsTrue(principal.HasClaim(x => x.Type == "tid"));
            Assert.IsTrue(principal.HasClaim(x => x.Type == "oid"));

            var tidClaim = principal.FindFirst("tid");
            var oidClaim = principal.FindFirst("oid");

            Assert.AreEqual<string>(TestConstants.TenantId, tidClaim.Value);
            Assert.AreEqual<string>(TestConstants.ObjectId, oidClaim.Value);
        }
    }
}
