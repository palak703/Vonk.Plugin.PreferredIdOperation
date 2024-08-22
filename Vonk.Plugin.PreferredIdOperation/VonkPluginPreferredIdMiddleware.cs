using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vonk.Core.Context;
using Vonk.Core.Context.Http;
using Vonk.Core.Support;

namespace Vonk.Plugin.PreferredIdOperation
{
    internal class VonkPluginPreferredIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PreferredIdService> _logger;

        public VonkPluginPreferredIdMiddleware(RequestDelegate next,ILogger<PreferredIdService> logger)
        {
            Check.NotNull(next,nameof(next));
            Check.NotNull(logger,nameof(logger));
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext httpContext) 
        {
            _logger.LogDebug("VonkPluginPreferredIdMiddleware - Begin Invoke");
           
            var (request, args, response) = httpContext.Vonk().Parts();
            httpContext.Items["key"] = "value";
           
            await _next(httpContext);
            _logger.LogDebug("VonkPluginPreferredIdMiddleware -End Invoke");

        }
    }
}
