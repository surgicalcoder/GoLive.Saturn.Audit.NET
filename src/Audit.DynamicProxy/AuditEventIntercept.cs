using Audit.Core;

namespace Audit.DynamicProxy;

/// <summary>
/// Represents the output of the audit process for the Audit.DynamicProxy
/// </summary>
public class AuditEventIntercept : AuditEvent
{
    /// <summary>
    /// Gets or sets the intercepted event details.
    /// </summary>
    public InterceptEvent InterceptEvent { get; set; }
}