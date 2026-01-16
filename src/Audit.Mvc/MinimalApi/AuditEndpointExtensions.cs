using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Audit.Mvc.MinimalApi;

public static class AuditEndpointExtensions
{
    /// <summary>
    /// Adds the AuditEndpointFilter to an EndpointConventionBuilder.
    /// Example: app.MapPost(...).Auditable();
    /// </summary>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="configure">Optional configuration action for the filter instance.</param>
    /// <returns>The same builder with the filter added.</returns>
    public static TBuilder Auditable<TBuilder>(
        this TBuilder builder,
        Action<AuditFilter>? configure = null)
        where TBuilder : IEndpointConventionBuilder
    {
        var filter = new AuditFilter();
        configure?.Invoke(filter);

        builder.AddEndpointFilter(filter);
        return builder;
    }
}