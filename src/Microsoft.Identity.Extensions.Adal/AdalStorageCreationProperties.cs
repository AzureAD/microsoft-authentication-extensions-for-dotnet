using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Identity.Extensions.Adal
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE1006 // Naming Styles
    public class AdalStorageCreationProperties
    {
        internal AdalStorageCreationProperties(
            string adalCacheFileName,
            string macKeyChainServiceName,
            string macKeyChainAccountName,
            string keyringSchemaName,
            string keyringCollection,
            string keyringSecretLabel,
            string keyringAttribute1,
            string keyringAttribute2
            )
        {
            ADALCacheFileName = adalCacheFileName;
            MacKeyChainServiceName = macKeyChainServiceName;
            MacKeyChainAccountName = macKeyChainAccountName;
            KeyringSchemaName = keyringSchemaName;
            KeyringCollection = keyringCollection;
            KeyringSecretLabel = keyringSecretLabel;
            KeyringAttribute1 = keyringAttribute1;
            KeyringAttribute2 = keyringAttribute2;
        }

        public readonly string ADALCacheFileName;
        public readonly string MacKeyChainServiceName;
        public readonly string MacKeyChainAccountName;
        public readonly string KeyringSchemaName;
        public readonly string KeyringCollection;
        public readonly string KeyringSecretLabel;
        public readonly string KeyringAttribute1;
        public readonly string KeyringAttribute2;
    }

    public class AdalStorageCreationPropertiesBuilder
    {
        private readonly string _adalCacheFileName;
        private string _macKeyChainServiceName;
        private string _macKeyChainAccountName;
        private string _keyringSchemaName;
        private string _keyringCollection;
        private string _keyringSecretLabel;
        private string _keyringAttribute1;
        private string _keyringAttribute2;

        public AdalStorageCreationPropertiesBuilder(string cacheFileName)
        {
            _adalCacheFileName = cacheFileName;
        }

        public AdalStorageCreationProperties Build()
        {
            return new AdalStorageCreationProperties(
                _adalCacheFileName,
                _macKeyChainServiceName,
                _macKeyChainAccountName,
                _keyringSchemaName,
                _keyringCollection,
                _keyringSecretLabel,
                _keyringAttribute1,
                _keyringAttribute2);
        }

        public AdalStorageCreationPropertiesBuilder WithMacKeyChainServiceName(string serviceName)
        {
            _macKeyChainServiceName = serviceName;
            return this;
        }

        public AdalStorageCreationPropertiesBuilder WithMacKeyChainAccountName(string accountName)
        {
            _macKeyChainAccountName = accountName;
            return this;
        }

        public AdalStorageCreationPropertiesBuilder WithKeyringSchemaName(string schemaName)
        {
            _keyringSchemaName = schemaName;
            return this;
        }

        public AdalStorageCreationPropertiesBuilder WithKeyringCollection(string keyringCollection)
        {
            _keyringCollection = keyringCollection;
            return this;
        }

        public AdalStorageCreationPropertiesBuilder WithKeyringSecretLabel(string secretLabel)
        {
            _keyringSecretLabel = secretLabel;
            return this;
        }

        public AdalStorageCreationPropertiesBuilder WithKeyringAttributes(string attribute1, string attribute2)
        {
            _keyringAttribute1 = attribute1;
            _keyringAttribute2 = attribute2;
            return this;
        }
    }
}
    #pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
