using System.Globalization;
using System.Text;
using Bunit;
using Demo.Pages;
using Microsoft.AspNetCore.Components;

namespace DemoTests;

public sealed class SelectorTests
{
    private BunitContext _context = null!;

    [SetUp]
    public void CreateContext()
    {
        _context = new BunitContext();
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [TearDown]
    public async Task DisposeContext() => await _context.DisposeAsync();

    [Test]
    public async Task Evaluates_the_latest_json_input_after_rapid_edits()
    {
        var component = _context.Render<Selector>();
        var pendingInputs = new List<Task>();
        await component.InvokeAsync(() =>
        {
            pendingInputs.Add(component.Find("#json-input").InputAsync(new ChangeEventArgs { Value = "{" }));
            pendingInputs.Add(component.Find("#json-input").InputAsync(new ChangeEventArgs { Value = "{\"name\":1}" }));
            pendingInputs.Add(component.Find("#json-input").InputAsync(new ChangeEventArgs { Value = "{\"name\":2}" }));
        });
        await Task.WhenAll(pendingInputs);

        Assert.That(component.Find("#result-output").TextContent, Does.Contain("2"));
        Assert.That(component.Find(".validity").ClassList, Does.Contain("valid"));
    }

    [Test]
    public async Task Recovers_after_invalid_json_is_corrected()
    {
        var component = _context.Render<Selector>();
        await component.Find("#json-input").InputAsync(new ChangeEventArgs { Value = "{" });

        Assert.That(component.Find("#result-output").ClassList, Does.Contain("output-error"));

        await component.Find("#json-input").InputAsync(new ChangeEventArgs { Value = "42" });

        Assert.That(component.Find("#result-output").TextContent, Is.EqualTo("42"));
        Assert.That(component.Find("#result-output").ClassList, Does.Not.Contain("output-error"));
    }

    [Test]
    public async Task Query_edits_evaluate_the_current_json()
    {
        var component = _context.Render<Selector>();
        await component.Find("#json-input").InputAsync(new ChangeEventArgs { Value = "{\"first name\":\"Alice\",\"age\":30}" });
        await component.Find("#selector-input").InputAsync(new ChangeEventArgs { Value = "$['first name']" });

        Assert.That(component.Find("#result-output").TextContent, Is.EqualTo("\"Alice\""));
    }

    [Test]
    public async Task Formatting_input_preserves_minified_result_and_displayed_byte_count()
    {
        var component = _context.Render<Selector>();
        await component.Find("#json-input").InputAsync(new ChangeEventArgs { Value = "{\"name\":\"Åsa\",\"items\":[1,2]}" });
        component.Find("button[title='Minify result']").Click();
        var result = component.Find("#result-output").TextContent;
        component.Find("button[title='Format JSON']").Click();

        Assert.That(component.Find("#result-output").TextContent, Is.EqualTo(result));
        Assert.That(component.Find("#output-gutter").TextContent.Trim(), Is.EqualTo("1"));
        var expectedBytes = Encoding.UTF8.GetByteCount(result).ToString("#,0", CultureInfo.InvariantCulture);

        Assert.That(component.FindAll(".panel-header")[1].TextContent, Does.Contain($"{expectedBytes} bytes"));
    }

    [Test]
    public async Task Formatting_and_tree_view_preserve_exact_numbers()
    {
        var component = _context.Render<Selector>();
        await component.Find("#json-input").InputAsync(new ChangeEventArgs { Value = "{\"number\":0.123456789012345678901234567891}" });
        component.Find("button[title='Format result']").Click();

        Assert.That(component.Find("#result-output").TextContent, Does.Contain("0.123456789012345678901234567891"));

        component.FindAll(".btn-group button")[1].Click();

        Assert.That(component.Find("#result-output").TextContent, Does.Contain("0.123456789012345678901234567891"));
    }

    [Test]
    public async Task Selecting_a_sample_cancels_pending_input_evaluation()
    {
        var component = _context.Render<Selector>();
        var pendingInput = Task.CompletedTask;
        await component.InvokeAsync(() =>
        {
            pendingInput = component.Find("#json-input").InputAsync(new ChangeEventArgs { Value = "{" });
            component.Find("#example-select").Change("Entire document");
        });
        await pendingInput;

        Assert.That(component.Find("#result-output").TextContent, Does.Contain("Junie"));
        Assert.That(component.Find("#result-output").ClassList, Does.Not.Contain("output-error"));
    }

    [Test]
    public async Task Disposing_the_component_cancels_pending_evaluation()
    {
        var component = _context.Render<Selector>();
        var pendingInput = component.Find("#json-input").InputAsync(new ChangeEventArgs { Value = "{}" });
        await _context.DisposeComponentsAsync();

        Assert.That(async () => await pendingInput, Throws.Nothing);
    }
}
