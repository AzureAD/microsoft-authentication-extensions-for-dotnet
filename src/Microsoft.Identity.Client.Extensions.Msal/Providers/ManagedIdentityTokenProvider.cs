// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Identity.Client.Extensions.Msal.Providers
{
    /// <summary>
    ///     ManagedIdentityTokenProvider will look in environment variable to determine if the managed identity provider
    ///     is available. If the managed identity provider is available, the provider will provide AAD tokens using the
    ///     IMDS endpoint.
    /// </summary>
    public class ManagedIdentityTokenProvider : ITokenProvider
    {
        private readonly HttpClient _httpClient;
        private readonly IManagedIdentityConfiguration _config;
        private readonly string _overrideClientId;
        private readonly ILogger _logger;

        internal ManagedIdentityTokenProvider(HttpClient httpClient, IConfiguration config = null,
            string overrideClientId = null, ILogger logger = null)
        {
            _httpClient = httpClient;
            config = config ?? new ConfigurationBuilder().AddEnvironmentVariables().Build();
            _config = new DefaultManagedIdentityConfiguration(config);
            _overrideClientId = overrideClientId;
            _logger = logger;
        }

        /// <summary>
        ///     Create a Managed Identity probe with a specified client identity
        /// </summary>
        /// <param name="config">option configuration structure -- if not supplied, a default environmental configuration is used.</param>
        /// <param name="overrideClientId">override the client identity found in the config for use when querying the Azure IMDS endpoint</param>
        /// <param name="logger">TraceSource logger</param>
        public ManagedIdentityTokenProvider(IConfiguration config = null, string overrideClientId = null, ILogger logger = null)
            : this(null, config, overrideClientId, logger: logger) { }

        /// <inheritdoc />
        /// <summary>
        ///     Check if the probe is available for use in the current environment
        /// </summary>
        /// <returns>True if a credential provider can be built</returns>
        public async Task<bool> AvailableAsync()
        {
            // check App Service MSI
            if (IsAppService())
            {
                Log(Microsoft.Extensions.Logging.LogLevel.Information, "AppService Managed Identity is available");
                return true;
            }
            Log(Microsoft.Extensions.Logging.LogLevel.Information, "AppService Managed Identity is not available");

            try
            {
                Log(Microsoft.Extensions.Logging.LogLevel.Information, "Attempting to fetch test token with Virtual Machine Managed Identity");
                // if service is listening on VM IP check if a token can be acquired
                var provider = BuildInternalProvider(maxRetries: 2, httpClient: _httpClient);
                var token = await provider.GetTokenAsync(new List<string> { Constants.AzureResourceManagerDefaultScope }).ConfigureAwait(false);
                Log(Microsoft.Extensions.Logging.LogLevel.Information, $"provider available: {token != null}");
                return token != null;
            }
            catch (TooManyRetryAttemptsException)
            {
                Log(Microsoft.Extensions.Logging.LogLevel.Information, "Exceeded retry limit for Virtual Machine Managed Identity request");
                return false;
            }
        }

        /// <inheritdoc />
        /// <summary>
        ///     GetTokenAsync returns a token for a given set of scopes
        /// </summary>
        /// <param name="scopes">Scopes requested to access a protected API</param>
        /// <returns>A token with expiration</returns>
        public async Task<IToken> GetTokenAsync(IEnumerable<string> scopes)
        {
            var internalProvider = BuildInternalProvider(httpClient: _httpClient);
            Log(Microsoft.Extensions.Logging.LogLevel.Information, $"Attempting to fetch token with scopes {string.Join(", ", scopes)}");
            return await internalProvider.GetTokenAsync(scopes).ConfigureAwait(false);
        }


        /// <summary>
        /// IsAppService tells us if we are executing within AppService with Managed Identities enabled
        /// </summary>
        /// <returns></returns>
        private bool IsAppService()
        {
            var vars = new List<string> { _config.ManagedIdentitySecret, _config.ManagedIdentityEndpoint };
            return vars.All(item => !string.IsNullOrWhiteSpace(item));
        }

        private InternalManagedIdentityCredentialProvider BuildInternalProvider(int maxRetries = 5, HttpClient httpClient = null)
        {
            var endpoint = IsAppService() ? _config.ManagedIdentityEndpoint : Constants.ManagedIdentityTokenEndpoint;
            Log(Microsoft.Extensions.Logging.LogLevel.Information, "building " + (IsAppService() ? "an AppService " : "a VM") + " Managed Identity provider");
            return new InternalManagedIdentityCredentialProvider(endpoint,
                httpClient: httpClient,
                secret: _config.ManagedIdentitySecret,
                clientId: ClientId,
                maxRetries: maxRetries,
                logger: _logger);
        }

        private string ClientId => string.IsNullOrWhiteSpace(_overrideClientId) ? _config.ClientId : _overrideClientId;

        private void Log(Microsoft.Extensions.Logging.LogLevel level, string message, [CallerMemberName] string memberName = "")
        {
            _logger?.Log(level, $"{nameof(ManagedIdentityTokenProvider)}.{memberName} :: {message}");
        }
    }

    /// <summary>
    ///     InternalManagedIdentityCredentialProvider will fetch AAD JWT tokens from the IMDS endpoint for the default client id or
    ///     a specified client id.
    /// </summary>
    internal class InternalManagedIdentityCredentialProvider
    {
        private static readonly HttpClient DefaultClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(100) // 100 milliseconds -- make sure there is an extremely short timeout to ensure we fail fast
        };

        private readonly ManagedIdentityClient _client;
        private readonly ILogger _logger;

        internal InternalManagedIdentityCredentialProvider(string endpoint, HttpClient httpClient = null,
            string secret = null, string clientId = null, int maxRetries = 5, ILogger logger = null)
        {
            _logger = logger;
            Log(Microsoft.Extensions.Logging.LogLevel.Information, $"built with clientId: {clientId}, maxRetries: {maxRetries}, secret was present {!string.IsNullOrWhiteSpace(secret)}");
            if (string.IsNullOrWhiteSpace(secret))
            {
                _client = new ManagedIdentityVmClient(endpoint, httpClient ?? DefaultClient, clientId: clientId, maxRetries: maxRetries, logger: _logger);
            }
            else
            {
                _client = new ManagedIdentityAppServiceClient(endpoint, secret, httpClient ?? DefaultClient, maxRetries: maxRetries, logger: _logger);
            }
        }

        /// <summary>
        ///     GetTokenAsync returns a token for a given set of scopes
        /// </summary>
        /// <param name="scopes">Scopes requested to access a protected API</param>
        /// <returns>A token with expiration</returns>
        public async Task<IToken> GetTokenAsync(IEnumerable<string> scopes)
        {
            var resourceUriInScopes = scopes?.FirstOrDefault(i => i.EndsWith(@"/.default", StringComparison.OrdinalIgnoreCase));
            if (resourceUriInScopes == null)
            {
                throw new NoResourceUriInScopesException();
            }

            Log(Microsoft.Extensions.Logging.LogLevel.Information, $"fetching token with retry and scopes {string.Join(", ", scopes)}");
            var resourceUri = resourceUriInScopes.Substring(0, resourceUriInScopes.Length - 8);
            return await _client.FetchTokenWithRetryAsync(resourceUri).ConfigureAwait(false);
        }

        private void Log(Microsoft.Extensions.Logging.LogLevel level, string message, [CallerMemberName] string memberName = "")
        {
            _logger?.Log(level, $"{nameof(InternalManagedIdentityCredentialProvider)}.{memberName} :: {message}");
        }
    }

    internal abstract class ManagedIdentityClient
    {
        private readonly int _maxRetries;
        private readonly HttpClient _client;
        protected readonly ILogger Logger;

        internal ManagedIdentityClient(string endpoint, HttpClient client, int maxRetries = 5, ILogger logger = null)
        {
            Endpoint = endpoint;
            _client = client;
            _maxRetries = maxRetries;
            Logger = logger;
        }

        protected abstract HttpRequestMessage BuildTokenRequest(string resourceUri);

        protected abstract DateTimeOffset ParseExpiresOn(TokenResponse tokenResponse);


        public async Task<IToken> FetchTokenWithRetryAsync(string resourceUri)
        {
            var strategy = new RetryWithExponentialBackoff(_maxRetries, 50, 60000);
            HttpResponseMessage res = null;
            await strategy.RunAsync(async () =>
            {
                Log(Microsoft.Extensions.Logging.LogLevel.Information, $"fetching resource uri {resourceUri}");
                var req = BuildTokenRequest(resourceUri);
                res = await _client.SendAsync(req).ConfigureAwait(false);

                var intCode = (int)res.StatusCode;
                Log(Microsoft.Extensions.Logging.LogLevel.Information, $"received status code {intCode}");
                switch (intCode) {
                    case 404:
                    case 429:
                    case var _ when intCode >= 500:
                        throw new TransientManagedIdentityException($"encountered transient managed identity service error with status code {intCode}");
                    case 400:
                        throw new BadRequestManagedIdentityException();
                }
            }).ConfigureAwait(false);

            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            if(string.IsNullOrEmpty(json))
            {
                Log(Microsoft.Extensions.Logging.LogLevel.Information, "received empty body");
                return null;
            }

            var tokenRes = TokenResponse.Parse(json);
            return new AccessTokenWithExpiration { ExpiresOn = ParseExpiresOn(tokenRes), AccessToken = tokenRes.AccessToken };
        }

        protected string Endpoint { get; }

        private void Log(Microsoft.Extensions.Logging.LogLevel level, string message, [CallerMemberName] string memberName = "")
        {
            Logger?.Log(level, $"{nameof(ManagedIdentityClient)}.{memberName} :: {message}");
        }
    }

    internal sealed class ManagedIdentityVmClient : ManagedIdentityClient
    {
        private readonly string _clientId;

        public ManagedIdentityVmClient(string endpoint, HttpClient client, string clientId = null, int maxRetries = 10, ILogger logger = null)
            : base(endpoint, client, maxRetries, logger)
        {
            _clientId = clientId;
        }

        protected override HttpRequestMessage BuildTokenRequest(string resourceUri)
        {
            var clientIdParameter = string.IsNullOrWhiteSpace(_clientId)
                    ? string.Empty :
                    $"&client_id={_clientId}";

            var requestUri = $"{Endpoint}?resource={resourceUri}{clientIdParameter}&api-version={Constants.ManagedIdentityVMApiVersion}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("Metadata", "true");
            Log(Microsoft.Extensions.Logging.LogLevel.Information, $"sending token request: {requestUri}");
            return request;
        }

        protected override DateTimeOffset ParseExpiresOn(TokenResponse tokenResponse)
        {
            var startOfUnixTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

            if (double.TryParse(tokenResponse.ExpiresOn, out var seconds))
            {
                return startOfUnixTime.AddSeconds(seconds);
            }

            Log(Microsoft.Extensions.Logging.LogLevel.Error, $"failed to parse: {tokenResponse.ExpiresOn}");
            throw new FailedParseOfManagedIdentityExpirationException();
        }

        private void Log(Microsoft.Extensions.Logging.LogLevel level, string message, [CallerMemberName] string memberName = "")
        {
            Logger?.Log(level, $"{nameof(ManagedIdentityVmClient)}.{memberName} :: {message}");
        }
    }

    internal sealed class ManagedIdentityAppServiceClient : ManagedIdentityClient
    {
        private readonly string _secret;

        public ManagedIdentityAppServiceClient(string endpoint, string secret, HttpClient client, int maxRetries = 10, ILogger logger = null) : base(endpoint, client, maxRetries, logger)
        {
            _secret = secret;
        }

        protected override HttpRequestMessage BuildTokenRequest(string resourceUri)
        {
            var requestUri = $"{Endpoint}?resource={resourceUri}&api-version={Constants.ManagedIdentityAppServiceApiVersion}";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("Secret", _secret);
            Log(Microsoft.Extensions.Logging.LogLevel.Information, $"sending token request: {requestUri}");
            return request;
        }

        protected override DateTimeOffset ParseExpiresOn(TokenResponse tokenResponse)
        {
            if (DateTimeOffset.TryParse(tokenResponse.ExpiresOn, out var dateTimeOffset))
            {
                return dateTimeOffset;
            }

            Log(Microsoft.Extensions.Logging.LogLevel.Error, $"failed to parse: {tokenResponse.ExpiresOn}");
            throw new FailedParseOfManagedIdentityExpirationException();
        }

        private void Log(Microsoft.Extensions.Logging.LogLevel level, string message, [CallerMemberName] string memberName = "")
        {
            Logger?.Log(level, $"{nameof(ManagedIdentityAppServiceClient)}.{memberName} :: {message}");
        }
    }

    /// <summary>
    /// IManagedIdentityConfiguration provides the configurable properties for the ManagedIdentityProbe
    /// </summary>
    internal interface IManagedIdentityConfiguration
    {
        /// <summary>
        /// ManagedIdentitySecret is the secret for use in Azure AppService
        /// </summary>
        string ManagedIdentitySecret { get; }

        /// <summary>
        /// ManagedIdentityEndpoint is the AppService endpoint
        /// </summary>
        string ManagedIdentityEndpoint { get; }

        /// <summary>
        /// ClientId is the user assigned managed identity for use in VM managed identity
        /// </summary>
        string ClientId { get; }
    }

    internal class DefaultManagedIdentityConfiguration : IManagedIdentityConfiguration
    {
        private readonly IConfiguration _config;

        public DefaultManagedIdentityConfiguration(IConfiguration config)
        {
            _config = config;
        }

        public string ManagedIdentitySecret => _config.GetValue<string>(Constants.ManagedIdentitySecretEnvName);

        public string ManagedIdentityEndpoint => _config.GetValue<string>(Constants.ManagedIdentityEndpointEnvName);

        public string ClientId => _config.GetValue<string>(Constants.AzureClientIdEnvName);
    }

    /// <inheritdoc />
    /// <summary>
    /// NoResourceUriInScopesException is thrown when the managed identity token provider does not find a .default
    /// scope for a resource in the enumeration of scopes.
    /// </summary>
    public class NoResourceUriInScopesException : MsalClientException
    {
        private const string Code = "no_resource_uri_with_slash_.default_in_scopes";
        private const string ErrorMessage = "The scopes provided is either empty or none that end in `/.default`.";

        /// <inheritdoc />
        /// <summary>
        /// Create a NoResourceUriInScopesException
        /// </summary>
        public NoResourceUriInScopesException() : base(Code, ErrorMessage) { }

        /// <summary>
        /// Create a NoResourceUriInScopesException with an error message
        /// </summary>
        public NoResourceUriInScopesException(string errorMessage) : base(Code, errorMessage) { }
    }

    /// <inheritdoc />
    /// <summary>
    /// FailedParseOfManagedIdentityExpirationException is thrown when the managed identity service returns a token
    /// with an expiration we are unable to parse.
    /// </summary>
    public class FailedParseOfManagedIdentityExpirationException : MsalClientException
    {
        private const string Code = "failed_parse_of_managed_identity_token_expiry";
        private const string ErrorMessage = "managed identity service returned a response with an expiration we are unable to parse.";

        /// <inheritdoc />
        /// <summary>
        /// Create a FailedParseOfManagedIdentityExpirationException
        /// </summary>
        public FailedParseOfManagedIdentityExpirationException() : base(Code, ErrorMessage) { }

        /// <summary>
        /// Create a FailedParseOfManagedIdentityExpirationException with an error message
        /// </summary>
        public FailedParseOfManagedIdentityExpirationException(string errorMessage) : base(Code, errorMessage) { }
    }
}
