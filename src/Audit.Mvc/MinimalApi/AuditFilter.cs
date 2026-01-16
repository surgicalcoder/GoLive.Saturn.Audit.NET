using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Audit.Core;
using Audit.Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Audit.Mvc.MinimalApi;

public class AuditFilter : IEndpointFilter
{
    private const int DefaultCopyBufferSize = 81920;
    private const string AuditActionKey = "__private_AuditAction__";
    private const string AuditScopeKey = "__private_AuditScope__";

    public bool IncludeModel { get; set; }
    public bool IncludeRequestBody { get; set; }
    public bool IncludeHeaders { get; set; }
    public bool IncludeResponseBody { get; set; }
    public string EventTypeName { get; set; } = "{verb} {route}";
    public bool SerializeParameters { get; set; } = true;

    public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var request = httpContext.Request;
        var cancellationToken = httpContext.RequestAborted;

        if (Configuration.AuditDisabled)
        {
            return await next(context);
        }

        // BEFORE EXECUTION
        var auditAction = new AuditAction()
        {
            UserName = httpContext.User?.Identity?.Name,
            IpAddress = httpContext.Connection?.RemoteIpAddress?.ToString(),
            RequestUrl = $"{request.Scheme}://{request.Host}{request.Path}",
            HttpMethod = request.Method,
            Headers = IncludeHeaders ? ToDictionary(request.Headers) : null,
            ActionName = httpContext.GetEndpoint()?.DisplayName,
            ControllerName = httpContext.GetEndpoint()?.Metadata?.GetMetadata<string>(),
            ActionParameters = SerializeParameters ? SerializeInputParameters(context) : GetInputParameters(context),
            RequestBody = new BodyContent
            {
                Type = request.ContentType,
                Length = request.ContentLength,
                Value = IncludeRequestBody ? await GetRequestBodyAsync(request, cancellationToken) : null
            },
            TraceId = httpContext.TraceIdentifier
        };

        var routeName = httpContext.GetEndpoint()?.DisplayName ?? "Unknown";
        var eventType = (EventTypeName ?? "{verb} {route}")
                        .Replace("{verb}", auditAction.HttpMethod)
                        .Replace("{route}", routeName);

        var auditEventAction = new AuditEventMvcAction
        {
            Action = auditAction,
            UserId = Configuration.GetUserId?.Invoke(httpContext.User),
            TenantId = Configuration.GetTenantId?.Invoke(httpContext.User),
            UserSessionId = Configuration.GetSessionId?.Invoke(httpContext.User),
            CorrelationId = Configuration.GetCorrelationId?.Invoke(httpContext.RequestServices)
        };

        var auditScopeOptions = new AuditScopeOptions
        {
            EventType = eventType,
            AuditEvent = auditEventAction
        };

        var auditScope = await AuditScope.CreateAsync(auditScopeOptions, cancellationToken);
        httpContext.Items[AuditActionKey] = auditAction;
        httpContext.Items[AuditScopeKey] = auditScope;

        object result = null;

        try
        {
            // Proceed with normal endpoint pipeline
            result = await next(context);
        }
        catch (Exception ex)
        {
            auditAction.Exception = ex.GetExceptionInfo();
            throw;
        }
        finally
        {
            // AFTER EXECUTION
            auditAction.ResponseStatusCode = httpContext.Response.StatusCode;
            auditAction.ResponseStatus = httpContext.Response.StatusCode.ToString();
            auditAction.ResponseBody = IncludeResponseBody 
                ? new BodyContent
                {
                    Type = result?.GetType().FullName,
                    Value = result
                }
                : null;

            auditScope.EventAs<AuditEventMvcAction>().Action = auditAction;
            if (auditScope.EventCreationPolicy == EventCreationPolicy.Manual)
            {
                await auditScope.SaveAsync(cancellationToken);
            }
            await auditScope.DisposeAsync();
        }

        return result;
    }

    private static IDictionary<string, string> ToDictionary(IEnumerable<KeyValuePair<string, StringValues>> col)
    {
        return col?.ToDictionary(k => k.Key, v => string.Join(", ", v.Value));
    }

    private static async Task<string> GetRequestBodyAsync(HttpRequest request, CancellationToken token)
    {
        if (request.Body is null || !request.Body.CanRead)
            return null;

        request.EnableBuffering();
        using (var stream = new MemoryStream())
        {
            await request.Body.CopyToAsync(stream, DefaultCopyBufferSize, token);
            stream.Seek(0, SeekOrigin.Begin);
            request.Body.Seek(0, SeekOrigin.Begin);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    private static IDictionary<string, object> GetInputParameters(EndpointFilterInvocationContext context)
    {
        var methodInfo = context.HttpContext.GetEndpoint()?.Metadata?.GetMetadata<MethodInfo>();
        var parameters = methodInfo?.GetParameters() ?? [];
        
        return context.Arguments
                      .Select((a, i) => new { Index = i, Argument = a, Parameter = parameters.ElementAtOrDefault(i) })
                      .Where(x => x.Parameter != null 
                                  && !x.Parameter.GetCustomAttributes(typeof(FromServicesAttribute), true).Any()
                                  && !HasDoNotAuditAttribute(x.Argument))
                      .ToDictionary(kv => kv.Parameter?.Name ?? $"arg{kv.Index}", kv => kv.Argument);
    }

    private static bool HasDoNotAuditAttribute(object argument)
    {
        if (argument == null)
            return false;

        var argumentType = argument.GetType();
        return argumentType.GetCustomAttributes(typeof(DoNotAuditAttribute), true).Any();
    }

    private static IDictionary<string, object> SerializeInputParameters(EndpointFilterInvocationContext context)
        => AuditHelper.SerializeParameters(GetInputParameters(context));
}