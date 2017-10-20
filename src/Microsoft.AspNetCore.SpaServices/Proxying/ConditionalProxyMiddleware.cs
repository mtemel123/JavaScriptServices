// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.SpaServices.Proxy
{
    /// <summary>
    /// Based on https://github.com/aspnet/Proxy/blob/dev/src/Microsoft.AspNetCore.Proxy/ProxyMiddleware.cs
    /// Differs in that, if the proxied request returns a 404, we pass through to the next middleware in the chain
    /// This is useful for Webpack/Angular CLI middleware, because it lets you fall back on prebuilt files on disk
    /// for files not served by that middleware.
    /// </summary>
    internal class ConditionalProxyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly Task<ConditionalProxyMiddlewareTarget> _targetTask;
        private readonly string _pathPrefix;
        private readonly bool _pathPrefixIsRoot;
        private readonly HttpClient _httpClient;
        private readonly CancellationToken _applicationStoppingToken;

        public ConditionalProxyMiddleware(
            RequestDelegate next,
            string pathPrefix,
            TimeSpan requestTimeout,
            Task<ConditionalProxyMiddlewareTarget> targetTask,
            IApplicationLifetime applicationLifetime)
        {
            if (!pathPrefix.StartsWith("/"))
            {
                pathPrefix = "/" + pathPrefix;
            }

            _next = next;
            _pathPrefix = pathPrefix;
            _pathPrefixIsRoot = string.Equals(_pathPrefix, "/", StringComparison.Ordinal);
            _targetTask = targetTask;
            _httpClient = ConditionalProxy.CreateHttpClientForProxy(requestTimeout);
            _applicationStoppingToken = applicationLifetime.ApplicationStopping;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments(_pathPrefix) || _pathPrefixIsRoot)
            {
                var didProxyRequest = await ConditionalProxy.PerformProxyRequest(
                    context, _httpClient, _targetTask, _applicationStoppingToken);
                if (didProxyRequest)
                {
                    return;
                }
            }

            // Not a request we can proxy
            await _next.Invoke(context);
        }
    }
}
