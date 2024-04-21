using Audit.Core;

namespace Audit.Http
{
    public class AuditEventHttpClient : AuditEvent
    {
        /// <summary>
        /// Gets or sets the HttpClient event details.
        /// </summary>
        public HttpAction Action { get; set; }
        
        public string TenantId { get; set; }
        public string UserId { get; set; }
        public string UserSessionId { get; set; }
        
        public string CorrelationId { get; set; }
        public string TraceId { get; set; }
        public string RequestId { get; set; }
    }
}
