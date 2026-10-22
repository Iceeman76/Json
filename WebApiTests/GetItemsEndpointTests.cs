using System.Net;
using System.Net.Http.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using WebApi.Items;
using WebApi.Selector;

namespace WebApiTests;

public sealed class GetItemsEndpointTests
{
    private WebApplication _application = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task StartApplication()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints(options => options.Assemblies = [typeof(GetItemsEndpoint).Assembly]);
        _application = builder.Build();
        _application.UseDefaultExceptionHandler();
        _application.UseMiddleware<SelectorMiddleware>();
        _application.UseFastEndpoints();
        _application.Urls.Add("http://127.0.0.1:0");
        await _application.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_application.Urls.Single()) };
    }

    [OneTimeTearDown]
    public async Task StopApplication()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    [TestCase(-1)]
    [TestCase(10_001)]
    [TestCase(int.MaxValue)]
    public async Task Rejects_limits_outside_the_allowed_range(int limit)
    {
        using var response = await _client.GetAsync($"/items?limit={limit}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase(0)]
    [TestCase(10_000)]
    public async Task Accepts_limits_at_the_boundaries(int limit)
    {
        var response = await _client.GetFromJsonAsync<GetItemsResponse>($"/items?limit={limit}");

        Assert.That(response!.ItemCount, Is.EqualTo(limit));
        Assert.That(response.Items, Has.Length.EqualTo(limit));
    }

    [Test]
    public async Task Uses_the_default_limit_when_omitted()
    {
        var response = await _client.GetFromJsonAsync<GetItemsResponse>("/items");

        Assert.That(response!.ItemCount, Is.EqualTo(1000));
    }

    [Test]
    public async Task Projects_responses_through_the_complete_middleware_pipeline()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/items?limit=1");
        request.Headers.Add("X-Selector", "$.items[name]");
        using var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("""[{"name":"Keyboard #1"}]"""));
    }

    [Test]
    public async Task Selector_headers_do_not_hide_limit_validation_errors()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/items?limit=10001");
        request.Headers.Add("X-Selector", "$.items[name]");
        using var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("errors"));
    }
}
