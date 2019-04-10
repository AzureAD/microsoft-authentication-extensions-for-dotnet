// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

#if ADAL
namespace Microsoft.Identity.Client.Extensions.Adal
#elif MSAL
namespace Microsoft.Identity.Client.Extensions.Msal
#else // WEB
namespace Microsoft.Identity.Client.Extensions.Web
#endif
{
/// <summary>
/// An incremental builder for <see cref="StorageCreationProperties"/> objects.
/// </summary>
public class StorageCreationPropertiesBuilder
    {
        private readonly string _cacheFileName;
        private readonly string _cacheDirectory;
        private readonly string _clientId;
        private string _macKeyChainServiceName;
        private string _macKeyChainAccountName;
        private string _keyringSchemaName;
        private string _keyringCollection;
        private string _keyringSecretLabel;
        private KeyValuePair<string, string> _keyringAttribute1;
        private KeyValuePair<string, string> _keyringAttribute2;

        /// <summary>
        /// Constructs a new instance of this builder associated with the given cache file.
        /// </summary>
        /// <param name="cacheFileName">The name of the cache file to use when creating or opening storage.</param>
        /// <param name="cacheDirectory">The name of the directory containing the cache file.</param>
        /// <param name="clientId">The client id for the calling application</param>
        public StorageCreationPropertiesBuilder(string cacheFileName, string cacheDirectory, string clientId)
        {
            _cacheFileName = cacheFileName;
            _cacheDirectory = cacheDirectory;
            _clientId = clientId;
        }

        /// <summary>
        /// Returns an immutable instance of <see cref="StorageCreationProperties"/> matching the configuration of this builder.
        /// </summary>
        /// <returns>An immutable instance of <see cref="StorageCreationProperties"/> matching the configuration of this builder.</returns>
        public StorageCreationProperties Build()
        {
            return new StorageCreationProperties(
                _cacheFileName,
                _cacheDirectory,
                _macKeyChainServiceName,
                _macKeyChainAccountName,
                _keyringSchemaName,
                _keyringCollection,
                _keyringSecretLabel,
                _keyringAttribute1,
                _keyringAttribute2,
                _clientId);
        }

        /// <summary>
        /// Augments this builder with mac keychain values and returns the augmented builder.
        /// </summary>
        /// <param name="serviceName">The mac keychain service name</param>
        /// <param name="accountName">The mac keychain account name</param>
        /// <returns>The augmented builder</returns>
        public StorageCreationPropertiesBuilder WithMacKeyChain(string serviceName, string accountName)
        {
            _macKeyChainServiceName = serviceName;
            _macKeyChainAccountName = accountName;
            return this;
        }

        /// <summary>
        /// Augments this builder with linux keyring values and returns the augmented builder.
        /// </summary>
        /// <param name="schemaName">Schema name</param>
        /// <param name="collection">Collection</param>
        /// <param name="secretLabel">Secret label</param>
        /// <param name="attribute1">Additional attribute</param>
        /// <param name="attribute2">Additional attribute</param>
        /// <returns>The augmented builder</returns>
        public StorageCreationPropertiesBuilder WithLinuxKeyring(
            string schemaName,
            string collection,
            string secretLabel,
            KeyValuePair<string, string> attribute1,
            KeyValuePair<string, string> attribute2)
        {
            _keyringSchemaName = schemaName;
            _keyringCollection = collection;
            _keyringSecretLabel = secretLabel;
            _keyringAttribute1 = attribute1;
            _keyringAttribute2 = attribute2;
            return this;
        }
    }
}
