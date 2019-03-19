using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Identity.Extensions.Msal
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE1006 // Naming Styles
    public class MsalStorageCreationProperties
    {
        internal MsalStorageCreationProperties(
            string msalCacheFileName,
            string macKeyChainServiceName,
            string macKeyChainAccountName,
            string keyringSchemaName,
            string keyringCollection,
            string keyringSecretLabel,
            string keyringAttribute1,
            string keyringAttribute2
            )
        {
            MSALCacheFileName = msalCacheFileName;
            MacKeyChainServiceName = macKeyChainServiceName;
            MacKeyChainAccountName = macKeyChainAccountName;
            KeyringSchemaName = keyringSchemaName;
            KeyringCollection = keyringCollection;
            KeyringSecretLabel = keyringSecretLabel;
            KeyringAttribute1 = keyringAttribute1;
            KeyringAttribute2 = keyringAttribute2;
        }

        public readonly string MSALCacheFileName; //= "msal.cache";
        public readonly string MacKeyChainServiceName; //= "Microsoft.Developer.IdentityService";
        public readonly string MacKeyChainAccountName; //= "MSALCache";
        public readonly string KeyringSchemaName; //= "msal.cache";
        public readonly string KeyringCollection; //= "default";
        public readonly string KeyringSecretLabel; //= "MSALCache";
        public readonly string KeyringAttribute1; //= "Microsoft.Developer.IdentityService";
        public readonly string KeyringAttribute2; //= "1.0.0.0";
    }

    public class MsalStorageCreationPropertiesBuilder
    {
        private readonly string _msalCacheFileName;
        private string _macKeyChainServiceName;
        private string _macKeyChainAccountName;
        private string _keyringSchemaName;
        private string _keyringCollection;
        private string _keyringSecretLabel;
        private string _keyringAttribute1;
        private string _keyringAttribute2;

        public MsalStorageCreationPropertiesBuilder(string cacheFileName)
        {
            _msalCacheFileName = cacheFileName;
        }

        public MsalStorageCreationProperties Build()
        {
            return new MsalStorageCreationProperties(
                _msalCacheFileName,
                _macKeyChainServiceName,
                _macKeyChainAccountName,
                _keyringSchemaName,
                _keyringCollection,
                _keyringSecretLabel,
                _keyringAttribute1,
                _keyringAttribute2);
        }

        public MsalStorageCreationPropertiesBuilder WithMacKeyChainServiceName(string serviceName)
        {
            _macKeyChainServiceName = serviceName;
            return this;
        }

        public MsalStorageCreationPropertiesBuilder WithMacKeyChainAccountName(string accountName)
        {
            _macKeyChainAccountName = accountName;
            return this;
        }

        public MsalStorageCreationPropertiesBuilder WithKeyringSchemaName(string schemaName)
        {
            _keyringSchemaName = schemaName;
            return this;
        }

        public MsalStorageCreationPropertiesBuilder WithKeyringCollection(string keyringCollection)
        {
            _keyringCollection = keyringCollection;
            return this;
        }

        public MsalStorageCreationPropertiesBuilder WithKeyringSecretLabel(string secretLabel)
        {
            _keyringSecretLabel = secretLabel;
            return this;
        }

        public MsalStorageCreationPropertiesBuilder WithKeyringAttributes(string attribute1, string attribute2)
        {
            _keyringAttribute1 = attribute1;
            _keyringAttribute2 = attribute2;
            return this;
        }
    }
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
