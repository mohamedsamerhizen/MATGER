using System.Text.Json;
using MATGER.Api.DTOs.Common;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var entries = exception.Entries
                .Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.State}")
                .ToArray();

            logger.LogError(
                exception,
                "Database concurrency exception occurred. TraceId: {TraceId}. Entries: {Entries}",
                context.TraceIdentifier,
                string.Join(", ", entries));

            await WriteErrorResponseAsync(
                context,
                StatusCodes.Status409Conflict,
                "The resource was updated by another request. Please retry.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled exception occurred. TraceId: {TraceId}",
                context.TraceIdentifier);

            await WriteErrorResponseAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorResponseAsync(
        HttpContext context,
        int statusCode,
        string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new ApiErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            TraceId = context.TraceIdentifier
        };

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        await context.Response.WriteAsync(json);
    }
}
