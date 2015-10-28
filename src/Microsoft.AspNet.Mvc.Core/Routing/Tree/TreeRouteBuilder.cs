// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc.Infrastructure;
using Microsoft.AspNet.Routing;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNet.Mvc.Routing
{
    public class TreeRouteBuilder
    {
        private readonly IRouter _target;
        private readonly List<AttributeRouteLinkGenerationEntry> _generatingEntries;
        private readonly List<UrlMatchingEntry> _matchingEntries;

        private readonly ILogger _logger;
        private readonly ILogger _constraintLogger;

        public TreeRouteBuilder(IRouter target, ILogger routeLogger, ILogger constraintLogger)
        {
            _target = target;
            _generatingEntries = new List<AttributeRouteLinkGenerationEntry>();
            _matchingEntries = new List<UrlMatchingEntry>();

            _logger = routeLogger;
            _constraintLogger = constraintLogger;
        }

        public void Add(AttributeRouteLinkGenerationEntry entry)
        {
            _generatingEntries.Add(entry);
        }

        public void Add(UrlMatchingEntry entry)
        {
            _matchingEntries.Add(entry);
        }

        public TreeRouter Build(int version)
        {
            var trees = new Dictionary<int, UrlMatchingTree>();

            foreach (var entry in _matchingEntries)
            {
                UrlMatchingTree tree;
                if (!trees.TryGetValue(entry.Order, out tree))
                {
                    tree = new UrlMatchingTree();
                    trees.Add(entry.Order, tree);
                }

                AddEntryToTree(tree, entry);
            }

            return new TreeRouter(
                _target,
                trees.Values.ToArray(),
                _generatingEntries,
                _logger,
                _constraintLogger,
                version);
        }

        public void Clear()
        {
            _generatingEntries.Clear();
            _matchingEntries.Clear();
        }

        private void AddEntryToTree(UrlMatchingTree tree, UrlMatchingEntry entry)
        {
            var current = tree.Root;
            for (var i = 0; i < entry.RouteTemplate.Segments.Count; i++)
            {
                var segment = entry.RouteTemplate.Segments[i];
                if (!segment.IsSimple)
                {
                    // Treat complex segments as a constrained parameter
                    if (current.ConstrainedParameters == null)
                    {
                        current.ConstrainedParameters = new UrlMatchingNode(length: i + 1);
                    }

                    current = current.ConstrainedParameters;
                    continue;
                }

                Debug.Assert(segment.Parts.Count == 1);
                var part = segment.Parts[0];
                if (part.IsLiteral)
                {
                    UrlMatchingNode next;
                    if (!current.Literals.TryGetValue(part.Text, out next))
                    {
                        next = new UrlMatchingNode(i + 1);
                        current.Literals.Add(part.Text, next);
                    }

                    current = next;
                    continue;
                }

                if (part.IsParameter && part.InlineConstraints.Any() && !part.IsCatchAll)
                {
                    if (current.ConstrainedParameters == null)
                    {
                        current.ConstrainedParameters = new UrlMatchingNode(length: i + 1);
                    }

                    current = current.ConstrainedParameters;
                    continue;
                }

                if (part.IsParameter && !part.IsCatchAll)
                {
                    if (current.Parameters == null)
                    {
                        current.Parameters = new UrlMatchingNode(length: i + 1);
                    }

                    current = current.Parameters;
                    continue;
                }

                if (part.IsParameter && part.InlineConstraints.Any() && part.IsCatchAll)
                {
                    if (current.ConstrainedCatchAlls == null)
                    {
                        current.ConstrainedCatchAlls = new UrlMatchingNode(length: i + 1);
                    }

                    current = current.ConstrainedCatchAlls;
                    continue;
                }

                if (part.IsParameter && part.IsCatchAll)
                {
                    if (current.CatchAlls == null)
                    {
                        current.CatchAlls = new UrlMatchingNode(length: i + 1);
                    }

                    current = current.CatchAlls;
                    continue;
                }

                Debug.Fail("We shouldn't get here.");
            }

            current.Matches.Add(entry);
        }

        public VirtualPathData GetVirtualPath(VirtualPathContext context)
        {
            throw new NotImplementedException();
        }
    }
}
