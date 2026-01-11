using System;
using System.Net.Http;
using Audit.Http.ConfigurationApi;

namespace Audit.Http;

/// <summary>
/// Factory to create audited HttpClient instances
/// </summary>
public static class ClientFactory
{
    /// <summary>
    /// Creates and returns an audited HttpClient with the default configuration
    /// </summary>
    public static HttpClient Create()
    {
        return new HttpClient(new AuditHttpClientHandler(), false);
    }

    /// <summary>
    /// Creates and returns an audited HttpClient with the specified configuration
    /// </summary>
    /// <param name="config">Action to set the configuration via a fluent API</param>
    public static HttpClient Create(Action<IAuditClientHandlerConfigurator> config)
    {
        return new HttpClient(new AuditHttpClientHandler(config), false);
    }
}