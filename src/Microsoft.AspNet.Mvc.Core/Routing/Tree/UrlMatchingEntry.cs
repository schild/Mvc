// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNet.Routing;
using Microsoft.AspNet.Routing.Template;

namespace Microsoft.AspNet.Mvc.Routing
{
    public class UrlMatchingEntry
    {
        public IReadOnlyDictionary<string, IRouteConstraint> Constraints { get; set; }

        public int Order { get; set; }

        public string RouteName { get; set; }

        public RouteTemplate RouteTemplate { get; set; }

        public IRouter Target { get; set; }

        public TemplateMatcher TemplateMatcher { get; set; }
    }
}
