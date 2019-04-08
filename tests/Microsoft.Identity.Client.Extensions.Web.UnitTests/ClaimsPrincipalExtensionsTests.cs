// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Identity.Client.Extensions.Web.UnitTests
{
    [TestClass]
    public class ClaimsPrincipalExtensionsTests
    {
        [TestMethod]
        public void EnsureGetMsalAccountIdReturnsProperValue()
        {
            ClaimsPrincipal principal = ClaimsPrincipalFactory.FromTenantIdAndObjectId(TestConstants.TenantId, TestConstants.ObjectId);

            string msalAccountId = principal.GetMsalAccountId();
            Assert.AreEqual<string>($"{TestConstants.ObjectId}.{TestConstants.TenantId}", msalAccountId);
        }

        [TestMethod]
        public void EnsureGetObjectIdReturnsProperValue()
        {
            ClaimsPrincipal principal = ClaimsPrincipalFactory.FromTenantIdAndObjectId(TestConstants.TenantId, TestConstants.ObjectId);

            string objectId = principal.GetObjectId();
            Assert.AreEqual<string>(TestConstants.ObjectId, objectId);
        }

        [TestMethod]
        public void EnsureGetTenantIdReturnsProperValue()
        {
            ClaimsPrincipal principal = ClaimsPrincipalFactory.FromTenantIdAndObjectId(TestConstants.TenantId, TestConstants.ObjectId);

            string tenantId = principal.GetTenantId();
            Assert.AreEqual<string>(TestConstants.TenantId, tenantId);
        }

        [TestMethod]
        public void EnsureGetDisplayNameIsNullWithSimpleClaimsPrincipal()
        {
            ClaimsPrincipal principal = ClaimsPrincipalFactory.FromTenantIdAndObjectId(TestConstants.TenantId, TestConstants.ObjectId);

            string displayName = principal.GetDisplayName();
            Assert.IsNull(displayName);
        }

        [TestMethod]
        public void EnsureGetLoginHintIsNullWithSimpleClaimsPrincipal()
        {
            ClaimsPrincipal principal = ClaimsPrincipalFactory.FromTenantIdAndObjectId(TestConstants.TenantId, TestConstants.ObjectId);

            string loginHint = principal.GetLoginHint();
            Assert.IsNull(loginHint);
        }

        // TODO: add tests for ClaimsPrincipal created from IAccount instance...

        [TestMethod]
        public void EnsureGetDomainHintReturnsOrganizationsForNonMsaTenant()
        {
            ClaimsPrincipal principal = ClaimsPrincipalFactory.FromTenantIdAndObjectId(TestConstants.TenantId, TestConstants.ObjectId);
            string domainHint = principal.GetDomainHint();
            Assert.AreEqual<string>("organizations", domainHint);
        }

        [TestMethod]
        public void EnsureGetDomainHintReturnsConsumersForNonTenant()
        {
            ClaimsPrincipal principal = ClaimsPrincipalFactory.FromTenantIdAndObjectId(TestConstants.MsaTenantId, TestConstants.ObjectId);
            string domainHint = principal.GetDomainHint();
            Assert.AreEqual<string>("consumers", domainHint);
        }

        [TestMethod]
        public void EnsureGetDomainHintReturnsNullForEmptyClaimsPrincipal()
        {
            ClaimsPrincipal principal = new ClaimsPrincipal();
            string domainHint = principal.GetDomainHint();
            Assert.IsNull(domainHint);
        }
    }
}
