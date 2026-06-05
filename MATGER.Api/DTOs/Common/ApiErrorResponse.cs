namespace MATGER.Api.DTOs.Common;

public sealed class ApiErrorResponse
{
    public int StatusCode { get; init; }

    public string Message { get; init; } = string.Empty;

    public string TraceId { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}