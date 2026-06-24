using System.Diagnostics;

namespace MATGER.Api.Middleware;

public sealed class RequestTracingMiddleware(RequestDelegate next)
{
    private const string TraceIdHeader = "X-Trace-Id";
    private const string RequestDurationHeader = "X-Request-Duration-Ms";

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        context.Response.OnStarting(() =>
        {
            stopwatch.Stop();

            context.Response.Headers[TraceIdHeader] = context.TraceIdentifier;
            context.Response.Headers[RequestDurationHeader] = stopwatch.ElapsedMilliseconds.ToString();

            return Task.CompletedTask;
        });

        await next(context);
    }
}
