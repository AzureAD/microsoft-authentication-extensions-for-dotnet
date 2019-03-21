// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Identity.Extensions.Msal
{
    /// <summary>
    /// An immutable class containing information required to instantiate storage objects for MSAL caches in various platforms.
    /// </summary>
    public class MsalStorageCreationProperties
    {
        /// <summary>
        /// This constructor is intentionally internal. To get one of these objects use <see cref="MsalStorageCreationPropertiesBuilder.Build"/>.
        /// </summary>
        internal MsalStorageCreationProperties(
            string cacheFileName,
            string cacheDirectory,
            string macKeyChainServiceName,
            string macKeyChainAccountName,
            string keyringSchemaName,
            string keyringCollection,
            string keyringSecretLabel,
            KeyValuePair<string, string> keyringAttribute1,
            KeyValuePair<string, string> keyringAttribute2)
        {
            CacheFileName = cacheFileName;
            CacheDirectory = cacheDirectory;
            MacKeyChainServiceName = macKeyChainServiceName;
            MacKeyChainAccountName = macKeyChainAccountName;
            KeyringSchemaName = keyringSchemaName;
            KeyringCollection = keyringCollection;
            KeyringSecretLabel = keyringSecretLabel;
            KeyringAttribute1 = keyringAttribute1;
            KeyringAttribute2 = keyringAttribute2;
        }

        /// <summary>
        /// The name of the cache file.
        /// </summary>
        public readonly string CacheFileName;

        /// <summary>
        /// The name of the directory containing the cache file.
        /// </summary>
        public readonly string CacheDirectory;

        /// <summary>
        /// The mac keychain service name.
        /// </summary>
        public readonly string MacKeyChainServiceName;

        /// <summary>
        /// The mac keychain account name.
        /// </summary>
        public readonly string MacKeyChainAccountName;

        /// <summary>
        /// The linux keyring schema name.
        /// </summary>
        public readonly string KeyringSchemaName;

        /// <summary>
        /// The linux keyring collection.
        /// </summary>
        public readonly string KeyringCollection;

        /// <summary>
        /// The linux keyring secret label.
        /// </summary>
        public readonly string KeyringSecretLabel;

        /// <summary>
        /// Additional linux keyring attribute.
        /// </summary>
        public readonly KeyValuePair<string, string> KeyringAttribute1;

        /// <summary>
        /// Additional linux keyring attribute.
        /// </summary>
        public readonly KeyValuePair<string, string> KeyringAttribute2;
    }
}
