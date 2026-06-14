using System.Text;
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

        await next(context);

        context.Response.Body = originalBody;

        if (IsJsonResponse(context) == false)
        {
            logger.LogDebug("Response content type {ContentType} is not JSON, selector skipped", context.Response.ContentType);
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
            return;
        }

        buffer.Position = 0;
        var json = Encoding.UTF8.GetString(buffer.ToArray());

        try
        {
            var projected = new JsonFieldSelector(json).Select(selector);

            var projectedBytes = Encoding.UTF8.GetBytes(projected);
            context.Response.ContentLength = projectedBytes.Length;
            await originalBody.WriteAsync(projectedBytes);

            logger.LogInformation("Selector applied, response reduced from {OriginalBytes} to {ProjectedBytes} bytes", buffer.Length, projectedBytes.Length);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to apply selector {Selector}, returning original response", selector);
            buffer.Position = 0;
            context.Response.ContentLength = buffer.Length;
            await buffer.CopyToAsync(originalBody);
        }
    }

    private static bool IsJsonResponse(HttpContext context)
    {
        var contentType = context.Response.ContentType;
        return contentType is not null && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }
}
