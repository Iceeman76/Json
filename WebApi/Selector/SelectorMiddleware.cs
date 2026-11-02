using System.Buffers;
using System.Net.Http.Headers;
using DotNetLab.Json;

namespace WebApi.Selector;

public sealed partial class SelectorMiddleware(RequestDelegate next, ILogger<SelectorMiddleware> logger)
{
    private const string SelectorHeaderName = "X-Selector";

    public async Task InvokeAsync(HttpContext context)
    {
        var selector = context.Request.Headers[SelectorHeaderName].ToString();

        if (string.IsNullOrWhiteSpace(selector))
        {
            LogNoSelector(logger);
            await next(context);

            return;
        }

        LogApplyingSelector(logger, selector, context.Request.Path);

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        if (buffer.Length == 0 || HttpMethods.IsHead(context.Request.Method) || context.Response.StatusCode is < 200 or >= 300 || IsJsonResponse(context) is false)
        {
            LogSelectorSkipped(logger, context.Response.StatusCode, context.Response.ContentType);
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted);

            return;
        }

        var source = new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length);

        var projection = new ArrayBufferWriter<byte>();
        new JsonFieldSelector(source).Select(selector, projection);

        context.Response.ContentLength = projection.WrittenCount;
        await originalBody.WriteAsync(projection.WrittenMemory, context.RequestAborted);

        LogSelectorApplied(logger, buffer.Length, projection.WrittenCount);
    }

    [LoggerMessage(0, LogLevel.Debug, "No selector header present, passing response through unchanged")]
    private static partial void LogNoSelector(ILogger logger);

    [LoggerMessage(1, LogLevel.Information, "Applying selector {Selector} to {Path}")]
    private static partial void LogApplyingSelector(ILogger logger, string selector, PathString path);

    [LoggerMessage(2, LogLevel.Debug, "Selector skipped for response with status {StatusCode} and content type {ContentType}")]
    private static partial void LogSelectorSkipped(ILogger logger, int statusCode, string? contentType);

    [LoggerMessage(3, LogLevel.Information, "Selector applied, response reduced from {OriginalBytes} to {ProjectedBytes} bytes")]
    private static partial void LogSelectorApplied(ILogger logger, long originalBytes, int projectedBytes);

    private static bool IsJsonResponse(HttpContext context)
    {
        if (MediaTypeHeaderValue.TryParse(context.Response.ContentType, out var contentType) is false)
        {
            return false;
        }

        var mediaType = contentType.MediaType!;

        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.StartsWith("application/", StringComparison.OrdinalIgnoreCase) && mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }
}
