// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using Microsoft.AspNet.Mvc.Core;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNet.Mvc.Internal
{
    public static class ResponseContentTypeHelper
    {
        public static void ResolveContentTypeAndEncoding(
            MediaTypeHeaderValue actionResultContentType,
            string httpResponseContentType,
            MediaTypeHeaderValue defaultContentType,
            out string resolvedResponseContentType,
            out Encoding resolvedContentTypeEncoding)
        {
            if (defaultContentType == null)
            {
                throw new ArgumentNullException(nameof(defaultContentType));
            }

            if (defaultContentType.Encoding == null)
            {
                throw new ArgumentException(
                    Resources.FormatDefaultContentTypeMustHaveEncoding(defaultContentType.ToString()));
            }

            // 1. User sets the ContentType property on the action result
            if (actionResultContentType != null)
            {
                resolvedResponseContentType = actionResultContentType.ToString();
                resolvedContentTypeEncoding = actionResultContentType.Encoding ?? defaultContentType.Encoding;
                return;
            }

            // 2. User sets the ContentType property on the http response directly
            if (!string.IsNullOrEmpty(httpResponseContentType))
            {
                MediaTypeHeaderValue mediaType;
                if (MediaTypeHeaderValue.TryParse(httpResponseContentType, out mediaType))
                {
                    resolvedResponseContentType = httpResponseContentType;
                    resolvedContentTypeEncoding = mediaType.Encoding ?? defaultContentType.Encoding;
                }
                else
                {
                    resolvedResponseContentType = httpResponseContentType;
                    resolvedContentTypeEncoding = defaultContentType.Encoding;
                }

                return;
            }

            // 3. Fall-back to the default content type
            resolvedResponseContentType = defaultContentType.ToString();
            resolvedContentTypeEncoding = defaultContentType.Encoding;
        }
    }
}
