// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Newtonsoft.Json;
using WebAppTestWithAzureSDK.Credentials;

namespace WebAppTestWithAzureSDK.AzureClients
{
    public class EnhancedResourceManagementClient : IResourceManagementClient
    {
        private readonly ResourceManagementClient _client;

        public EnhancedResourceManagementClient(IConfiguration config, ServiceClientCredentials creds)
        {
            _client = new ResourceManagementClient(creds);
            var subId = config.GetValue<string>("AZURE_SUBSCRIPTION_ID");
            if (!string.IsNullOrEmpty(subId))
            {
                _client.SubscriptionId = subId;
            }
            else
            {
                throw new InvalidOperationException("no subscription id set in the environment");
            }
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        public Uri BaseUri { get => _client.BaseUri; set => _client.BaseUri = value; }
        public JsonSerializerSettings SerializationSettings => _client.SerializationSettings;
        public JsonSerializerSettings DeserializationSettings => _client.DeserializationSettings;
        public ServiceClientCredentials Credentials => _client.Credentials;
        public string SubscriptionId { get => _client.SubscriptionId; set => _client.SubscriptionId = value; }
        public string ApiVersion => _client.ApiVersion;
        public string AcceptLanguage { get => _client.AcceptLanguage; set => _client.AcceptLanguage = value; }
        public int? LongRunningOperationRetryTimeout { get => _client.LongRunningOperationRetryTimeout; set => _client.LongRunningOperationRetryTimeout = value; }
        public bool? GenerateClientRequestId { get => _client.GenerateClientRequestId; set => _client.GenerateClientRequestId = value; }
        public IDeploymentsOperations Deployments => _client.Deployments;
        public IProvidersOperations Providers => _client.Providers;
        public IResourcesOperations Resources => _client.Resources;
        public IResourceGroupsOperations ResourceGroups => _client.ResourceGroups;
        public ITagsOperations Tags => _client.Tags;
        public IDeploymentOperations DeploymentOperations => _client.DeploymentOperations;
    }
}
