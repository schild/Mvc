// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNet.Mvc.Routing
{
    public class UrlMatchingTree
    {
        public UrlMatchingNode Root { get; } = new UrlMatchingNode(length: 0);
    }
}
