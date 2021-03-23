// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Identity.Extensions;
using Microsoft.Identity.Extensions.Mac;
using static Microsoft.Identity.Extensions.Mac.CoreFoundation;
using static Microsoft.Identity.Extensions.Mac.SecurityFramework;

#if ADAL
namespace Microsoft.Identity.Client.Extensions.Adal
#elif MSAL
namespace Microsoft.Identity.Client.Extensions.Msal
#endif
{
    internal class MacOSKeychain
    {
        private static object s_lock = new object();
        private readonly string _namespace;
        private readonly bool _useIosKeyChain;

        #region Constructors

        /// <summary>
        /// Open the default keychain (current user's login keychain).
        /// </summary>
        /// <param name="namespace">Optional namespace to scope credential operations.</param>
        /// <param name="useIosKeyChain">Use iOS KeyChain, which is not compatible with MacOs keychain
        /// <returns>Default keychain.</returns>
        public MacOSKeychain(string @namespace = null, bool useIosKeyChain = false)
        {
            _namespace = @namespace;
            _useIosKeyChain = useIosKeyChain;
        }

        #endregion

        #region ICredentialStore

        public MacOSKeychainCredential Get(string service, string account)
        {
            IntPtr query = IntPtr.Zero;
            IntPtr resultPtr = IntPtr.Zero;
            IntPtr servicePtr = IntPtr.Zero;
            IntPtr accountPtr = IntPtr.Zero;

            try
            {
                query = CFDictionaryCreateMutable(
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero, IntPtr.Zero);

                CFDictionaryAddValue(query, kSecClass, kSecClassGenericPassword);
                CFDictionaryAddValue(query, kSecMatchLimit, kSecMatchLimitOne);
                CFDictionaryAddValue(query, kSecReturnData, kCFBooleanTrue);
                CFDictionaryAddValue(query, kSecReturnAttributes, kCFBooleanTrue);

                UpdateQueryWithServiceAndAccount(service, account, query, ref servicePtr, ref accountPtr);


                int searchResult = SecItemCopyMatching(query, out resultPtr);

                switch (searchResult)
                {
                    case OK:
                        int typeId = CFGetTypeID(resultPtr);
                        Debug.Assert(typeId != CFArrayGetTypeID(), "Returned more than one keychain item in search");
                        if (typeId == CFDictionaryGetTypeID())
                        {
                            return CreateCredentialFromAttributes(resultPtr);
                        }

                        throw new InteropException($"Unknown keychain search result type CFTypeID: {typeId}.", -1);

                    case ErrorSecItemNotFound:
                        return null;

                    default:
                        ThrowIfError(searchResult);
                        return null;
                }
            }
            finally
            {
                if (query != IntPtr.Zero)
                    CFRelease(query);
                if (servicePtr != IntPtr.Zero)
                    CFRelease(servicePtr);
                if (accountPtr != IntPtr.Zero)
                    CFRelease(accountPtr);
                if (resultPtr != IntPtr.Zero)
                    CFRelease(resultPtr);
            }
        }

        public void AddOrUpdate(string service, string account, byte[] secretBytes)
        {

            lock (s_lock)
            {
                if (Get(service, account) != null)
                {
                    Remove(service, account);
                }

                Add(service, account, secretBytes);

            }
        }

        // Fails if item already exists
        public void Add(string service, string account, byte[] secretBytes)
        {
            IntPtr query = IntPtr.Zero;
            IntPtr itemRefPtr = IntPtr.Zero;
            IntPtr servicePtr = IntPtr.Zero;
            IntPtr accountPtr = IntPtr.Zero;

            try
            {
                query = CFDictionaryCreateMutable(
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero, IntPtr.Zero);

                CFDictionaryAddValue(query, kSecClass, kSecClassGenericPassword);
                IntPtr val = CreateCFStringUtf8(secretBytes);
                CFDictionaryAddValue(query, kSecValueData, val);

                UpdateQueryWithServiceAndAccount(service, account, query, ref servicePtr, ref accountPtr);

                // Search for the credential to delete and get the SecKeychainItem ref.
                int addResult = SecItemAdd(query, IntPtr.Zero);

                ThrowIfError(addResult);

            }
            finally
            {
                if (query != IntPtr.Zero)
                    CFRelease(query);
                if (itemRefPtr != IntPtr.Zero)
                    CFRelease(itemRefPtr);
                if (servicePtr != IntPtr.Zero)
                    CFRelease(servicePtr);
                if (accountPtr != IntPtr.Zero)
                    CFRelease(accountPtr);
            }
        }

        public void Update(string service, string account, byte[] newSecretBytes)
        {
            IntPtr query = IntPtr.Zero;
            IntPtr attrList = IntPtr.Zero;
            IntPtr itemRefPtr = IntPtr.Zero;
            IntPtr servicePtr = IntPtr.Zero;
            IntPtr accountPtr = IntPtr.Zero;

            try
            {
                query = CFDictionaryCreateMutable(
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero, IntPtr.Zero);

                attrList = CFDictionaryCreateMutable(
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero, IntPtr.Zero);

                CFDictionaryAddValue(query, kSecClass, kSecClassGenericPassword);
                IntPtr val = CreateCFStringUtf8(newSecretBytes);
                CFDictionaryAddValue(attrList, kSecValueData, val);

                UpdateQueryWithServiceAndAccount(service, account, query, ref servicePtr, ref accountPtr);

                // Search for the credential to delete and get the SecKeychainItem ref.
                int addResult = SecItemUpdate(query, attrList);

                ThrowIfError(addResult);

            }
            finally
            {
                if (query != IntPtr.Zero)
                    CFRelease(query);
                if (itemRefPtr != IntPtr.Zero)
                    CFRelease(itemRefPtr);
                if (servicePtr != IntPtr.Zero)
                    CFRelease(servicePtr);
                if (accountPtr != IntPtr.Zero)
                    CFRelease(accountPtr);
            }
        }

        public bool Remove(string service, string account)
        {
            IntPtr query = IntPtr.Zero;
            IntPtr itemRefPtr = IntPtr.Zero;
            IntPtr servicePtr = IntPtr.Zero;
            IntPtr accountPtr = IntPtr.Zero;

            try
            {
                query = CFDictionaryCreateMutable(
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero, IntPtr.Zero);

                CFDictionaryAddValue(query, kSecClass, kSecClassGenericPassword);
                CFDictionaryAddValue(query, kSecMatchLimit, kSecMatchLimitOne);
                CFDictionaryAddValue(query, kSecReturnRef, kCFBooleanTrue);

                UpdateQueryWithServiceAndAccount(service, account, query, ref servicePtr, ref accountPtr);

                // Search for the credential to delete and get the SecKeychainItem ref.
                int searchResult = SecItemCopyMatching(query, out itemRefPtr);
                switch (searchResult)
                {
                    case OK:
                        // Delete the item
                        ThrowIfError(
                            SecKeychainItemDelete(itemRefPtr)
                        );
                        return true;

                    case ErrorSecItemNotFound:
                        return false;

                    default:
                        ThrowIfError(searchResult);
                        return false;
                }
            }
            finally
            {
                if (query != IntPtr.Zero)
                    CFRelease(query);
                if (itemRefPtr != IntPtr.Zero)
                    CFRelease(itemRefPtr);
                if (servicePtr != IntPtr.Zero)
                    CFRelease(servicePtr);
                if (accountPtr != IntPtr.Zero)
                    CFRelease(accountPtr);
            }
        }

        #endregion
        
        private void UpdateQueryWithServiceAndAccount(string service, string account, IntPtr query, ref IntPtr servicePtr, ref IntPtr accountPtr)
        {
     
if (_useIosKeyChain) {
                CFDictionaryAddValue(query, kSec, servicePtr);

}
            

            if (!string.IsNullOrWhiteSpace(service))
            {
                string fullService = CreateServiceName(service);
                servicePtr = CreateCFStringUtf8(fullService);
                CFDictionaryAddValue(query, kSecAttrService, servicePtr);
            }

            if (!string.IsNullOrWhiteSpace(account))
            {
                accountPtr = CreateCFStringUtf8(account);
                CFDictionaryAddValue(query, kSecAttrAccount, accountPtr);
            }
        }

        private static IntPtr CreateCFStringUtf8(string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            return CreateCFStringUtf8(bytes);
        }

        private static IntPtr CreateCFStringUtf8(byte[] bytes)
        {
            return CFStringCreateWithBytes(IntPtr.Zero,
                bytes, bytes.Length, CFStringEncoding.kCFStringEncodingUTF8, false);
        }

        private static MacOSKeychainCredential CreateCredentialFromAttributes(IntPtr attributes)
        {
            string service = GetStringAttribute(attributes, kSecAttrService);
            string account = GetStringAttribute(attributes, kSecAttrAccount);
            byte[] password = GetByteArrayAtrribute(attributes, kSecValueData);
            string label = GetStringAttribute(attributes, kSecAttrLabel);
            return new MacOSKeychainCredential(service, account, password, label);
        }

        private static byte[] GetByteArrayAtrribute(IntPtr dict, IntPtr key)
        {
            if (dict == IntPtr.Zero)
            {
                return null;
            }

            if (CFDictionaryGetValueIfPresent(dict, key, out IntPtr value) && value != IntPtr.Zero)
            {
                if (CFGetTypeID(value) == CFDataGetTypeID())
                {
                    int length = CFDataGetLength(value);
                    if (length > 0)
                    {
                        IntPtr ptr = CFDataGetBytePtr(value);
                        byte[] managedArray = new byte[length]; // last byte is the string terminator!
                        Marshal.Copy(ptr, managedArray, 0, length);

                        return managedArray;
                    }
                }
            }

            return null;
        }

        private static string GetStringAttribute(IntPtr dict, IntPtr key)
        {
            if (dict == IntPtr.Zero)
            {
                return null;
            }

            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (CFDictionaryGetValueIfPresent(dict, key, out IntPtr value) && value != IntPtr.Zero)
                {
                    if (CFGetTypeID(value) == CFStringGetTypeID())
                    {
                        int stringLength = (int)CFStringGetLength(value);
                        int bufferSize = stringLength + 1;
                        buffer = Marshal.AllocHGlobal(bufferSize);
                        if (CFStringGetCString(value, buffer, bufferSize, CFStringEncoding.kCFStringEncodingUTF8))
                        {
                            return Marshal.PtrToStringAuto(buffer, stringLength);
                        }
                    }

                    if (CFGetTypeID(value) == CFDataGetTypeID())
                    {
                        int length = CFDataGetLength(value);
                        if (length > 0)
                        {
                            IntPtr ptr = CFDataGetBytePtr(value);
                            return Marshal.PtrToStringAuto(ptr, length);
                        }
                    }
                }
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return null;
        }

        private string CreateServiceName(string service)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(_namespace))
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0}:", _namespace);
            }

            sb.Append(service);
            return sb.ToString();
        }
    }
}

