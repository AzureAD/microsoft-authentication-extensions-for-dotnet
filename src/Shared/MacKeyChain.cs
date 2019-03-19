// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable CA2201 // Do not raise reserved exception types
namespace Microsoft.Identity.Extensions
{
    /// <summary>
    /// APIs for reading, writing and deleting data from Mac KeyChain
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal static class MacKeyChain
    {
        /// <summary>
        /// Writes specifies vaue for given service and account names.
        /// </summary>
        /// <param name="serviceName">Service name.</param>
        /// <param name="accountName">Account name.</param>
        /// <param name="value">Value.</param>
        public static void WriteKey(string serviceName, string accountName, byte[] value)
        {
            IntPtr itemRef = IntPtr.Zero;
            IntPtr valuePtr = IntPtr.Zero;

            try
            {
                // check if the key already exists
                uint valueLength = 0;
                int status = NativeMethods.SecKeychainFindGenericPassword(
                                                                          keychainOrArray: IntPtr.Zero,
                                                                          serviceNameLength: (uint)serviceName.Length,
                                                                          serviceName: serviceName,
                                                                          accountNameLength: (uint)accountName.Length,
                                                                          accountName: accountName,
                                                                          passwordLength: out valueLength,
                                                                          passwordData: out valuePtr,
                                                                          itemRef: out itemRef);

                if (status != NativeMethods.errSecSuccess
                    && status != NativeMethods.errSecItemNotFound)
                {
                    throw new Exception(string.Format(CultureInfo.CurrentCulture, Resources.MacKeyChainFindFailed, status));
                }

                if (itemRef != IntPtr.Zero)
                {
                    // key already exists so update it
                    status = NativeMethods.SecKeychainItemModifyAttributesAndData(
                                                                                  itemRef: itemRef,
                                                                                  attrList: IntPtr.Zero,
                                                                                  length: (uint)value.Length,
                                                                                  data: value);

                    if (status != NativeMethods.errSecSuccess)
                    {
                        throw new Exception(string.Format(CultureInfo.CurrentCulture, Resources.MacKeyChainUpdateFailed, status));
                    }
                }
                else
                {
                    // key doesn't exist. add key value data
                    status = NativeMethods.SecKeychainAddGenericPassword(
                                                                         keychain: IntPtr.Zero,
                                                                         serviceNameLength: (uint)serviceName.Length,
                                                                         serviceName: serviceName,
                                                                         accountNameLength: (uint)accountName.Length,
                                                                         accountName: accountName,
                                                                         passwordLength: (uint)value.Length,
                                                                         passwordData: value,
                                                                         itemRef: out itemRef);

                    if (status != NativeMethods.errSecSuccess)
                    {
                        throw new Exception(string.Format(CultureInfo.CurrentCulture, Resources.MacKeyChainInsertFailed, status));
                    }
                }
            }
            finally
            {
                ReleaseItemRefAndValuePtr(ref itemRef, ref valuePtr);
            }
        }

        /// <summary>
        /// Retrieves value for the specified service and account names.
        /// </summary>
        /// <returns>The key.</returns>
        /// <param name="serviceName">Service name.</param>
        /// <param name="accountName">Account name.</param>
        /// <param name="logger">Logger</param>
        /// <returns>null if key corresponding to given serviceName and accountName is not found</returns>
        public static byte[] RetrieveKey(string serviceName, string accountName, TraceSource logger = null)
        {
            IntPtr itemRef = IntPtr.Zero;
            IntPtr valuePtr = IntPtr.Zero;
            byte[] valueBuffer = null;

            StringBuilder logging = new StringBuilder();
            TraceEventType eventType = TraceEventType.Information;

            try
            {
                // get the key value
                uint valueLength = 0;

                logging.AppendLine(FormattableString.Invariant($"SecKeychainFindGenericPassword, for serviceName {serviceName} and accountName {accountName}"));
                int status = NativeMethods.SecKeychainFindGenericPassword(
                                                                          keychainOrArray: IntPtr.Zero,
                                                                          serviceNameLength: (uint)serviceName.Length,
                                                                          serviceName: serviceName,
                                                                          accountNameLength: (uint)accountName.Length,
                                                                          accountName: accountName,
                                                                          passwordLength: out valueLength,
                                                                          passwordData: out valuePtr,
                                                                          itemRef: out itemRef);

                logging.AppendLine(FormattableString.Invariant($"Status: '{status}'"));

                if (status == NativeMethods.errSecItemNotFound)
                {
                    eventType = TraceEventType.Error;
                    logging.AppendLine(FormattableString.Invariant($"Failed, item not found"));
                    return null;
                }

                if (status != NativeMethods.errSecSuccess)
                {
                    eventType = TraceEventType.Error;
                    logging.AppendLine(FormattableString.Invariant($"Failed, other error {status}"));
                    throw new Exception(string.Format(CultureInfo.CurrentCulture, Resources.MacKeyChainFindFailed, status));
                }

                if (itemRef != IntPtr.Zero)
                {
                    logging.AppendLine(FormattableString.Invariant($"SecKeychainFindGenericPassword succeeded"));
                    valueBuffer = new byte[valueLength];
                    Marshal.Copy(source: valuePtr, destination: valueBuffer, startIndex: 0, length: valueBuffer.Length);
                }
            }
            finally
            {
                ReleaseItemRefAndValuePtr(ref itemRef, ref valuePtr);
                logger?.TraceEvent(eventType, /* id */ 0, logging.ToString());
            }

            return valueBuffer;
        }

        /// <summary>
        /// Deletes the key specified by given service and account names.
        /// </summary>
        /// <param name="serviceName">Service name.</param>
        /// <param name="accountName">Account name.</param>
        public static void DeleteKey(string serviceName, string accountName)
        {
            IntPtr itemRef = IntPtr.Zero;
            IntPtr valuePtr = IntPtr.Zero;

            try
            {
                // check if the key exists
                uint valueLength = 0;
                int status = NativeMethods.SecKeychainFindGenericPassword(
                                                                          keychainOrArray: IntPtr.Zero,
                                                                          serviceNameLength: (uint)serviceName.Length,
                                                                          serviceName: serviceName,
                                                                          accountNameLength: (uint)accountName.Length,
                                                                          accountName: accountName,
                                                                          passwordLength: out valueLength,
                                                                          passwordData: out valuePtr,
                                                                          itemRef: out itemRef);

                if (status == NativeMethods.errSecItemNotFound)
                {
                    return;
                }

                if (status != NativeMethods.errSecSuccess)
                {
                    throw new Exception(string.Format(CultureInfo.CurrentCulture, Resources.MacKeyChainFindFailed, status));
                }

                if (itemRef == IntPtr.Zero)
                {
                    return;
                }

                // key exists so delete it
                status = NativeMethods.SecKeychainItemDelete(itemRef);

                if (status != NativeMethods.errSecSuccess)
                {
                    throw new Exception(string.Format(CultureInfo.CurrentCulture, Resources.MacKeyChainDeleteFailed, status));
                }
            }
            finally
            {
                ReleaseItemRefAndValuePtr(ref itemRef, ref valuePtr);
            }
        }

        private static void ReleaseItemRefAndValuePtr(ref IntPtr itemRef, ref IntPtr valuePtr)
        {
            ReleaseItemRef(ref itemRef);
            ReleaseValuePtr(ref valuePtr);
        }

        private static void ReleaseItemRef(ref IntPtr itemRef)
        {
            if (itemRef != IntPtr.Zero)
            {
                NativeMethods.CFRelease(itemRef);
                itemRef = IntPtr.Zero;
            }
        }

        private static void ReleaseValuePtr(ref IntPtr valuePtr)
        {
            if (valuePtr != IntPtr.Zero)
            {
                NativeMethods.SecKeychainItemFreeContent(attrList: IntPtr.Zero, data: valuePtr);
                valuePtr = IntPtr.Zero;
            }
        }
    }
}
#pragma warning restore CA2201 // Do not raise reserved exception types
