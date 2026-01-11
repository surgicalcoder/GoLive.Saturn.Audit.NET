using System;

namespace Audit.DynamicProxy;

/// <summary>
/// Use to avoid logging specific operations, parameters or return values.
/// </summary>
[AttributeUsage(AttributeTargets.Event | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
public sealed class AuditIgnoreAttribute : Attribute { }