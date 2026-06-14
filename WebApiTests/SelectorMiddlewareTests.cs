using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Selector;

namespace WebApiTests;

public class SelectorMiddlewareTests
{
    private const string Json = """{"person":{"name":"Alice","age":30}}""";

    [Test]
    public async Task Passes_response_through_unchanged_when_no_selector_header()
    {
        var context = CreateContext(selector: null);

        await Invoke(context, WriteJson(Json));

        Assert.That(ReadBody(context), Is.EqualTo(Json));
    }

    [Test]
    public async Task Skips_selector_for_non_json_response()
    {
        var context = CreateContext(selector: "$.person[name]");

        await Invoke(context, WritePlainText("hello"));

        Assert.That(ReadBody(context), Is.EqualTo("hello"));
    }

    [Test]
    public async Task Applies_projection_for_json_response()
    {
        var context = CreateContext(selector: "$.person[name]");

        await Invoke(context, WriteJson(Json));

        Assert.That(ReadBody(context), Is.EqualTo("""{"name":"Alice"}"""));
    }

    [Test]
    public void Throws_when_selector_syntax_is_invalid()
    {
        var context = CreateContext(selector: "not-a-valid-selector");

        Assert.That(async () => await Invoke(context, WriteJson(Json)), Throws.TypeOf<FormatException>());
    }

    private static Task Invoke(HttpContext context, RequestDelegate next)
    {
        var middleware = new SelectorMiddleware(next, NullLogger<SelectorMiddleware>.Instance);

        return middleware.InvokeAsync(context);
    }

    private static RequestDelegate WriteJson(string json) => async context =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(json));
    };

    private static RequestDelegate WritePlainText(string text) => async context =>
    {
        context.Response.ContentType = "text/plain";
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(text));
    };

    private static DefaultHttpContext CreateContext(string? selector)
    {
        var context = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() }
        };

        if (selector is not null)
        {
            context.Request.Headers["X-Selector"] = selector;
        }

        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        var body = (MemoryStream)context.Response.Body;
        return Encoding.UTF8.GetString(body.ToArray());
    }
}
