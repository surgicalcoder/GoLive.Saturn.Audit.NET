using Audit.Core;
using System;
using System.Net.Http;
using System.Security.Claims;

namespace Audit.Http.ConfigurationApi
{
    public class AuditClientHandlerConfigurator : IAuditClientHandlerConfigurator
    {
        internal Func<HttpRequestMessage, bool> _requestFilter;
        internal Func<HttpResponseMessage, bool> _responseFilter;
        internal Func<HttpRequestMessage, bool> _includeContentHeaders;
        internal Func<HttpRequestMessage, bool> _includeRequestHeaders;
        internal Func<HttpResponseMessage, bool> _includeResponseHeaders;
        internal Func<HttpRequestMessage, bool> _includeRequestBody;
        internal Func<HttpResponseMessage, bool> _includeResponseBody;
        internal Func<HttpRequestMessage, string> _eventTypeName;
        internal EventCreationPolicy? _eventCreationPolicy;
        internal AuditDataProvider _auditDataProvider;
        internal IAuditScopeFactory _auditScopeFactory;
        internal Func<IServiceProvider, ClaimsPrincipal> _getClaimsFunc;

        internal Func<IServiceProvider, string> _getTraceId;
        internal Func<IServiceProvider, string> _getCorrelationId;
        internal Func<IServiceProvider, string> _getRequestId;

        public IAuditClientHandlerConfigurator AuditScopeFactory(IAuditScopeFactory auditScopeFactory)
        {
            _auditScopeFactory = auditScopeFactory;
            return this;
        }

        public IAuditClientHandlerConfigurator AuditClaimsPrinciple(Func<IServiceProvider, ClaimsPrincipal> getClaims)
        {
            _getClaimsFunc = getClaims;

            return this;
        }

        public IAuditClientHandlerConfigurator GetTraceId(Func<IServiceProvider, string> getTraceId)
        {
            _getTraceId = getTraceId;

            return this;
        }

        public IAuditClientHandlerConfigurator GetCorrelationId(Func<IServiceProvider, string> getCorrelationId)
        {
            _getCorrelationId = getCorrelationId;

            return this;
        }

        public IAuditClientHandlerConfigurator GetRequestId(Func<IServiceProvider, string> getRequestId)
        {
            _getRequestId = getRequestId;

            return this;
        }

        public IAuditClientHandlerConfigurator CreationPolicy(EventCreationPolicy eventCreationPolicy)
        {
            _eventCreationPolicy = eventCreationPolicy;
            return this;
        }

        public IAuditClientHandlerConfigurator AuditDataProvider(AuditDataProvider auditDataProvider)
        {
            _auditDataProvider = auditDataProvider;
            return this;
        }

        public IAuditClientHandlerConfigurator IncludeRequestHeaders(bool include = true)
        {
            _includeRequestHeaders = _ => include;
            return this;
        }

        public IAuditClientHandlerConfigurator IncludeContentHeaders(bool include = true)
        {
            _includeContentHeaders = _ => include;
            return this;
        }

        public IAuditClientHandlerConfigurator IncludeRequestHeaders(Func<HttpRequestMessage, bool> includePredicate)
        {
            _includeRequestHeaders = includePredicate;
            return this;
        }

        public IAuditClientHandlerConfigurator IncludeContentHeaders(Func<HttpRequestMessage, bool> includePredicate)
        {
            _includeContentHeaders = includePredicate;
            return this;
        }

        public IAuditClientHandlerConfigurator IncludeResponseHeaders(bool include = true)
        {
            _includeResponseHeaders = _ => include;
            return this;
        }

        public IAuditClientHandlerConfigurator IncludeResponseHeaders(Func<HttpResponseMessage, bool> includePredicate)
        {
            _includeResponseHeaders = includePredicate;
            return this;
        }

        public IAuditClientHandlerConfigurator IncludeRequestBody(bool include = true)
        {
            _includeRequestBody = _ => include;
            return this;
        }

        public IAuditClientHandlerConfigurator IncludeRequestBody(Func<HttpRequestMessage, bool> includePredicate)
        {
            _includeRequestBody = includePredicate;
            return this;
        }

        public IAuditClientHandlerConfigurator IncludeResponseBody(bool include = true)
        {
            _includeResponseBody = _ => include;
            return this;
        }

        public IAuditClientHandlerConfigurator IncludeResponseBody(Func<HttpResponseMessage, bool> includePredicate)
        {
            _includeResponseBody = includePredicate;
            return this;
        }

        public IAuditClientHandlerConfigurator FilterByRequest(Func<HttpRequestMessage, bool> requestPredicate)
        {
            _requestFilter = requestPredicate;
            return this;
        }

        public IAuditClientHandlerConfigurator FilterByResponse(Func<HttpResponseMessage, bool> responsePredicate)
        {
            _responseFilter = responsePredicate;
            return this;
        }

        public IAuditClientHandlerConfigurator EventType(string eventTypeName)
        {
            _eventTypeName = _ => eventTypeName;
            return this;
        }

        public IAuditClientHandlerConfigurator EventType(Func<HttpRequestMessage, string> eventTypeNamePredicate)
        {
            _eventTypeName = eventTypeNamePredicate;
            return this;
        }
    }
}
