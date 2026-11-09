using System.Diagnostics;

namespace Aenaon.Api.Middleware;

/// <summary>Καταγράφει τη διάρκεια και την κατάσταση αιτημάτων χωρίς περιεχόμενο ή διαπιστευτήρια.</summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        long started = Stopwatch.GetTimestamp();
        bool failed = false;
        string traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["TraceId"] = traceId });

        try
        {
            await next(context);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            int statusCode = failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
            logger.LogInformation("HTTP {Method} returned {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method, statusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
