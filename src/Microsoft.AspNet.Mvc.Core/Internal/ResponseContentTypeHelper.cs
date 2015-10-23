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
        public static Tuple<string, Encoding> GetResponseContentTypeAndEncoding(
            MediaTypeHeaderValue actionResultContentType,
            string httpResponseContentType,
            MediaTypeHeaderValue defaultContentType)
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
                return new Tuple<string, Encoding>(
                    actionResultContentType.ToString(),
                    actionResultContentType.Encoding ?? defaultContentType.Encoding);
            }

            // 2. User sets the ContentType property on the http response directly
            if (!string.IsNullOrEmpty(httpResponseContentType))
            {
                MediaTypeHeaderValue mediaType;
                if (MediaTypeHeaderValue.TryParse(httpResponseContentType, out mediaType))
                {
                    return new Tuple<string, Encoding>(
                        httpResponseContentType,
                        mediaType.Encoding ?? defaultContentType.Encoding);
                }
                else
                {
                    return new Tuple<string, Encoding>(httpResponseContentType, defaultContentType.Encoding);
                }
            }

            // 3. Fall-back to the default content type
            return new Tuple<string, Encoding>(defaultContentType.ToString(), defaultContentType.Encoding);
        }
    }
}
