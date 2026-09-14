using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantDelivery.Api.Infrastructure;

// Catches anything that escapes a controller. Without it, an unhandled exception got
// ASP.NET Core's built-in bare 500: no log line tying it to a request, and a body the
// Angular client couldn't read, so a customer saw a blank failure and there was no way to
// find out afterwards what had happened to them.
//
// Deliberately does NOT include exception details in the response. The message and stack
// go to the log (where the trace id links them to the request that caused them); the
// caller gets the trace id and nothing else, so a stack trace can never reach a browser.
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // TraceIdentifier is the same value surfaced to the caller below and stamped on
        // every log line for this request (see the Serilog request-logging setup in
        // Program.cs), so a customer reporting "it failed and gave me this code" is enough
        // to find the exact failure.
        var traceId = httpContext.TraceIdentifier;

        _logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path} (trace {TraceId})",
            httpContext.Request.Method,
            httpContext.Request.Path,
            traceId);

        // A client that has already started receiving a response can't be sent a different
        // one - writing here would corrupt what it has. Returning false lets the host tear
        // the connection down instead; the log line above is still written either way.
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        problem.Extensions["traceId"] = traceId;

        // The Angular client reads err.error?.errors?.[0] everywhere (60-odd call sites),
        // which is the shape every validation failure and ServiceResult failure already
        // returns. Carrying the same key here means an unexpected failure surfaces as a
        // readable message in the UI rather than silently falling through to a blank
        // "something went wrong", while the rest of the payload stays valid RFC 7807.
        problem.Extensions["errors"] = new[]
        {
            $"Something went wrong on our end. Please try again. (reference {traceId})"
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
