using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace DriveAndGo_API.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                string traceId = context.TraceIdentifier ?? Guid.NewGuid().ToString("N");
                _logger.LogError(ex, "Unhandled exception [{TraceId}] on {Method} {Path}", traceId, context.Request.Method, context.Request.Path);
                await HandleExceptionAsync(context, ex, traceId);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception, string traceId)
        {
            context.Response.ContentType = "application/problem+json";
            
            string friendlyDetail = context.Request.Path.Value?.Contains("swagger", StringComparison.OrdinalIgnoreCase) == true
                ? exception.ToString()
                : Services.UserFriendlyErrorMessage.Clean(exception.Message);

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "System Notice",
                Instance = context.Request.Path,
                Detail = friendlyDetail
            };
            problem.Extensions["traceId"] = traceId;

            // Custom validations or standard client errors
            if (exception is ArgumentException || exception is InvalidOperationException)
            {
                problem.Status = StatusCodes.Status400BadRequest;
                problem.Title = "Information Required";
                problem.Detail = friendlyDetail;
            }
            else if (exception is PostgresException pgEx)
            {
                if (pgEx.SqlState == "23505") // Unique key violation (e.g. duplicate booking / plate number)
                {
                    problem.Status = StatusCodes.Status409Conflict;
                    problem.Title = "Duplicate Record Detected";
                    problem.Detail = "A record with the same details (such as plate number, email, or booking) already exists. Please review your input.";
                }
                else
                {
                    problem.Status = StatusCodes.Status500InternalServerError;
                    problem.Title = "Database Service Notice";
                    problem.Detail = "Our database service is temporarily unreachable. Please check your connection and try again.";
                }
            }

            context.Response.StatusCode = problem.Status.Value;
            var json = JsonSerializer.Serialize(problem);
            return context.Response.WriteAsync(json);
        }
    }
}
