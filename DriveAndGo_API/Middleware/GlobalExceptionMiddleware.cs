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
                _logger.LogError(ex, "An unhandled exception occurred on request: {Path}", context.Request.Path);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/problem+json";
            
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An error occurred while processing your request",
                Instance = context.Request.Path,
                Detail = "Internal Server Error"
            };

            // Custom validations or standard client errors
            if (exception is ArgumentException || exception is InvalidOperationException)
            {
                problem.Status = StatusCodes.Status400BadRequest;
                problem.Title = "Bad Request";
                problem.Detail = exception.Message;
            }
            else if (exception is PostgresException pgEx)
            {
                // Mask detailed database error details in production-grade releases
                if (pgEx.SqlState == "23505") // Unique key violation (e.g. duplicate booking / plate number)
                {
                    problem.Status = StatusCodes.Status409Conflict;
                    problem.Title = "Conflict";
                    problem.Detail = "The resource already exists or conflicts with another record.";
                }
                else
                {
                    problem.Status = StatusCodes.Status500InternalServerError;
                    problem.Title = "Database Execution Error";
                    problem.Detail = "A database operation failed.";
                }
            }

            context.Response.StatusCode = problem.Status.Value;
            var json = JsonSerializer.Serialize(problem);
            return context.Response.WriteAsync(json);
        }
    }
}
