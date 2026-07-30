using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebApplication2.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var traceId = context.TraceIdentifier;
                var user = context.User?.Identity?.Name ?? "anonymous";
                var path = context.Request?.Path;
                _logger.LogError(ex, "Unhandled exception. TraceId={TraceId} User={User} Path={Path}", traceId, user, path);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var env = context.RequestServices.GetService(typeof(IHostEnvironment)) as IHostEnvironment;
            var factory = context.RequestServices.GetService(typeof(ProblemDetailsFactory)) as ProblemDetailsFactory;

            var status = MapExceptionToStatusCode(exception);
            var title = GetTitleForStatus(status);
            var detail = env?.IsDevelopment() == true ? exception.Message : null;

            var pd = factory?.CreateProblemDetails(context, status, title, null, detail) ?? new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            };

            // add trace id for correlation
            var trace = context.TraceIdentifier;
            pd.Extensions["traceId"] = trace;

            context.Response.Clear();
            context.Response.StatusCode = pd.Status ?? status;
            context.Response.ContentType = "application/problem+json";
            context.Response.Headers["X-Trace-Id"] = trace;

            var options = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(pd, options));
        }

        private static int MapExceptionToStatusCode(Exception ex)
        {
            return ex switch
            {
                ArgumentNullException => (int)HttpStatusCode.BadRequest,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                InvalidOperationException => (int)HttpStatusCode.Conflict,
                _ => (int)HttpStatusCode.InternalServerError,
            };
        }

        private static string GetTitleForStatus(int status)
        {
            return status switch
            {
                400 => "Bad Request",
                401 => "Unauthorized",
                404 => "Not Found",
                409 => "Conflict",
                _ => "An error occurred while processing your request."
            };
        }
    }
}
