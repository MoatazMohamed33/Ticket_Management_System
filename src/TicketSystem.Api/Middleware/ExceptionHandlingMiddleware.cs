using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Application.Common.Exceptions;
using TicketSystem.Domain.Tickets;

namespace TicketSystem.Api.Middleware;

/// <summary>
/// Centralized exception handler that maps Application-layer exceptions to RFC 7807 ProblemDetails.
/// Never leaks stack traces to clients in Production. Includes correlationId in every response body.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            await HandleAsync(ctx, ex);
        }
    }

    private async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        var correlationId = ctx.Items[CorrelationIdMiddleware.ItemsKey] as string ?? "unknown";

        if (ctx.Response.HasStarted)
        {
            // Already streaming — cannot rewrite status or body. Log and re-throw with the
            // original stack trace preserved. Bare `throw;` only works inside a catch clause;
            // ExceptionDispatchInfo is the equivalent from a helper method.
            _logger.LogError(ex,
                "Unhandled exception after response started. CorrelationId={CorrelationId}", correlationId);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        }

        ProblemDetails problem;
        int status;

        switch (ex)
        {
            case ValidationException v:
                status = StatusCodes.Status400BadRequest;
                problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/400",
                    Title = "One or more validation errors occurred.",
                    Status = status,
                    Detail = "See errors extension for field-level failures."
                };
                var errors = v.Errors
                    .GroupBy(f => f.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());
                problem.Extensions["errors"] = errors;
                _logger.LogInformation(ex, "Validation failure. CorrelationId={CorrelationId}", correlationId);
                break;

            case InvalidCredentialsException ic:
                status = StatusCodes.Status401Unauthorized;
                problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/401",
                    Title = "Unauthorized.",
                    Status = status,
                    Detail = ic.Message
                };
                // Not logged here — the auth handler already logged the failed attempt with more context.
                break;

            case BadRequestException br:
                status = StatusCodes.Status400BadRequest;
                problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/400",
                    Title = "Bad request.",
                    Status = status,
                    Detail = br.Message
                };
                _logger.LogInformation(ex, "Bad request. CorrelationId={CorrelationId}", correlationId);
                break;

            case NotFoundException nf:
                status = StatusCodes.Status404NotFound;
                problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/404",
                    Title = "Resource not found.",
                    Status = status,
                    Detail = nf.Message
                };
                _logger.LogInformation(ex, "Not found. CorrelationId={CorrelationId}", correlationId);
                break;

            case ForbiddenException fb:
                status = StatusCodes.Status403Forbidden;
                problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/403",
                    Title = "Forbidden.",
                    Status = status,
                    Detail = fb.Message
                };
                _logger.LogWarning(ex, "Forbidden. CorrelationId={CorrelationId}", correlationId);
                break;

            case ConflictException cf:
                status = StatusCodes.Status409Conflict;
                problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/409",
                    Title = "Conflict.",
                    Status = status,
                    Detail = cf.Message
                };
                _logger.LogInformation(ex, "Conflict. CorrelationId={CorrelationId}", correlationId);
                break;

            case InvalidTicketTransitionException it:
                status = StatusCodes.Status400BadRequest;
                problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/400",
                    Title = "Invalid ticket transition.",
                    Status = status,
                    Detail = it.Message,
                };
                problem.Extensions["from"] = it.From.ToString();
                problem.Extensions["to"]   = it.To.ToString();
                _logger.LogInformation(ex,
                    "Invalid ticket transition {From} → {To}. CorrelationId={CorrelationId}",
                    it.From, it.To, correlationId);
                break;

            case ConcurrencyConflictException cc:
                status = StatusCodes.Status409Conflict;
                problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/409",
                    Title = "The resource was updated by another operation.",
                    Status = status,
                    Detail = "Reload the resource to see the latest state, then re-apply your change.",
                };
                foreach (var kv in cc.CurrentState) problem.Extensions[kv.Key] = kv.Value;
                _logger.LogInformation(ex, "Concurrency conflict. CorrelationId={CorrelationId}", correlationId);
                break;

            case DbUpdateConcurrencyException:
                // Fallback — Application handlers use SaveChangesWithConcurrencyCheckAsync
                // which translates to ConcurrencyConflictException above with enriched state.
                // This case protects against any handler that bypasses that wrapper.
                status = StatusCodes.Status409Conflict;
                problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/409",
                    Title = "The resource was updated by another operation.",
                    Status = status,
                    Detail = "Reload the resource to see the latest state, then re-apply your change."
                };
                _logger.LogInformation(ex, "Optimistic concurrency conflict. CorrelationId={CorrelationId}", correlationId);
                break;

            case DbUpdateException dbEx when IsUniqueIndexViolation(dbEx):
                // TOCTOU race guard: two concurrent inserts both pass an application-level
                // uniqueness check, one hits the DB unique index. Map to 409 so ACs that
                // demand "duplicate → 409" hold even under concurrency.
                status = StatusCodes.Status409Conflict;
                problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/409",
                    Title = "Conflict.",
                    Status = status,
                    Detail = "A resource with the same unique identifier already exists."
                };
                _logger.LogInformation(ex, "Unique index violation. CorrelationId={CorrelationId}", correlationId);
                break;

            default:
                status = StatusCodes.Status500InternalServerError;
                // Only Development sees raw exception details. Staging + Production get sanitized
                // output — staging often talks to real customer data via QA.
                var showDetails = _env.IsDevelopment();
                problem = new ProblemDetails
                {
                    Type = "https://httpstatuses.io/500",
                    Title = showDetails ? ex.GetType().Name : "Internal server error.",
                    Status = status,
                    Detail = showDetails ? ex.Message : "An unexpected error occurred."
                };
                _logger.LogError(ex, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);
                break;
        }

        problem.Extensions["correlationId"] = correlationId;

        static bool IsUniqueIndexViolation(DbUpdateException ex)
        {
            // SQL Server error numbers: 2601 (unique index) / 2627 (unique constraint).
            // Match on message text to keep this file provider-neutral (avoids a direct
            // Microsoft.Data.SqlClient reference).
            var msg = ex.InnerException?.Message ?? string.Empty;
            return msg.Contains("Cannot insert duplicate key", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("duplicate key value violates unique constraint", StringComparison.OrdinalIgnoreCase);
        }

        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        try
        {
            await ctx.Response.WriteAsJsonAsync(problem);
        }
        catch (Exception writeEx)
        {
            // Serialization or transport failed. Fall back to a static plain-text body so
            // the client at least sees the status code and correlation ID.
            _logger.LogError(writeEx,
                "Failed to serialize ProblemDetails response. CorrelationId={CorrelationId}", correlationId);
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.WriteAsync(
                    $"Internal server error. CorrelationId={correlationId}");
            }
        }
    }
}
