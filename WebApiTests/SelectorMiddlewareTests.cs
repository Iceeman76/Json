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

    [TestCase(400)]
    [TestCase(404)]
    [TestCase(500)]
    public async Task Preserves_error_responses_instead_of_projecting_them(int statusCode)
    {
        const string error = """{"errors":{"limit":["Limit is too large."]}}""";
        var context = CreateContext("$.person[name]");
        context.Response.StatusCode = statusCode;

        await Invoke(context, WriteJson(error));

        Assert.That(ReadBody(context), Is.EqualTo(error));
        Assert.That(context.Response.StatusCode, Is.EqualTo(statusCode));
    }

    [TestCase(200)]
    [TestCase(204)]
    public async Task Passes_empty_responses_through_without_parsing_json(int statusCode)
    {
        var context = CreateContext("$.person[name]");
        context.Response.StatusCode = statusCode;

        await Invoke(context, WriteJson(""));

        Assert.That(ReadBody(context), Is.Empty);
    }

    [Test]
    public async Task Does_not_apply_selectors_to_head_responses()
    {
        var context = CreateContext("$.person[name]");
        context.Request.Method = "HEAD";

        await Invoke(context, WriteJson(Json));

        Assert.That(ReadBody(context), Is.EqualTo(Json));
    }

    [Test]
    public async Task Leaves_jsonp_responses_unchanged()
    {
        var context = CreateContext("$.person[name]");

        await Invoke(context, async responseContext =>
        {
            responseContext.Response.ContentType = "application/jsonp";
            await responseContext.Response.WriteAsync("callback({});");
        });

        Assert.That(ReadBody(context), Is.EqualTo("callback({});"));
    }

    [Test]
    public async Task Projects_json_responses_with_vendor_media_types()
    {
        var context = CreateContext("$.person[name]");

        await Invoke(context, async responseContext =>
        {
            responseContext.Response.ContentType = "application/vnd.example+json; charset=utf-8";
            await responseContext.Response.WriteAsync(Json);
        });

        Assert.That(ReadBody(context), Is.EqualTo("""{"name":"Alice"}"""));
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
