using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Demo.Pages;

public partial class Selector
{
    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    private string JsonInput { get; set; } = "";
    private string SelectorInput { get; set; } = "$";
    private string Result { get; set; } = "";
    private string? ErrorMessage { get; set; }
    private bool QueryCopied { get; set; }
    private CancellationTokenSource? _copyResetCts;

    private async Task CopyQuery()
    {
        if (string.IsNullOrEmpty(SelectorInput))
        {
            return;
        }

        var copied = await Js.InvokeAsync<bool>("clipboardCopy.copyText", SelectorInput);

        if (!copied)
        {
            return;
        }

        QueryCopied = true;

        _copyResetCts?.Cancel();
        _copyResetCts = new CancellationTokenSource();
        var token = _copyResetCts.Token;

        try
        {
            await Task.Delay(1500, token);
            QueryCopied = false;
            StateHasChanged();
        }
        catch (TaskCanceledException)
        {
            // A newer copy started; ignore.
        }
    }

    private bool JsonPasted { get; set; }
    private CancellationTokenSource? _pasteResetCts;

    private async Task PasteJson()
    {
        var text = await Js.InvokeAsync<string?>("clipboardCopy.readText");

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        JsonInput = text;
        ApplySelector();

        JsonPasted = true;

        _pasteResetCts?.Cancel();
        _pasteResetCts = new CancellationTokenSource();
        var token = _pasteResetCts.Token;

        try
        {
            await Task.Delay(1500, token);
            JsonPasted = false;
            StateHasChanged();
        }
        catch (TaskCanceledException)
        {
            // A newer paste started; ignore.
        }
    }

    private enum ResultView
    {
        Json,
        Tree
    }

    private ResultView View { get; set; } = ResultView.Json;

    private string OutputText
    {
        get
        {
            if (ErrorMessage is not null)
            {
                return ErrorMessage;
            }

            if (string.IsNullOrEmpty(Result) || View == ResultView.Json)
            {
                return Result;
            }

            try
            {
                return JsonTree.Parse(Result).ToString();
            }
            catch
            {
                return Result;
            }
        }
    }

    private void SetView(ResultView view) => View = view;

    private MarkupString HighlightedOutput => (MarkupString)HighlightJson(OutputText);

    private static readonly JsonSerializerOptions StringLiteralOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private string HighlightJson(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        if (ErrorMessage is not null || View == ResultView.Tree)
        {
            return WebUtility.HtmlEncode(text);
        }

        try
        {
            var root = JsonTree.Parse(text).FindNode("$");

            if (root is null)
            {
                return WebUtility.HtmlEncode(text);
            }

            var sb = new StringBuilder(text.Length + 64);
            RenderNode(root, sb, 0);

            return sb.ToString();
        }
        catch
        {
            return WebUtility.HtmlEncode(text);
        }
    }

    private void RenderNode(JsonTree.Node node, StringBuilder sb, int indent)
    {
        switch (node.Kind)
        {
            case JsonValueKind.Object:
                RenderObject(node, sb, indent);
                break;
            case JsonValueKind.Array:
                RenderArray(node, sb, indent);
                break;
            default:
                RenderScalar(node, sb);
                break;
        }
    }

    private void RenderObject(JsonTree.Node node, StringBuilder sb, int indent)
    {
        if (node.Children.Count == 0)
        {
            sb.Append("{}");
            return;
        }

        sb.AppendLine("{");

        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            AppendIndent(sb, indent + 1);
            sb.Append("<span class=\"tok-key\" title=\"")
                .Append(WebUtility.HtmlEncode(child.JsonPath))
                .Append("\">")
                .Append(WebUtility.HtmlEncode(ToJsonStringLiteral(child.Name)))
                .Append("</span>: ");
            RenderNode(child, sb, indent + 1);

            if (i < node.Children.Count - 1)
            {
                sb.Append(',');
            }

            sb.AppendLine();
        }

        AppendIndent(sb, indent);
        sb.Append('}');
    }

    private void RenderArray(JsonTree.Node node, StringBuilder sb, int indent)
    {
        if (node.Children.Count == 0)
        {
            sb.Append("[]");
            return;
        }

        sb.AppendLine("[");

        for (var i = 0; i < node.Children.Count; i++)
        {
            AppendIndent(sb, indent + 1);
            RenderNode(node.Children[i], sb, indent + 1);

            if (i < node.Children.Count - 1)
            {
                sb.Append(',');
            }

            sb.AppendLine();
        }

        AppendIndent(sb, indent);
        sb.Append(']');
    }

    private static void RenderScalar(JsonTree.Node node, StringBuilder sb)
    {
        var title = WebUtility.HtmlEncode(node.JsonPath);

        switch (node.Kind)
        {
            case JsonValueKind.String:
                sb.Append("<span class=\"tok-string\" title=\"").Append(title).Append("\">")
                    .Append(WebUtility.HtmlEncode(ToJsonStringLiteral(node.Value as string ?? "")))
                    .Append("</span>");
                break;
            case JsonValueKind.Number:
                sb.Append("<span class=\"tok-number\" title=\"").Append(title).Append("\">")
                    .Append(FormatNumber(node.Value))
                    .Append("</span>");
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                sb.Append("<span class=\"tok-bool\" title=\"").Append(title).Append("\">")
                    .Append(node.Kind == JsonValueKind.True ? "true" : "false")
                    .Append("</span>");
                break;
            default:
                sb.Append("<span class=\"tok-null\" title=\"").Append(title).Append("\">null</span>");
                break;
        }
    }

    private static void AppendIndent(StringBuilder sb, int indent) => sb.Append(' ', indent * 2);
    private static string ToJsonStringLiteral(string value) => JsonSerializer.Serialize(value, StringLiteralOptions);

    private static string FormatNumber(object? value) => value switch
    {
        long l => l.ToString(CultureInfo.InvariantCulture),
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        double db => db.ToString(CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => WebUtility.HtmlEncode(value?.ToString() ?? "0")
    };

    private record Example(string Name, string Json, string Selector);

    private static readonly Example[] Examples =
    [
        new("Entire document",
            """
            {
              "name": "Junie",
              "age": 3
            }
            """,
            "$"),
        new("Property access",
            """
            {
              "name": "Junie",
              "age": 3
            }
            """,
            "$.name"),
        new("Nested property",
            """
            {
              "user": {
                "name": "Ada",
                "city": "Stockholm"
              }
            }
            """,
            "$.user.city"),
        new("Array index",
            """
            {
              "items": [10, 20, 30]
            }
            """,
            "$.items[0]"),
        new("Negative index",
            """
            {
              "items": [10, 20, 30]
            }
            """,
            "$.items[-1]"),
        new("Property in array element",
            """
            {
              "users": [
                { "name": "Ada" },
                { "name": "Bob" }
              ]
            }
            """,
            "$.users[1].name"),
        new("Nested arrays",
            """
            {
              "matrix": [[1, 2], [3, 4]]
            }
            """,
            "$.matrix[1][0]"),
        new("All properties (wildcard)",
            """
            {
              "name": "Ada",
              "age": 30
            }
            """,
            "$.*"),
        new("Property from all elements",
            """
            {
              "users": [
                { "name": "Ada" },
                { "name": "Bob" }
              ]
            }
            """,
            "$.users[*].name"),
        new("Chained wildcards",
            """
            {
              "teams": [
                { "players": [{ "name": "Ada" }, { "name": "Bob" }] },
                { "players": [{ "name": "Cy" }] }
              ]
            }
            """,
            "$.teams[*].players[*].name"),
        new("Field projection",
            """
            {
              "person": {
                "name": "Ada",
                "age": 30,
                "ssn": "secret"
              }
            }
            """,
            "$.person[name,age]"),
        new("Root projection",
            """
            {
              "name": "Ada",
              "age": 30,
              "ssn": "secret"
            }
            """,
            "$[name,age]"),
        new("Field alias",
            """
            {
              "name": "Ada",
              "age": 30
            }
            """,
            "$[displayName:name,age]"),
        new("Alias on dotted path",
            """
            {
              "events": [
                { "name": "A", "league": { "name": "Premier" } },
                { "name": "B", "league": { "name": "Championship" } }
              ]
            }
            """,
            "$.events[name,league:league.name]"),
        new("Indexed access in projection",
            """
            {
              "participants": [
                { "name": "KHK" },
                { "name": "FBK" }
              ]
            }
            """,
            "$[home:participants[0].name,away:participants[1].name]"),
        new("Projection over array",
            """
            {
              "books": [
                { "title": "A", "price": 100, "isbn": "x1" },
                { "title": "B", "price": 150, "isbn": "x2" }
              ]
            }
            """,
            "$.books[*][title,price]"),
        new("Nested sub-projection",
            """
            {
              "order": {
                "id": 7,
                "customer": {
                  "name": "Ada",
                  "email": "ada@example.com",
                  "phone": "123"
                }
              }
            }
            """,
            "$.order[id,customer[name,email]]")
    ];

    private void OnExampleSelected(ChangeEventArgs e)
    {
        var name = e.Value?.ToString();
        var example = Examples.FirstOrDefault(x => x.Name == name);

        if (example is null)
        {
            ClearInputs();
            return;
        }

        JsonInput = example.Json;
        SelectorInput = example.Selector;

        ApplySelector();
    }

    private void ClearInputs()
    {
        JsonInput = "";
        SelectorInput = "";
        Result = "";
        ErrorMessage = null;
    }

    private void ApplySelector()
    {
        ErrorMessage = null;
        Result = "";

        if (string.IsNullOrWhiteSpace(JsonInput) || string.IsNullOrWhiteSpace(SelectorInput))
        {
            return;
        }

        try
        {
            var fieldSelector = new JsonFieldSelector(JsonInput);
            Result = fieldSelector.Select(SelectorInput);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
