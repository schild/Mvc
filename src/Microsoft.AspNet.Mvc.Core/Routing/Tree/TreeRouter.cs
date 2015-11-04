// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc.Core;
using Microsoft.AspNet.Mvc.Internal.Routing;
using Microsoft.AspNet.Mvc.Logging;
using Microsoft.AspNet.Routing;
using Microsoft.AspNet.Routing.Internal;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNet.Mvc.Routing
{
    public class TreeRouter : IRouter
    {
        private readonly IRouter _next;
        private readonly LinkGenerationDecisionTree _linkGenerationTree;
        private readonly UrlMatchingTree[] _trees;
        private readonly IDictionary<string, AttributeRouteLinkGenerationEntry> _namedEntries;

        // Left as an exercise to the reader.
        private readonly ILogger _logger;
        private readonly ILogger _constraintLogger;

        public TreeRouter(
            IRouter next,
            UrlMatchingTree[] trees,
            IEnumerable<AttributeRouteLinkGenerationEntry> linkGenerationEntries,
            ILogger routeLogger,
            ILogger constraintLogger,
            int version)
        {
            _next = next;
            _trees = trees;
            _logger = routeLogger;
            _constraintLogger = constraintLogger;

            var namedEntries = new Dictionary<string, AttributeRouteLinkGenerationEntry>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var entry in linkGenerationEntries)
            {
                // Skip unnamed entries
                if (entry.Name == null)
                {
                    continue;
                }

                // We only need to keep one AttributeRouteLinkGenerationEntry per route template
                // so in case two entries have the same name and the same template we only keep
                // the first entry.
                AttributeRouteLinkGenerationEntry namedEntry = null;
                if (namedEntries.TryGetValue(entry.Name, out namedEntry) &&
                    !namedEntry.TemplateText.Equals(entry.TemplateText, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        Resources.FormatAttributeRoute_DifferentLinkGenerationEntries_SameName(entry.Name),
                        nameof(linkGenerationEntries));
                }
                else if (namedEntry == null)
                {
                    namedEntries.Add(entry.Name, entry);
                }
            }

            _namedEntries = namedEntries;

            // The decision tree will take care of ordering for these entries.
            _linkGenerationTree = new LinkGenerationDecisionTree(linkGenerationEntries.ToArray());

            Version = version;
        }

        public int Version { get; }

        public async Task RouteAsync(RouteContext context)
        {
            foreach (var tree in _trees)
            {
                var tokenizer = new PathTokenizer(context.HttpContext.Request.Path);
                var enumerator = tokenizer.GetEnumerator();
                var current = tree.Root;

                var treeEnumerator = new TreeEnumerator(current, tokenizer);

                while(treeEnumerator.MoveNext())
                {
                    var node = treeEnumerator.Current;
                    foreach (var item in node.Matches)
                    {
                        // We may want to build something more efficient than TemplateMatcher.
                        // We already test all the literals, and that the shape matches, and that doesn't
                        // need to be redone.
                        var values = item.TemplateMatcher.Match(context.HttpContext.Request.Path);
                        if (values == null)
                        {
                            continue;
                        }

                        var match = new TemplateMatch(item, values);

                        var oldRouteData = context.RouteData;

                        var newRouteData = new RouteData(oldRouteData);

                        newRouteData.Routers.Add(match.Entry.Target);
                        MergeValues(newRouteData.Values, match.Values);

                        if (!RouteConstraintMatcher.Match(
                            match.Entry.Constraints,
                            newRouteData.Values,
                            context.HttpContext,
                            this,
                            RouteDirection.IncomingRequest,
                            _constraintLogger))
                        {
                            return;
                        }

                        _logger.LogVerbose(
                            "Request successfully matched the route with name '{RouteName}' and template '{RouteTemplate}'.",
                            match.Entry.RouteName,
                            match.Entry.RouteTemplate);

                        context.RouteData = newRouteData;

                        await match.Entry.Target.RouteAsync(context);

                        if (context.IsHandled)
                        {
                            return;
                        }

                        // Restore the original values to prevent polluting the route data.
                        context.RouteData = oldRouteData;
                    }
                }
            }
        }

        private struct TreeEnumerator : IEnumerator<UrlMatchingNode>
        {
            private readonly Stack<UrlMatchingNode> _stack;
            private readonly PathTokenizer _tokenizer;

            private int _segmentIndex;

            public TreeEnumerator(UrlMatchingNode root, PathTokenizer tokenizer)
            {
                _stack = new Stack<UrlMatchingNode>();
                _tokenizer = tokenizer;
                Current = null;
                _segmentIndex = -1;

                _stack.Push(root);
            }

            public UrlMatchingNode Current { get; private set; }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (_stack == null)
                {
                    return false;
                }

                while (_stack.Count > 0)
                {
                    var next = _stack.Pop();
                    if (++_segmentIndex >= _tokenizer.Count)
                    {
                        _segmentIndex--;
                        if (next.Matches.Count > 0)
                        {
                            Current = next;
                            return true;
                        }
                    }

                    if (_tokenizer.Count == 0)
                    {
                        continue;
                    }

                    if (next.CatchAlls != null)
                    {
                        _stack.Push(next.CatchAlls);
                    }

                    if (next.ConstrainedCatchAlls != null)
                    {
                        _stack.Push(next.ConstrainedCatchAlls);
                    }

                    if (next.Parameters != null)
                    {
                        _stack.Push(next.Parameters);
                    }

                    if (next.ConstrainedParameters != null)
                    {
                        _stack.Push(next.ConstrainedParameters);
                    }

                    if (next.Literals.Count > 0)
                    {
                        UrlMatchingNode node;
                        if (next.Literals.TryGetValue(_tokenizer[_segmentIndex].Value, out node))
                        {
                            _stack.Push(node);
                        }
                    }
                }

                return false;
            }

            public void Reset()
            {
                _stack.Clear();
                Current = null;
                _segmentIndex = -1;
            }
        }

        private static void MergeValues(
            IDictionary<string, object> destination,
            IDictionary<string, object> values)
        {
            foreach (var kvp in values)
            {
                if (kvp.Value != null)
                {
                    // This will replace the original value for the specified key.
                    // Values from the matched route will take preference over previous
                    // data in the route context.
                    destination[kvp.Key] = kvp.Value;
                }
            }
        }

        private struct TemplateMatch : IEquatable<TemplateMatch>
        {
            public TemplateMatch(AttributeRouteMatchingEntry entry, IDictionary<string, object> values)
            {
                Entry = entry;
                Values = values;
            }

            public AttributeRouteMatchingEntry Entry { get; }

            public IDictionary<string, object> Values { get; }

            public override bool Equals(object obj)
            {
                if (obj is TemplateMatch)
                {
                    return Equals((TemplateMatch)obj);
                }

                return false;
            }

            public bool Equals(TemplateMatch other)
            {
                return
                    object.ReferenceEquals(Entry, other.Entry) &&
                    object.ReferenceEquals(Values, other.Values);
            }

            public override int GetHashCode()
            {
                var hash = new HashCodeCombiner();
                hash.Add(Entry);
                hash.Add(Values);
                return hash.CombinedHash;
            }

            public static bool operator ==(TemplateMatch left, TemplateMatch right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(TemplateMatch left, TemplateMatch right)
            {
                return !left.Equals(right);
            }
        }

        public VirtualPathData GetVirtualPath(VirtualPathContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // If it's a named route we will try to generate a link directly and
            // if we can't, we will not try to generate it using an unnamed route.
            if (context.RouteName != null)
            {
                return GetVirtualPathForNamedRoute(context);
            }

            // The decision tree will give us back all entries that match the provided route data in the correct
            // order. We just need to iterate them and use the first one that can generate a link.
            var matches = _linkGenerationTree.GetMatches(context);

            foreach (var match in matches)
            {
                var path = GenerateVirtualPath(context, match.Entry);
                if (path != null)
                {
                    context.IsBound = true;
                    return path;
                }
            }

            return null;
        }

        private VirtualPathData GetVirtualPathForNamedRoute(VirtualPathContext context)
        {
            AttributeRouteLinkGenerationEntry entry;
            if (_namedEntries.TryGetValue(context.RouteName, out entry))
            {
                var path = GenerateVirtualPath(context, entry);
                if (path != null)
                {
                    context.IsBound = true;
                    return path;
                }
            }
            return null;
        }

        private VirtualPathData GenerateVirtualPath(VirtualPathContext context, AttributeRouteLinkGenerationEntry entry)
        {
            // In attribute the context includes the values that are used to select this entry - typically
            // these will be the standard 'action', 'controller' and maybe 'area' tokens. However, we don't
            // want to pass these to the link generation code, or else they will end up as query parameters.
            //
            // So, we need to exclude from here any values that are 'required link values', but aren't
            // parameters in the template.
            //
            // Ex:
            //      template: api/Products/{action}
            //      required values: { id = "5", action = "Buy", Controller = "CoolProducts" }
            //
            //      result: { id = "5", action = "Buy" }
            var inputValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in context.Values)
            {
                if (entry.RequiredLinkValues.ContainsKey(kvp.Key))
                {
                    var parameter = entry.Template.Parameters
                        .FirstOrDefault(p => string.Equals(p.Name, kvp.Key, StringComparison.OrdinalIgnoreCase));

                    if (parameter == null)
                    {
                        continue;
                    }
                }

                inputValues.Add(kvp.Key, kvp.Value);
            }

            var bindingResult = entry.Binder.GetValues(context.AmbientValues, inputValues);
            if (bindingResult == null)
            {
                // A required parameter in the template didn't get a value.
                return null;
            }

            var matched = RouteConstraintMatcher.Match(
                entry.Constraints,
                bindingResult.CombinedValues,
                context.Context,
                this,
                RouteDirection.UrlGeneration,
                _constraintLogger);

            if (!matched)
            {
                // A constraint rejected this link.
                return null;
            }

            // These values are used to signal to the next route what we would produce if we round-tripped
            // (generate a link and then parse). In MVC the 'next route' is typically the MvcRouteHandler.
            var providedValues = new Dictionary<string, object>(
                bindingResult.AcceptedValues,
                StringComparer.OrdinalIgnoreCase);
            providedValues.Add(AttributeRouting.RouteGroupKey, entry.RouteGroup);

            var childContext = new VirtualPathContext(context.Context, context.AmbientValues, context.Values)
            {
                ProvidedValues = providedValues,
            };

            var pathData = _next.GetVirtualPath(childContext);
            if (pathData != null)
            {
                // If path is non-null then the target router short-circuited, we don't expect this
                // in typical MVC scenarios.
                return pathData;
            }
            else if (!childContext.IsBound)
            {
                // The target router has rejected these values. We don't expect this in typical MVC scenarios.
                return null;
            }

            var path = entry.Binder.BindValues(bindingResult.AcceptedValues);
            if (path == null)
            {
                return null;
            }

            return new VirtualPathData(this, path);
        }
    }
}
