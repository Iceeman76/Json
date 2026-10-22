using System.Buffers;
using System.Net.Http.Headers;
using Json;

namespace WebApi.Selector;

public sealed class SelectorMiddleware(RequestDelegate next, ILogger<SelectorMiddleware> logger)
{
    private const string SelectorHeaderName = "X-Selector";

    public async Task InvokeAsync(HttpContext context)
    {
        var selector = context.Request.Headers[SelectorHeaderName].ToString();

        if (string.IsNullOrWhiteSpace(selector))
        {
            logger.LogDebug("No selector header present, passing response through unchanged");
            await next(context);
            return;
        }

        logger.LogInformation("Applying selector {Selector} to {Path}", selector, context.Request.Path);

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
            logger.LogDebug("Selector skipped for response with status {StatusCode} and content type {ContentType}", context.Response.StatusCode, context.Response.ContentType);
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted);
            return;
        }

        var source = new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length);

        var projection = new ArrayBufferWriter<byte>();
        new JsonFieldSelector(source).Select(selector, projection);

        context.Response.ContentLength = projection.WrittenCount;
        await originalBody.WriteAsync(projection.WrittenMemory, context.RequestAborted);

        logger.LogInformation("Selector applied, response reduced from {OriginalBytes} to {ProjectedBytes} bytes", buffer.Length, projection.WrittenCount);
    }

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
