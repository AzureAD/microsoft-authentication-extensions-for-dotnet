// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client.Extensions.Web.Resource;
using Microsoft.Identity.Client.Extensions.Web.TokenCacheProviders;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Microsoft.Identity.Client.Extensions.Web
{
    /// <summary>
    /// Extensions for IServiceCollection for startup initialization.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add authentication with Microsoft identity platform v2.0 (AAD v2.0).
        /// This supposes that the configuration files have a section named "AzureAD"
        /// </summary>
        /// <param name="services">Service collection to which to add authentication</param>
        /// <param name="configuration">Configuration</param>
        /// <param name="subscribeToOpenIdConnectMiddlewareDiagnosticsEvents">
        /// Set to true if you want to debug, or just understand the OpenIdConnect events.
        /// </param>
        /// <returns></returns>
        public static IServiceCollection AddAzureAdV2Authentication(
            this IServiceCollection services,
            IConfiguration configuration,
            bool subscribeToOpenIdConnectMiddlewareDiagnosticsEvents = false)
        {
            services.AddAuthentication(AzureADDefaults.AuthenticationScheme)
                .AddAzureAD(options => configuration.Bind("AzureAd", options));

            services.Configure<OpenIdConnectOptions>(AzureADDefaults.OpenIdScheme, options =>
            {
                // Per the code below, this application signs in users in any Work and School
                // accounts and any Microsoft Personal Accounts.
                // If you want to direct Azure AD to restrict the users that can sign-in, change 
                // the tenant value of the appsettings.json file in the following way:
                // - only Work and School accounts => 'organizations'
                // - only Microsoft Personal accounts => 'consumers'
                // - Work and School and Personal accounts => 'common'
                // If you want to restrict the users that can sign-in to only one tenant
                // set the tenant value in the appsettings.json file to the tenant ID 
                // or domain of this organization
                options.Authority = options.Authority + "/v2.0/";

                // If you want to restrict the users that can sign-in to several organizations
                // Set the tenant value in the appsettings.json file to 'organizations', and add the
                // issuers you want to accept to options.TokenValidationParameters.ValidIssuers collection
                options.TokenValidationParameters.IssuerValidator = AadIssuerValidator.ForAadInstance(options.Authority).ValidateAadIssuer;

                // Set the nameClaimType to be preferred_username.
                // This change is needed because certain token claims from Azure AD V1 endpoint 
                // (on which the original .NET core template is based) are different than Azure AD V2 endpoint. 
                // For more details see [ID Tokens](https://docs.microsoft.com/en-us/azure/active-directory/develop/id-tokens) 
                // and [Access Tokens](https://docs.microsoft.com/en-us/azure/active-directory/develop/access-tokens)
                options.TokenValidationParameters.NameClaimType = "preferred_username";

                // Handling the sign-out: removing the account from MSAL.NET cache
                options.Events.OnRedirectToIdentityProviderForSignOut = context =>
                {
                    var user = context.HttpContext.User;

                    // Avoid displaying the select account dialog
                    context.ProtocolMessage.LoginHint = user.GetLoginHint();
                    context.ProtocolMessage.DomainHint = user.GetDomainHint();
                    return Task.FromResult(0);
                };

                // Avoids having users being presented the select account dialog when they are already signed-in
                // for instance when going through incremental consent 
                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    var login = context.Properties.GetParameter<string>(OpenIdConnectParameterNames.LoginHint);
                    if (!string.IsNullOrWhiteSpace(login))
                    {
                        context.ProtocolMessage.LoginHint = login;
                        context.ProtocolMessage.DomainHint = context.Properties.GetParameter<string>(
                            OpenIdConnectParameterNames.DomainHint);

                        // delete the loginhint and domainHint from the Properties when we are done otherwise 
                        // it will take up extra space in the cookie.
                        context.Properties.Parameters.Remove(OpenIdConnectParameterNames.LoginHint);
                        context.Properties.Parameters.Remove(OpenIdConnectParameterNames.DomainHint);
                    }

                    // Additional claims
                    if (context.Properties.Items.ContainsKey(OidcConstants.AdditionalClaims))
                    {
                        context.ProtocolMessage.SetParameter(
                            OidcConstants.AdditionalClaims,
                            context.Properties.Items[OidcConstants.AdditionalClaims]);
                    }

                    return Task.FromResult(0);
                };

                if (subscribeToOpenIdConnectMiddlewareDiagnosticsEvents)
                {
                    OpenIdConnectMiddlewareDiagnostics.Subscribe(options.Events);
                }
            });
            return services;
        }

        /// <summary>
        /// Add MSAL support to the Web App or Web API
        /// </summary>
        /// <param name="services">Service collection to which to add authentication</param>
        /// <param name="initialScopes">Initial scopes to request at sign-in</param>
        /// <returns></returns>
        public static IServiceCollection AddMsal(this IServiceCollection services, IEnumerable<string> initialScopes)
        {
            services.AddTokenAcquisition();

            services.Configure<OpenIdConnectOptions>(AzureADDefaults.OpenIdScheme, options =>
            {
                // Response type
                options.ResponseType = OpenIdConnectResponseType.CodeIdToken;

                // This scope is needed to get a refresh token when users sign-in with their Microsoft personal accounts
                // (it's required by MSAL.NET and automatically provided when users sign-in with work or school accounts)
                options.Scope.Add(OidcConstants.ScopeOfflineAccess);
                if (initialScopes != null)
                {
                    foreach (string scope in initialScopes)
                    {
                        if (!options.Scope.Contains(scope))
                        {
                            options.Scope.Add(scope);
                        }
                    }
                }

                // Handling the auth redemption by MSAL.NET so that a token is available in the token cache
                // where it will be usable from Controllers later (through the TokenAcquisition service)
                var handler = options.Events.OnAuthorizationCodeReceived;
                options.Events.OnAuthorizationCodeReceived = async context =>
                {
                    var tokenAcquisition = context.HttpContext.RequestServices.GetRequiredService<ITokenAcquisition>();
                    await tokenAcquisition.AddAccountToCacheFromAuthorizationCodeAsync(context, options.Scope).ConfigureAwait(false);
                    await handler(context).ConfigureAwait(false);
                };

                // Handling the sign-out: removing the account from MSAL.NET cache
                options.Events.OnRedirectToIdentityProviderForSignOut = async context =>
                {
                    // Remove the account from MSAL.NET token cache
                    var tokenAcquisition = context.HttpContext.RequestServices.GetRequiredService<ITokenAcquisition>();
                    await tokenAcquisition.RemoveAccountAsync(context).ConfigureAwait(false);
                };
            });
            return services;
        }

        /// <summary>
        /// Protects the Web API with Microsoft Identity Platform v2.0 (AAD v2.0)
        /// This supposes that the configuration files have a section named "AzureAD"
        /// </summary>
        /// <param name="services">Service collection to which to add authentication</param>
        /// <param name="configuration">Configuration</param>
        /// <param name="subscribeToJwtBearerMiddlewareDiagnosticsEvents">
        /// Set to true if you want to debug, or just understand the JwtBearer events.
        /// </param>
        /// <returns></returns>
        public static IServiceCollection AddProtectWebApiWithMicrosoftIdentityPlatformV2(
            this IServiceCollection services,
            IConfiguration configuration,
            bool subscribeToJwtBearerMiddlewareDiagnosticsEvents = false)
        {
            services.AddAuthentication(AzureADDefaults.JwtBearerAuthenticationScheme)
                    .AddAzureADBearer(options => configuration.Bind("AzureAd", options));

            services.AddSession();

            // Added
            services.Configure<JwtBearerOptions>(AzureADDefaults.JwtBearerAuthenticationScheme, options =>
            {
                // This is an Azure AD v2.0 Web API
                options.Authority += "/v2.0";

                // The valid audiences are both the Client ID (options.Audience) and api://{ClientID}
                options.TokenValidationParameters.ValidAudiences = new string[]
                {
                    options.Audience, $"api://{options.Audience}"
                };

                // Instead of using the default validation (validating against a single tenant, as we do in line of business apps),
                // we inject our own multitenant validation logic (which even accepts both V1 and V2 tokens)
                options.TokenValidationParameters.IssuerValidator = AadIssuerValidator.ForAadInstance(options.Authority).ValidateAadIssuer;

                // When an access token for our own Web API is validated, we add it to MSAL.NET's cache so that it can
                // be used from the controllers.
                options.Events = new JwtBearerEvents();

                if (subscribeToJwtBearerMiddlewareDiagnosticsEvents)
                {
                    options.Events = JwtBearerMiddlewareDiagnostics.Subscribe(options.Events);
                }
            });

            return services;
        }

        /// <summary>
        /// Protects the Web API with Microsoft Identity Platform v2.0 (AAD v2.0)
        /// This supposes that the configuration files have a section named "AzureAD"
        /// </summary>
        /// <param name="services">Service collection to which to add authentication</param>
        /// <param name="configuration">Configuration</param>
        /// <param name="scopes"></param>
        /// <returns></returns>
        public static IServiceCollection AddProtectedApiCallsWebApis(
            this IServiceCollection services,
            IConfiguration configuration,
            IEnumerable<string> scopes)
        {
            services.AddTokenAcquisition();
            services.Configure<JwtBearerOptions>(AzureADDefaults.JwtBearerAuthenticationScheme, options =>
            {
                options.Events.OnTokenValidated = async context =>
                {
                    var tokenAcquisition = context.HttpContext.RequestServices.GetRequiredService<ITokenAcquisition>();
                    context.Success();

                    // Adds the token to the cache, and also handles the incremental consent and claim challenges
                    await tokenAcquisition.AddAccountToCacheFromJwtAsync(context, scopes).ConfigureAwait(false);
                };
            });
            return services;
        }

        /// <summary>
        /// Add the token acquisition service.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>the service collection</returns>
        /// <example>
        /// This method is typically called from the Startup.ConfigureServices(IServiceCollection services)
        /// Note that the implementation of the token cache can be chosen separately.
        ///
        /// <code>
        /// // Token acquisition service and its cache implementation as a session cache
        /// services.AddTokenAcquisition()
        /// .AddDistributedMemoryCache()
        /// .AddSession()
        /// .AddSessionBasedTokenCache()
        ///  ;
        /// </code>
        /// </example>
        public static IServiceCollection AddTokenAcquisition(this IServiceCollection services)
        {
            // Token acquisition service
            services.AddScoped<ITokenAcquisition>(factory =>
            {
                var config = factory.GetRequiredService<IConfiguration>();
                var apptokencacheprovider = factory.GetService<IMsalAppTokenCacheProvider>();
                var usertokencacheprovider = factory.GetService<IMsalUserTokenCacheProvider>();

                return new TokenAcquisition(config, apptokencacheprovider, usertokencacheprovider);
            });

            return services;
        }
    }
}
