using Devsu.Accounts.Application.Exceptions;
using Devsu.Accounts.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Devsu.Accounts.Api.Errors;

internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int status, string title, string code) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unexpected error while processing {Method} {Path}. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning(
                "Request rejected with code {Code} for {Method} {Path}. TraceId: {TraceId}",
                code,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = status;

        ProblemDetails problemDetails = new()
        {
            Status = status,
            Title = title,
            Detail = status >= StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred while processing the request."
                : exception.Message,
            Instance = httpContext.Request.Path,
        };
        problemDetails.Extensions["code"] = code;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });
    }

    private static (int Status, string Title, string Code) Map(Exception exception)
    {
        return exception switch
        {
            ValidationException validation =>
                (StatusCodes.Status400BadRequest, "Invalid request", validation.Code),
            BusinessRuleException businessRule =>
                (StatusCodes.Status400BadRequest, "Business rule violation", businessRule.Code),
            NotFoundException notFound =>
                (StatusCodes.Status404NotFound, "Resource not found", notFound.Code),
            ConflictException conflict =>
                (StatusCodes.Status409Conflict, "Conflict", conflict.Code),
            _ =>
                (StatusCodes.Status500InternalServerError, "Internal server error", "internal_error"),
        };
    }
}
