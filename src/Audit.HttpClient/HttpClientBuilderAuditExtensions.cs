using System;
using Audit.Http.ConfigurationApi;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.Http;

public static class HttpClientBuilderAuditExtensions
{
    /// <summary>
    /// Adds a delegate handler to audit HttpClient calls.
    /// </summary>
    /// <param name="builder">The Microsoft.Extensions.DependencyInjection.IHttpClientBuilder</param>
    /// <param name="config">The audit configuration</param>
    /// <returns></returns>
    public static IHttpClientBuilder AddAuditHandler(this IHttpClientBuilder builder, IServiceProvider provider, Action<IAuditClientHandlerConfigurator> config)
    {
        return builder.AddHttpMessageHandler(() => new AuditHttpClientHandler(provider, config, null));
    }
}