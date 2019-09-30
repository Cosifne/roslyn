﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.ExternalAccess.UnitTesting.Api
{
    internal readonly struct UnitTestingPersistentStorageLocationChangingEventArgsWrapper
    {
        internal UnitTestingPersistentStorageLocationChangingEventArgsWrapper(
            PersistentStorageLocationChangingEventArgs underlyingObject)
            => UnderlyingObject = underlyingObject;

        internal PersistentStorageLocationChangingEventArgs UnderlyingObject { get; }
    }
}
