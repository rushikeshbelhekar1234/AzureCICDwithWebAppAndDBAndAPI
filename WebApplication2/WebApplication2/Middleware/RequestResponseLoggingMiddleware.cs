using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace WebApplication2.Middleware
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

        public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            var req = context.Request;
            var traceId = context.TraceIdentifier;
            _logger.LogInformation("Incoming request {method} {path} TraceId={traceId}", req.Method, req.Path, traceId);

            await _next(context);

            sw.Stop();
            var res = context.Response;
            _logger.LogInformation("Handled {method} {path} -> {status} in {ms}ms TraceId={traceId}",
                req.Method, req.Path, res.StatusCode, sw.Elapsed.TotalMilliseconds, traceId);
        }
    }
}
