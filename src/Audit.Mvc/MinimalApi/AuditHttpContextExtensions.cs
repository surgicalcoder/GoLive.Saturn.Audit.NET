using System;
using Audit.Core;
using Microsoft.AspNetCore.Http;

namespace Audit.Mvc.MinimalApi;

/// <summary>
/// Extension methods for retrieving audit information from HttpContext.
/// </summary>
public static class AuditHttpContextExtensions
{
    private const string AuditActionKey = "__private_AuditAction__";
    private const string AuditScopeKey = "__private_AuditScope__";

    /// <summary>
    /// Gets the current AuditAction from the HttpContext.
    /// Returns null if no audit action is found.
    /// </summary>
    /// <param name="httpContext">The HttpContext.</param>
    /// <returns>The current AuditAction or null.</returns>
    public static AuditEventMvcAction GetAuditAction(this HttpContext httpContext)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }

        return httpContext.Items.TryGetValue(AuditActionKey, out var action) 
            ? action as AuditEventMvcAction 
            : null;
    }

    /// <summary>
    /// Gets the current AuditScope from the HttpContext.
    /// Returns null if no audit scope is found.
    /// </summary>
    /// <param name="httpContext">The HttpContext.</param>
    /// <returns>The current AuditScope or null.</returns>
    public static AuditScope GetAuditScope(this HttpContext httpContext)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));

        return httpContext.Items.TryGetValue(AuditScopeKey, out var scope) 
            ? scope as AuditScope 
            : null;
    }

    /// <summary>
    /// Gets both the current AuditAction and AuditScope from the HttpContext.
    /// </summary>
    /// <param name="httpContext">The HttpContext.</param>
    /// <returns>A tuple containing the AuditAction and AuditScope, either may be null if not found.</returns>
    public static (AuditEventMvcAction Action, AuditScope Scope) GetAuditContext(this HttpContext httpContext)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));

        var action = httpContext.GetAuditAction();
        var scope = httpContext.GetAuditScope();

        return (action, scope);
    }
}

