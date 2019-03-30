// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Identity.Client;
using Microsoft.Identity.Client.AppConfig;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace Microsoft.Identity.Client.Extensions.Msal.Providers
{
    /// <inheritdoc />
    /// <summary>
    ///     ServicePrincipalProbe looks to the application setting and environment variables to build a ICredentialProvider.
    /// </summary>
    public class ServicePrincipalTokenProvider : ITokenProvider
    {
        private readonly IServicePrincipalConfiguration _config;
        private readonly TraceSource _logger;

        /// <summary>
        /// Create a new instance of a ServicePrincipalProbe
        /// </summary>
        /// <param name="config">optional configuration; if not specified the default configuration will use environment variables</param>
        /// <param name="logger">optional TraceSource for detailed logging information</param>
        public ServicePrincipalTokenProvider(IConfigurationProvider config = null, TraceSource logger = null)
        {
            _logger = logger ?? new TraceSource(nameof(ServicePrincipalTokenProvider));
            config = config ?? new EnvironmentVariablesConfigurationProvider();
            _config = new DefaultServicePrincipalConfiguration(config);
        }

        // Async method lacks 'await' operators and will run synchronously
        /// <inheritdoc />
        public Task<bool> AvailableAsync()
        {
            TraceEvent(TraceEventType.Information,  "checking if provider is available");
            var available = IsClientSecret() || IsClientCertificate();
            TraceEvent(TraceEventType.Information,  $"provider available: {available}");
            return Task.FromResult(available);
        }


        /// <inheritdoc />
        public async Task<IToken> GetTokenAsync(IEnumerable<string> scopes)
        {
            var provider = await ProviderAsync().ConfigureAwait(false);
            TraceEvent(TraceEventType.Information,  $"fetching token");
            return await provider.GetTokenAsync(scopes).ConfigureAwait(false);
        }

        private async Task<InternalServicePrincipalTokenProvider> ProviderAsync()
        {
            var available = await AvailableAsync().ConfigureAwait(false);
            if (!available)
            {
                TraceEvent(TraceEventType.Information,  "provider is not available");
                throw new InvalidOperationException("The required environment variables are not available.");
            }

            var authorityWithTenant = string.Format(CultureInfo.InvariantCulture, AadAuthority.AadCanonicalAuthorityTemplate, _config.Authority, _config.TenantId);

            if (!IsClientCertificate())
            {
                TraceEvent(TraceEventType.Information,  "provider is configured to use client certificates");
                return new InternalServicePrincipalTokenProvider(authorityWithTenant, _config.TenantId, _config.ClientId, _config.ClientSecret);
            }

            X509Certificate2 cert;
            if (!string.IsNullOrWhiteSpace(_config.CertificateBase64))
            {
                TraceEvent(TraceEventType.Information,  $"decoding certificate from base64 directly from environment variable {Constants.AzureCertificateEnvName}");
                // If the certificate is provided as base64 encoded string in env, decode and hydrate a x509 cert
                var decoded = Convert.FromBase64String(_config.CertificateBase64);
                cert = new X509Certificate2(decoded);
            }
            else
            {
                TraceEvent(TraceEventType.Information,  $"using certificate store with name {StoreNameWithDefault} and location {StoreLocationFromEnv}");
                // Try to use the certificate store
                var store = new X509Store(StoreNameWithDefault, StoreLocationFromEnv);
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certs;
                if (!string.IsNullOrEmpty(_config.CertificateSubjectDistinguishedName))
                {
                    TraceEvent(TraceEventType.Information,  $"finding certificates in store by distinguished name {_config.CertificateSubjectDistinguishedName}");
                    certs = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName,
                        _config.CertificateSubjectDistinguishedName, true);
                }
                else
                {
                    TraceEvent(TraceEventType.Information,  $"finding certificates in store by thumbprint {_config.CertificateThumbprint}");
                    certs = store.Certificates.Find(X509FindType.FindByThumbprint, _config.CertificateThumbprint, true);
                }


                if (certs.Count < 1)
                {
                    var msg = !string.IsNullOrEmpty(_config.CertificateSubjectDistinguishedName)
                        ? $"Unable to find certificate with distinguished name '{_config.CertificateSubjectDistinguishedName}' in certificate store named '{StoreNameWithDefault}' and store location {StoreLocationFromEnv}"
                        : $"Unable to find certificate with thumbprint '{_config.CertificateThumbprint}' in certificate store named '{StoreNameWithDefault}' and store location {StoreLocationFromEnv}";
                    throw new InvalidOperationException(msg);
                }

                cert = certs[0];
            }

            return new InternalServicePrincipalTokenProvider(authorityWithTenant, _config.TenantId, _config.ClientId, cert);
        }

        private StoreLocation StoreLocationFromEnv
        {
            get
            {
                var loc = _config.CertificateStoreLocation;
                if (!string.IsNullOrWhiteSpace(loc) && Enum.TryParse(loc, true, out StoreLocation sLocation))
                {
                    return sLocation;
                }

                return StoreLocation.CurrentUser;
            }
        }

        private string StoreNameWithDefault
        {
            get
            {
                var name = _config.CertificateStoreName;
                return string.IsNullOrWhiteSpace(name) ? "My" : name;
            }
        }

        internal bool IsClientSecret()
        {
            TraceEvent(TraceEventType.Information, $"checking if {Constants.AzureTenantIdEnvName}, {Constants.AzureClientIdEnvName} and {Constants.AzureClientSecretEnvName} are set");
            var vars = new List<string> { _config.TenantId, _config.ClientId, _config.ClientSecret };
            var isClientSecret = vars.All(item => !string.IsNullOrWhiteSpace(item));
            TraceEvent(TraceEventType.Information, $"set: {isClientSecret}");
            return isClientSecret;
        }

        internal bool IsClientCertificate()
        {
            TraceEvent(TraceEventType.Information, $"checking if {Constants.AzureTenantIdEnvName}, {Constants.AzureClientIdEnvName} and ({Constants.AzureCertificateEnvName} or {Constants.AzureCertificateThumbprintEnvName} or {Constants.AzureCertificateSubjectDistinguishedNameEnvName}) are set");
            var tenantAndClient = new List<string> { _config.TenantId, _config.ClientId };
            if (tenantAndClient.All(item => !string.IsNullOrWhiteSpace(item)))
            {
                // TODO add certificate distinguished name and test
                return !string.IsNullOrWhiteSpace(_config.CertificateBase64) ||
                       !string.IsNullOrWhiteSpace(_config.CertificateThumbprint) ||
                       !string.IsNullOrWhiteSpace(_config.CertificateSubjectDistinguishedName);
            }

            return false;
        }

        private void TraceEvent(TraceEventType type, string message, [CallerMemberName] string memberName = "")
        {
            _logger?.TraceEvent(type, 0, $"{nameof(ServicePrincipalTokenProvider)}.{memberName} :: {message}");
        }
    }

    /// <summary>
    /// IManagedIdentityConfiguration provides the configurable properties for the ManagedIdentityProbe
    /// </summary>
    internal interface IServicePrincipalConfiguration
    {
        /// <summary>
        /// CertificateBase64 is the base64 encoded representation of an x509 certificate
        /// </summary>
        string CertificateBase64 { get; }

        /// <summary>
        /// CertificateThumbprint is the thumbprint of the certificate in the Windows Certificate Store
        /// </summary>
        string CertificateThumbprint { get; }

        /// <summary>
        /// CertificateSubjectDistinguishedName is the subject distinguished name of the certificate in the Windows Certificate Store
        /// </summary>
        string CertificateSubjectDistinguishedName { get; }

        /// <summary>
        /// CertificateStoreName is the name of the certificate store on Windows where the certificate is stored
        /// </summary>
        string CertificateStoreName { get; }

        /// <summary>
        /// CertificateStoreLocation is the location of the certificate store on Windows where the certificate is stored
        /// </summary>
        string CertificateStoreLocation { get; }

        /// <summary>
        /// TenantId is the AAD TenantID
        /// </summary>
        string TenantId { get; }

        /// <summary>
        /// ClientId is the service principal (application) ID
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// ClientSecret is the service principal (application) string secret
        /// </summary>
        string ClientSecret { get; }

        /// <summary>
        /// Authority is the URI pointing to the AAD endpoint
        /// </summary>
        string Authority { get; }
    }

    internal class DefaultServicePrincipalConfiguration : IServicePrincipalConfiguration
    {
        private readonly IConfigurationProvider _config;

        public DefaultServicePrincipalConfiguration(IConfigurationProvider config)
        {
            _config = config;
        }

        public string ClientId => _config.Get(Constants.AzureClientIdEnvName);

        public string CertificateBase64 => _config.Get(Constants.AzureCertificateEnvName);

        public string CertificateThumbprint => _config.Get(Constants.AzureCertificateThumbprintEnvName);

        public string CertificateStoreName => _config.Get(Constants.AzureCertificateStoreEnvName);

        public string TenantId => _config.Get(Constants.AzureTenantIdEnvName);

        public string ClientSecret => _config.Get(Constants.AzureClientSecretEnvName);

        public string CertificateStoreLocation => _config.Get(Constants.AzureCertificateStoreLocationEnvName);

        public string CertificateSubjectDistinguishedName => _config.Get(Constants.AzureCertificateSubjectDistinguishedNameEnvName);

        public string Authority => string.IsNullOrWhiteSpace(_config.Get(Constants.AadAuthorityEnvName)) ? AadAuthority.DefaultTrustedHost : _config.Get(Constants.AadAuthorityEnvName);
    }

    /// <summary>
    /// ServicePrincipalTokenProvider fetches an AAD token provided Service Principal credentials.
    /// </summary>
    internal class InternalServicePrincipalTokenProvider
    {
        private readonly IConfidentialClientApplication _client;

        internal InternalServicePrincipalTokenProvider(string authority, string tenantId, string clientId, string secret, IMsalHttpClientFactory clientFactory)
        {
            _client = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithTenantId(tenantId)
                .WithAuthority(new Uri(authority))
                .WithClientSecret(secret)
                .WithHttpClientFactory(clientFactory)
                .Build();
        }

        private InternalServicePrincipalTokenProvider(string authority, string tenantId, string clientId, X509Certificate2 cert, IMsalHttpClientFactory clientFactory)
        {
            _client = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithTenantId(tenantId)
                .WithAuthority(new Uri(authority))
                .WithCertificate(cert)
                .WithHttpClientFactory(clientFactory)
                .Build();
        }

        /// <summary>
        ///     ServicePrincipalCredentialProvider constructor to build the provider with a certificate
        /// </summary>
        /// <param name="authority">Hostname of the security token service (STS) from which MSAL.NET will acquire the tokens. Ex: login.microsoftonline.com
        /// </param>
        /// <param name="tenantId">A string representation for a GUID, which is the ID of the tenant where the account resides</param>
        /// <param name="clientId">A string representation for a GUID ClientId (application ID) of the application</param>
        /// <param name="cert">A ClientAssertionCertificate which is the certificate secret for the application</param>
        public InternalServicePrincipalTokenProvider(string authority, string tenantId, string clientId, X509Certificate2 cert)
            : this(authority, tenantId, clientId, cert, null)
        { }

        /// <summary>
        ///     ServicePrincipalCredentialProvider constructor to build the provider with a string secret
        /// </summary>
        /// <param name="authority">Hostname of the security token service (STS) from which MSAL.NET will acquire the tokens. Ex: login.microsoftonline.com
        /// </param>
        /// <param name="tenantId">A string representation for a GUID, which is the ID of the tenant where the account resides</param>
        /// <param name="clientId">A string representation for a GUID ClientId (application ID) of the application</param>
        /// <param name="secret">A string secret for the application</param>
        public InternalServicePrincipalTokenProvider(string authority, string tenantId, string clientId, string secret)
            : this(authority, tenantId, clientId, secret, null)
        { }

        /// <summary>
        ///     GetTokenAsync returns a token for a given set of scopes
        /// </summary>
        /// <param name="scopes">Scopes requested to access a protected API</param>
        /// <returns>A token with expiration</returns>
        public async Task<IToken> GetTokenAsync(IEnumerable<string> scopes)
        {
            var res = await _client.AcquireTokenForClientAsync(scopes).ConfigureAwait(false);
            return new AccessTokenWithExpiration { ExpiresOn = res.ExpiresOn, AccessToken = res.AccessToken };
        }
    }

    internal static class AadAuthority
    {
        public const string DefaultTrustedHost = "login.microsoftonline.com";
        public const string AadCanonicalAuthorityTemplate = "https://{0}/{1}/";
    }
}
