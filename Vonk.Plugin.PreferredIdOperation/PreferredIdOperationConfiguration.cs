using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vonk.Core.Context;
using Vonk.Core.Metadata;
using Vonk.Core.Pluggability;
using Vonk.Core.Pluggability.ContextAware;

namespace Vonk.Plugin.PreferredIdOperation
{
    [VonkConfiguration(order: 4600)]
    public static class PreferredIdOperationConfiguration
    {
        // Add services here to the DI system of ASP.NET Core
        public static IServiceCollection ConfigureServices(IServiceCollection services)
        {
            services.TryAddScoped<PreferredIdService>(); // $document implementation
            services.TryAddContextAware<ICapabilityStatementContributor, PreferredIdOperationConformanceContributor>
                (ServiceLifetime.Transient);
            return services;
        }

        // Add middleware to the pipeline being built with the builder
        public static IApplicationBuilder Configure(IApplicationBuilder builder)
        {
            builder
               .OnCustomInteraction(VonkInteraction.type_custom, "preferred-id")
               .AndResourceTypes(new[] { "NamingSystem" })
               .AndMethod("GET")
               .HandleAsyncWith<PreferredIdService>((svc, context)
                   => svc.PreferredIdGet(context));

            return builder;
        }
    }
    
    [VonkConfiguration(order: 4590)]
    public class VonkPreferredIdMiddlewareConfiguration
    {
        public static IServiceCollection ConfigureServices(IServiceCollection services)
        {
            return services;
        }

        public static IApplicationBuilder Configure(IApplicationBuilder builder)
        {
            builder.UseMiddleware<VonkPluginPreferredIdMiddleware>();
            return builder;
        }
    }
}
