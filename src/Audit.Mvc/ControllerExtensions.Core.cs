using Audit.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Audit.Mvc;

public static class ControllerExtensions
{
    /// <summary>
    /// Gets the current Audit Scope.
    /// </summary>
    /// <param name="controller">The MVC controller.</param>
    /// <returns>The current Audit Scope or NULL.</returns>
    public static AuditScope GetCurrentAuditScope(this ControllerBase controller)
    {
        return AuditAttribute.GetCurrentScope(controller.HttpContext);
    }

    /// <summary>
    /// Gets the current Audit Scope.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <returns>The current Audit Scope or NULL.</returns>
    public static AuditScope GetCurrentAuditScope(this HttpContext httpContext)
    {
        return AuditAttribute.GetCurrentScope(httpContext);
    }

    /// <summary>
    /// Gets the current Audit Scope.
    /// </summary>
    /// <param name="page">The razor page.</param>
    /// <returns>The current Audit Scope or NULL.</returns>
    public static AuditScope GetCurrentAuditScope(this PageModel page)
    {
        return AuditAttribute.GetCurrentScope(page.HttpContext);
    }
}