// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Identity.Client.Extensions.Web.Resource
{
    /// <summary>
    /// Generic class that validates token issuer from the provided Azure AD authority
    /// </summary>
    public class AadIssuerValidator
    {
        /// <summary>
        /// A list of all Issuers across the various Azure AD instances
        /// </summary>
        private readonly SortedSet<string> _issuerAliases;

        private const string FallBackAuthority = "https://login.microsoftonline.com/";

        private static readonly IDictionary<string, AadIssuerValidator> s_issuerValidators = new Dictionary<string, AadIssuerValidator>();

        private AadIssuerValidator(IEnumerable<string> aliases)
        {
            _issuerAliases = new SortedSet<string>(aliases);
        }

        /// <summary>
        /// Retrieves the AadIssuerValidator for a given authority
        /// </summary>
        /// <param name="aadAuthority"></param>
        /// <returns></returns>
        public static AadIssuerValidator ForAadInstance(string aadAuthority)
        {
            if (s_issuerValidators.ContainsKey(aadAuthority))
            {
                return s_issuerValidators[aadAuthority];
            }
            else
            {
                string authorityHost = new Uri(aadAuthority).Authority;
                // In the constructor, we hit the Azure AD issuer metadata endpoint and cache the aliases. The data is cached for 24 hrs.
                string azureADIssuerMetadataUrl = "https://login.microsoftonline.com/common/discovery/instance?authorization_endpoint=https://login.microsoftonline.com/common/oauth2/v2.0/authorize&api-version=1.1";
                ConfigurationManager<IssuerMetadata> configManager = new ConfigurationManager<IssuerMetadata>(azureADIssuerMetadataUrl, new IssuerConfigurationRetriever());
                IssuerMetadata issuerMetadata = configManager.GetConfigurationAsync().Result;

                // Add issuer aliases of the chosen authority
                string authority = authorityHost ?? FallBackAuthority;
                var aliases = issuerMetadata.Metadata.Where(m => m.Aliases.Any(a => a == authority)).SelectMany(m => m.Aliases).Distinct();
                AadIssuerValidator issuerValidator = new AadIssuerValidator(aliases);

                s_issuerValidators.Add(authority, issuerValidator);
                return issuerValidator;
            }
        }

        /// <summary>
        /// Validate the issuer for multi-tenant applications of various audience (Work and School account, or Work and School accounts +
        /// Personal accounts)
        /// </summary>
        /// <param name="issuer">Issuer to validate (will be tenanted)</param>
        /// <param name="securityToken">Received Security Token</param>
        /// <param name="validationParameters">Token Validation parameters</param>
        /// <remarks>The issuer is considered as valid if it has the same http scheme and authority as the
        /// authority from the configuration file, has a tenant Id, and optionally v2.0 (this web api
        /// accepts both V1 and V2 tokens).
        /// Authority aliasing is also taken into account</remarks>
        /// <returns>The <c>issuer</c> if it's valid, or otherwise <c>SecurityTokenInvalidIssuerException</c> is thrown</returns>
        public string ValidateAadIssuer(string issuer, SecurityToken securityToken, TokenValidationParameters validationParameters)
        {
            JwtSecurityToken jwtToken = securityToken as JwtSecurityToken;
            if (jwtToken == null)
            {
                throw new ArgumentNullException(nameof(securityToken), $"{nameof(securityToken)} cannot be null.");
            }

            if (validationParameters == null)
            {
                throw new ArgumentNullException(nameof(validationParameters), $"{nameof(validationParameters)} cannot be null.");
            }

            string tenantId = GetTenantIdFromClaims(jwtToken);
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new SecurityTokenInvalidIssuerException("Neither `tid` nor `tenantId` claim is present in the token obtained from Microsoft Identity Platform.");
            }

            // Build a list of valid tenanted issuers from the provided TokenValidationParameters.
            List<string> allValidTenantedIssuers = new List<string>();

            IEnumerable<string> validIssuers = validationParameters.ValidIssuers;
            if (validIssuers != null)
            {
                allValidTenantedIssuers.AddRange(validIssuers.Select(i => TenantedIssuer(i, tenantId)));
            }

            if (validationParameters.ValidIssuer != null)
            {
                allValidTenantedIssuers.Add(TenantedIssuer(validationParameters.ValidIssuer, tenantId));
            }

            // Looking for a valid issuer which authority would be one of the aliases of the authority declared in the
            // Web app / Web API, and which tenantId would be the one for the token
            foreach (string validIssuer in allValidTenantedIssuers)
            {
                Uri uri = new Uri(validIssuer);
                if (_issuerAliases.Contains(uri.Authority))
                {
                    string trimmedLocalPath = uri.LocalPath.Trim('/');
                    if (trimmedLocalPath == tenantId || trimmedLocalPath == $"{tenantId}/v2.0")
                    {
                        return issuer;
                    }
                }
            }

            // If a valid issuer is not found, throw
            throw new SecurityTokenInvalidIssuerException("Issuer does not match any of the valid issuers provided for this application.");
        }

        /// <summary>Gets the tenant id from claims.</summary>
        /// <param name="jwtToken">The JWT token with the claims collection.</param>
        /// <returns>A string containing tenantId, if found or an empty string</returns>
        private string GetTenantIdFromClaims(JwtSecurityToken jwtToken)
        {
            string tenantId;

            // Extract the tenant Id from the claims
            tenantId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimConstants.TidKey)?.Value;

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                tenantId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimConstants.TenantId)?.Value;
            }

            return tenantId;
        }

        private static string TenantedIssuer(string i, string tenantId)
        {
            return i.Replace("{tenantid}", tenantId);
        }
    }
}
