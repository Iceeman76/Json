using System.Text;
using Json;

namespace WebApi.Selector;

public sealed class SelectorMiddleware(RequestDelegate next)
{
    private const string SelectorHeaderName = "X-Selector";

    public async Task InvokeAsync(HttpContext context)
    {
        var selector = context.Request.Headers[SelectorHeaderName].ToString();

        if (string.IsNullOrWhiteSpace(selector))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await next(context);

        context.Response.Body = originalBody;

        if (IsJsonResponse(context) == false)
        {
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
            return;
        }

        buffer.Position = 0;
        var json = Encoding.UTF8.GetString(buffer.ToArray());
        var projected = new JsonFieldSelector(json).Select(selector);

        var projectedBytes = Encoding.UTF8.GetBytes(projected);
        context.Response.ContentLength = projectedBytes.Length;
        await originalBody.WriteAsync(projectedBytes);
    }

    private static bool IsJsonResponse(HttpContext context)
    {
        var contentType = context.Response.ContentType;
        return contentType is not null && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }
}
