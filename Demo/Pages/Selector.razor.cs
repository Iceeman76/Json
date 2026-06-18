using System.Diagnostics;
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

    private bool? JsonValid { get; set; }
    private double? ElapsedMs { get; set; }
    private int ResultCount { get; set; }
    private bool ShowCheatSheet { get; set; }
    private bool _guttersRegistered;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_guttersRegistered)
        {
            await Js.InvokeVoidAsync("lineNumbers.register", "json-input", "json-gutter");
            await Js.InvokeVoidAsync("lineNumbers.register", "result-output", "output-gutter");
            _guttersRegistered = true;
        }
    }

    private MarkupString InputGutter => BuildGutter(CountLines(JsonInput));
    private MarkupString OutputGutter => BuildGutter(CountLines(HighlightJson(OutputText)));

    private static int CountLines(string text) => string.IsNullOrEmpty(text) ? 1 : text.Split('\n').Length;

    private static MarkupString BuildGutter(int lines)
    {
        var sb = new StringBuilder(lines * 3);

        for (var i = 1; i <= lines; i++)
        {
            sb.Append(i).AppendLine();
        }

        return (MarkupString)sb.ToString();
    }

    private string JsonSizeMeta
    {
        get
        {
            if (string.IsNullOrEmpty(JsonInput))
            {
                return "";
            }

            var bytes = Encoding.UTF8.GetByteCount(JsonInput);

            return bytes == 1 ? "1 byte" : $"{bytes.ToString("#,0", CultureInfo.InvariantCulture)} bytes";
        }
    }

    private string ResultMeta
    {
        get
        {
            if (ErrorMessage is not null || string.IsNullOrEmpty(Result))
            {
                return "";
            }

            var matches = ResultCount == 1 ? "1 match" : $"{ResultCount} matches";
            var ms = ElapsedMs is { } e ? $" · {e.ToString("0.00", CultureInfo.InvariantCulture)} ms" : "";

            return matches + ms;
        }
    }

    private string ResultSizeMeta
    {
        get
        {
            if (ErrorMessage is not null || string.IsNullOrEmpty(Result))
            {
                return "";
            }

            var bytes = Encoding.UTF8.GetByteCount(DisplayedResultText);

            return bytes == 1 ? "1 byte" : $"{bytes.ToString("#,0", CultureInfo.InvariantCulture)} bytes";
        }
    }

    // The byte size shown for the result must match what is actually rendered, which is
    // re-formatted based on the current view and the Format/Minify (ResultMinified) state —
    // not the raw whitespace that happens to be in the Result string. This keeps the size
    // stable when the left-hand JSON is reformatted (which re-runs the selector).
    private string DisplayedResultText
    {
        get
        {
            var text = OutputText;

            if (string.IsNullOrEmpty(text) || View == ResultView.Tree)
            {
                return text;
            }

            try
            {
                using var doc = JsonDocument.Parse(text);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
                {
                    WriteIndented = !ResultMinified,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
            }
            catch
            {
                return text;
            }
        }
    }

    private void FormatJson() => ReformatJson(indent: true);
    private void MinifyJson() => ReformatJson(indent: false);

    private bool ResultMinified { get; set; }

    private void FormatResult() => ReformatResult(indent: true);
    private void MinifyResult() => ReformatResult(indent: false);

    private void ReformatResult(bool indent)
    {
        if (ErrorMessage is not null || string.IsNullOrWhiteSpace(Result))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(Result);
            Result = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
            {
                WriteIndented = indent,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            ResultMinified = !indent;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ReformatJson(bool indent)
    {
        if (string.IsNullOrWhiteSpace(JsonInput))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(JsonInput);
            JsonInput = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
            {
                WriteIndented = indent,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        // Reformatting the left-hand JSON re-runs the selector, but it must not change how the
        // result is presented. Remember the result's Format/Minify state and restore it so the
        // result (and its byte-size meta) stays stable.
        var resultMinified = ResultMinified;
        ApplySelector();
        ResultMinified = resultMinified;
    }

    private void ToggleCheatSheet() => ShowCheatSheet = !ShowCheatSheet;

    private void ApplyCheatExpression(string expression)
    {
        SelectorInput = expression;
        ApplySelector();
    }

    private record CheatRow(string Expression, string Description);

    private static readonly CheatRow[] CheatSheet =
    [
        new("$", "Entire document"),
        new("$.a.b", "Scalar value or sub-object at path"),
        new("$.items[*].name", "Array of name values from every element"),
        new("$.items.*", "Array of all property values of an object"),
        new("$..name", "Recursive descent — every name property at any depth (always an array)"),
        new("$..events[number,name]", "Recursive descent with a trailing projection per matched node"),
        new("$..books[*][title,price]", "Projection over recursive descent — flattens every matched array and projects each element"),
        new("$..*", "Recursive wildcard — every node (property value and array element) at any depth"),
        new("$.store..*", "Recursive wildcard scoped to a branch"),
        new("$..[*]", "Every array element at any depth"),
        new("$..[0]", "The first element of every array at any depth"),
        new("$..[-1]", "The last element of every array at any depth"),
        new("$.store..price", "Recursive descent in the middle of a path — only price under store"),
        new("$..orders..price", "Multiple recursive descents in one expression"),
        new("$..[cost:price]", "Recursive descent with alias — every price at any depth renamed to cost"),
        new("$.a[*].b[*].c", "Chained wildcards across nested arrays"),
        new("$.person[name,age]", "Object projected to the listed fields only"),
        new("$[name,age]", "Projection from the root object"),
        new("$.items[*][id,name]", "Array of projected objects"),
        new("$.items[id,name]", "Same as above — [*] is inferred when the target is an array"),
        new("$.order[id,customer[name,email]]", "Nested sub-projection inside an object"),
        new("$[id,user[name,posts[title,date]]]", "Sub-projection at any depth, including arrays nested inside objects"),
        new("$.events[description,league.name]", "Dotted path inside a projection — descends into nested objects"),
        new("$.events[description,league:league.name]", "Renames the projected field — emits as league instead of name"),
        new("$.participants[0]", "Specific array element by index"),
        new("$.participants[-1]", "Negative index — counted from the end (-1 is the last element)"),
        new("$.matrix[1][0]", "Chained indices in nested arrays"),
        new("$[home:participants[0].name]", "Indexed access inside a projection field, typically combined with an alias")
    ];

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

        sb.Append('{');
        NewLine(sb);

        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            AppendIndent(sb, indent + 1);
            sb.Append("<span class=\"tok-key\" title=\"")
                .Append(WebUtility.HtmlEncode(child.JsonPath))
                .Append("\">")
                .Append(WebUtility.HtmlEncode(ToJsonStringLiteral(child.Name)))
                .Append(ResultMinified ? "</span>:" : "</span>: ");
            RenderNode(child, sb, indent + 1);

            if (i < node.Children.Count - 1)
            {
                sb.Append(',');
            }

            NewLine(sb);
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

        sb.Append('[');
        NewLine(sb);

        for (var i = 0; i < node.Children.Count; i++)
        {
            AppendIndent(sb, indent + 1);
            RenderNode(node.Children[i], sb, indent + 1);

            if (i < node.Children.Count - 1)
            {
                sb.Append(',');
            }

            NewLine(sb);
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

    private void NewLine(StringBuilder sb)
    {
        if (!ResultMinified)
        {
            sb.AppendLine();
        }
    }

    private void AppendIndent(StringBuilder sb, int indent)
    {
        if (!ResultMinified)
        {
            sb.Append(' ', indent * 2);
        }
    }
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
            "$.order[id,customer[name,email]]"),
        new("Recursive descent",
            """
            {
              "store": {
                "price": 100,
                "book": {
                  "title": "JSON Basics",
                  "price": 20,
                  "details": { "price": 5 }
                },
                "bicycle": { "price": 200 }
              }
            }
            """,
            "$..price"),
        new("Projection over recursive descent",
            """
            {
              "store": {
                "books": [
                  { "title": "A", "price": 100, "isbn": "x1" },
                  { "title": "B", "price": 150, "isbn": "x2" }
                ],
                "warehouse": {
                  "books": [
                    { "title": "C", "price": 50, "isbn": "x3" }
                  ]
                }
              }
            }
            """,
            "$..books[*][title,price]"),
        new("Recursive wildcard & array descent",
            """
            {
              "store": {
                "price": 100,
                "tags": ["sale", "new"],
                "book": {
                  "price": 20,
                  "authors": ["Ada", "Bo"]
                }
              },
              "warehouse": {
                "price": 5,
                "tags": ["bulk"]
              }
            }
            """,
            "$..[0]"),
        new("Recursive descent in a branch",
            """
            {
              "store": {
                "price": 100,
                "book": { "price": 20 }
              },
              "other": {
                "price": 999
              }
            }
            """,
            "$.store..price"),
        new("All array elements at any depth",
            """
            {
              "a": [1, 2, 3],
              "b": {
                "c": [4, 5],
                "d": 9
              }
            }
            """,
            "$..[*]"),
        new("Multiple recursive descents",
            """
            {
              "a": {
                "orders": {
                  "price": 1,
                  "nested": { "price": 2 }
                }
              },
              "b": { "price": 999 }
            }
            """,
            "$..orders..price"),
        new("Projection per recursive match",
            """
            {
              "order": {
                "customer": { "name": "Ada", "email": "ada@x.com", "ssn": "s1" },
                "vendor": {
                  "name": "Acme",
                  "contact": { "name": "Bo", "email": "bo@x.com", "ssn": "s2" }
                }
              }
            }
            """,
            "$..contact[name,email]"),
        new("Real-world order summary",
            """
            {
              "order": {
                "id": 1001,
                "customer": {
                  "name": "Ada Lovelace",
                  "email": "ada@example.com",
                  "ssn": "secret"
                },
                "lines": [
                  { "sku": "A-1", "qty": 2, "price": 49.9 },
                  { "sku": "B-7", "qty": 1, "price": 199.0 }
                ]
              }
            }
            """,
            "$.order[id,buyer:customer.name,lines[sku,price]]"),
        new("Recursive descent with alias",
            """
            {
              "store": {
                "price": 100,
                "book": { "price": 20 }
              },
              "other": {
                "price": 999
              }
            }
            """,
            "$..[cost:price]")
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

        FormatJson();
        ApplySelector();
        FormatResult();
    }

    private void ClearInputs()
    {
        JsonInput = "";
        SelectorInput = "";
        Result = "";
        ErrorMessage = null;
        ElapsedMs = null;
        ResultCount = 0;
        JsonValid = null;
        ResultMinified = false;
    }

    private const int SelectorDebounceMs = 225;
    private CancellationTokenSource? _selectorDebounceCts;

    private async Task ApplySelectorDebounced()
    {
        await _selectorDebounceCts?.CancelAsync()!;
        _selectorDebounceCts?.Dispose();

        var cts = new CancellationTokenSource();
        _selectorDebounceCts = cts;

        try
        {
            await Task.Delay(SelectorDebounceMs, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (cts.IsCancellationRequested)
        {
            return;
        }

        ApplySelector();
        StateHasChanged();
    }

    private void ApplySelector()
    {
        ErrorMessage = null;
        Result = "";
        ElapsedMs = null;
        ResultCount = 0;
        ResultMinified = false;

        UpdateJsonValidity();

        if (string.IsNullOrWhiteSpace(JsonInput) || string.IsNullOrWhiteSpace(SelectorInput))
        {
            return;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var fieldSelector = new JsonFieldSelector(JsonInput);
            Result = fieldSelector.Select(SelectorInput);
            stopwatch.Stop();

            ElapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            ResultCount = CountMatches(Result);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void UpdateJsonValidity()
    {
        if (string.IsNullOrWhiteSpace(JsonInput))
        {
            JsonValid = null;
            return;
        }

        try
        {
            JsonTree.Parse(JsonInput);
            JsonValid = true;
        }
        catch
        {
            JsonValid = false;
        }
    }

    private static int CountMatches(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return 0;
        }

        try
        {
            var root = JsonTree.Parse(json).FindNode("$");

            if (root is null)
            {
                return 0;
            }

            return root.Kind == JsonValueKind.Array ? root.Children.Count : 1;
        }
        catch
        {
            return 0;
        }
    }
}
