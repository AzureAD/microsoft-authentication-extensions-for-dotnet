// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Identity.Client.Extensions.Msal
{
    internal interface ICacheAccessor
    {
        void Clear();

        byte[] Read();

        void Write(byte[] data);
    }
}
