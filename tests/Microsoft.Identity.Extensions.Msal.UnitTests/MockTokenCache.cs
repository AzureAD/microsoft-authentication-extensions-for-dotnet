// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Cache;

#pragma warning disable CS0618 // Type or member is obsolete (CacheData)
namespace Microsoft.Identity.Extensions.Msal.UnitTests
{
    class MockTokenCache : ITokenCache
    {
        private TokenCacheCallback _beforeAccess;
        private TokenCacheCallback _afterAccess;

        public void Deserialize(byte[] msalV2State)
        {
            throw new NotImplementedException();
        }

        public void DeserializeAdalV3(byte[] adalV3State)
        {
            throw new NotImplementedException();
        }

        public void DeserializeMsalV2(byte[] msalV2State)
        {
            throw new NotImplementedException();
        }

        public void DeserializeMsalV3(byte[] msalV3State)
        {
            throw new NotImplementedException();
        }

        public void DeserializeUnifiedAndAdalCache(CacheData cacheData)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize()
        {
            throw new NotImplementedException();
        }

        public byte[] SerializeAdalV3()
        {
            throw new NotImplementedException();
        }

        public byte[] SerializeMsalV2()
        {
            throw new NotImplementedException();
        }

        public byte[] SerializeMsalV3()
        {
            throw new NotImplementedException();
        }

        public CacheData SerializeUnifiedAndAdalCache()
        {
            throw new NotImplementedException();
        }

        public void SetAfterAccess(TokenCacheCallback afterAccess)
        {
            _afterAccess = afterAccess;
        }

        public void SetBeforeAccess(TokenCacheCallback beforeAccess)
        {
            _beforeAccess = beforeAccess;
        }

        public void SetBeforeWrite(TokenCacheCallback beforeWrite)
        {
            throw new NotImplementedException();
        }
    }
}
#pragma warning restore CS0618 // Type or member is obsolete
