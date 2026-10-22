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

public sealed partial class Selector : IDisposable
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
    private bool ShowDescription { get; set; }
    private Example? SelectedExample { get; set; }
    private bool _guttersRegistered;
    private string? _parsedInput;
    private string? _inputError;
    private JsonTree? _inputTree;
    private JsonFieldSelector? _fieldSelector;
    private JsonTree? _resultTree;
    private (string Result, string? Error, ResultView View, bool Minified)? _renderedOutputKey;
    private string _displayedOutput = "";
    private string _highlightedOutput = "";
    private string _outputGutter = "";

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
    private MarkupString OutputGutter
    {
        get
        {
            EnsureOutput();

            return (MarkupString)_outputGutter;
        }
    }

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

    private string DisplayedResultText
    {
        get
        {
            EnsureOutput();

            return _displayedOutput;
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

        ResultMinified = indent is false;
    }

    private void ReformatJson(bool indent)
    {
        if (string.IsNullOrWhiteSpace(JsonInput))
        {
            return;
        }

        EnsureInput();

        if (_inputTree is null)
        {
            ErrorMessage = _inputError;

            return;
        }

        JsonInput = _inputTree.ToJsonString(new JsonWriterOptions { Indented = indent, NewLine = "\n" });
        _parsedInput = JsonInput;
        var resultMinified = ResultMinified;
        ApplySelector();
        ResultMinified = resultMinified;
    }

    private void ToggleCheatSheet() => ShowCheatSheet = !ShowCheatSheet;

    private void ToggleDescription() => ShowDescription = !ShowDescription;

    private void ApplyCheatExpression(string expression)
    {
        SelectorInput = expression;
        ApplySelector();
    }

    private sealed record CheatRow(string Expression, string Description);

    private static readonly CheatRow[] CheatSheet =
    [
        new("$", "Entire document"),
        new("$.a.b", "Scalar value or sub-object at path"),
        new("$.items[*].name", "Array of name values from every element"),
        new("$.items[*].category|distinct", "Distinct category values, preserving first occurrence order"),
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

    private void EnsureOutput()
    {
        var key = (Result, ErrorMessage, View, ResultMinified);

        if (_renderedOutputKey == key)
        {
            return;
        }

        _renderedOutputKey = key;
        _displayedOutput = ErrorMessage ?? "";

        if (ErrorMessage is null && _resultTree is not null)
        {
            _displayedOutput = View == ResultView.Tree ? _resultTree.ToString().Replace("\r\n", "\n") : _resultTree.ToJsonString(new JsonWriterOptions { Indented = ResultMinified is false, NewLine = "\n" });
        }

        if (ErrorMessage is null && View == ResultView.Json && _resultTree is not null)
        {
            var builder = new StringBuilder(_displayedOutput.Length + 64);
            RenderNode(_resultTree.FindNode("$")!, builder, 0);
            _highlightedOutput = builder.ToString();
        }
        else
        {
            _highlightedOutput = WebUtility.HtmlEncode(_displayedOutput);
        }

        _outputGutter = BuildGutter(CountLines(_displayedOutput)).Value;
    }

    private void SetView(ResultView view) => View = view;

    private MarkupString HighlightedOutput
    {
        get
        {
            EnsureOutput();

            return (MarkupString)_highlightedOutput;
        }
    }

    private static readonly JsonSerializerOptions StringLiteralOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

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
            sb.Append('\n');
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

    private sealed record Example(string Name, string Json, string Selector, string Description);

    private static readonly Example[] Examples =
    [
        new("Entire document",
            """
            {
              "name": "Junie",
              "age": 3
            }
            """,
            "$",
            "The root selector $ selects the entire document."),
        new("Property access",
            """
            {
              "name": "Junie",
              "age": 3
            }
            """,
            "$.name",
            "Reads a single property from the root object by name."),
        new("Nested property",
            """
            {
              "user": {
                "name": "Ada",
                "city": "Stockholm"
              }
            }
            """,
            "$.user.city",
            "Walks down a dotted path to reach a value nested inside an object."),
        new("Array index",
            """
            {
              "items": [10, 20, 30]
            }
            """,
            "$.items[0]",
            "Selects a single array element by its zero-based index."),
        new("Negative index",
            """
            {
              "items": [10, 20, 30]
            }
            """,
            "$.items[-1]",
            "A negative index counts from the end of the array; -1 is the last element."),
        new("Property in array element",
            """
            {
              "users": [
                { "name": "Ada" },
                { "name": "Bob" }
              ]
            }
            """,
            "$.users[1].name",
            "Indexes into an array and then reads a property from that element."),
        new("Nested arrays",
            """
            {
              "matrix": [[1, 2], [3, 4]]
            }
            """,
            "$.matrix[1][0]",
            "Chains indices to reach into arrays nested inside other arrays."),
        new("All properties (wildcard)",
            """
            {
              "name": "Ada",
              "age": 30
            }
            """,
            "$.*",
            "The wildcard * returns all property values of an object as an array."),
        new("Property from all elements",
            """
            {
              "users": [
                { "name": "Ada" },
                { "name": "Bob" }
              ]
            }
            """,
            "$.users[*].name",
            "Collects the same property from every element of an array."),
        new("Chained wildcards",
            """
            {
              "teams": [
                { "players": [{ "name": "Ada" }, { "name": "Bob" }] },
                { "players": [{ "name": "Cy" }] }
              ]
            }
            """,
            "$.teams[*].players[*].name",
            "Applies wildcards across nested arrays and flattens the results."),
        new("Distinct scalar values",
            """
            {
              "products": [
                { "name": "Keyboard", "category": "Accessories" },
                { "name": "Mouse", "category": "Accessories" },
                { "name": "Monitor", "category": "Displays" },
                { "name": "Webcam", "category": "Accessories" }
              ]
            }
            """,
            "$.products[*].category|distinct",
            "Appends |distinct to keep only the first occurrence of each selected category."),
        new("Distinct recursive values",
            """
            {
              "primary": {
                "office": { "country": "Sweden" },
                "warehouse": { "country": "Norway" }
              },
              "secondary": {
                "office": { "country": "Sweden" },
                "warehouse": { "country": "Denmark" }
              }
            }
            """,
            "$..country|distinct",
            "Combines recursive descent with |distinct to collect unique values from any depth."),
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
            "$.person[name,age]",
            "A projection keeps only the listed fields and drops the rest."),
        new("Root projection",
            """
            {
              "name": "Ada",
              "age": 30,
              "ssn": "secret"
            }
            """,
            "$[name,age]",
            "Projects the listed fields directly from the root object."),
        new("Field alias",
            """
            {
              "displayName": "Ada",
              "age": 30
            }
            """,
            "$[name:displayName,age]",
            "An alias renames a projected field; here displayName is emitted as name."),
        new("Alias on dotted path",
            """
            {
              "teams": [
                { "name": "Everton", "league": { "name": "Premier" } },
                { "name": "Blackburn", "league": { "name": "Championship" } }
              ]
            }
            """,
            "$.teams[name,league:league.name]",
            "A projected field can descend a dotted path and be given an alias."),
        new("Indexed access in projection",
            """
            {
              "participants": [
                { "name": "KHK" },
                { "name": "FBK" }
              ]
            }
            """,
            "$[home:participants[0].name,away:participants[1].name]",
            "Projection fields can index into arrays and combine that with aliases."),
        new("Projection over array",
            """
            {
              "books": [
                { "title": "A", "price": 100, "isbn": "x1" },
                { "title": "B", "price": 150, "isbn": "x2" }
              ]
            }
            """,
            "$.books[*][title,price]",
            "Projects each element of an array down to a chosen set of fields."),
        new("Distinct projected objects",
            """
            {
              "orders": [
                { "id": 1, "customer": "Ada", "status": "Open" },
                { "id": 2, "customer": "Bo", "status": "Open" },
                { "id": 3, "customer": "Cy", "status": "Closed" },
                { "id": 4, "customer": "Di", "status": "Open" }
              ]
            }
            """,
            "$.orders[*][status]|distinct",
            "Distinct is applied after projection, so objects that produce the same projected shape are emitted once."),
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
            "$.order[id,customer[name,email]]",
            "A projection can nest another projection to shape sub-objects."),
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
            "$..price",
            "Recursive descent (..) finds every price property at any depth; the result is always an array."),
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
            "$..books[*][title,price]",
            "Finds every matching array at any depth and projects each element."),
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
            "$..[0]",
            "Selects the first element of every array found at any depth."),
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
            "$.store..price",
            "Scopes a recursive descent to a branch, so only prices under store match."),
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
            "$..[*]",
            "Returns every array element found anywhere in the document."),
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
            "$..orders..price",
            "Two recursive descents chain together: prices found under any orders node."),
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
            "$..contact[name,email]",
            "Applies a projection to every node matched by a recursive descent."),
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
            "$.order[id,buyer:customer.name,lines[sku,price]]",
            "Combines aliases, dotted paths and nested projections to reshape an order."),
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
            "$..[cost:price]",
            "Renames values gathered by a recursive descent; every price is emitted as cost.")
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

        SelectedExample = example;
        JsonInput = example.Json;
        SelectorInput = example.Selector;

        FormatJson();
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
        _inputTree = null;
        _fieldSelector = null;
        _resultTree = null;
        _parsedInput = null;
        JsonValid = null;
        ResultMinified = false;
        SelectedExample = null;
        ShowDescription = false;
    }

    private const int SelectorDebounceMs = 225;
    private CancellationTokenSource? _selectorDebounceCts;

    private async Task ApplySelectorDebounced()
    {
        var previous = _selectorDebounceCts;
        var cts = new CancellationTokenSource();
        _selectorDebounceCts = cts;
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            await Task.Delay(SelectorDebounceMs, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (cts.IsCancellationRequested || ReferenceEquals(_selectorDebounceCts, cts) is false)
        {
            return;
        }

        ApplySelector();
        StateHasChanged();
    }

    private void ApplySelector()
    {
        _selectorDebounceCts?.Cancel();
        ErrorMessage = null;
        Result = "";
        _resultTree = null;
        _renderedOutputKey = null;
        ElapsedMs = null;
        ResultCount = 0;
        ResultMinified = false;

        EnsureInput();

        if (JsonValid is false)
        {
            ErrorMessage = _inputError;

            return;
        }

        if (string.IsNullOrWhiteSpace(JsonInput) || string.IsNullOrWhiteSpace(SelectorInput))
        {
            return;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            Result = _fieldSelector!.Select(SelectorInput);
            stopwatch.Stop();

            ElapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            _resultTree = JsonTree.Parse(Result);
            var root = _resultTree.FindNode("$")!;
            ResultCount = root.Kind == JsonValueKind.Array ? root.Children.Count : 1;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void EnsureInput()
    {
        if (string.Equals(_parsedInput, JsonInput, StringComparison.Ordinal))
        {
            return;
        }

        _parsedInput = JsonInput;
        _inputTree = null;
        _fieldSelector = null;
        _inputError = null;

        if (string.IsNullOrWhiteSpace(JsonInput))
        {
            JsonValid = null;
            return;
        }

        try
        {
            _inputTree = JsonTree.Parse(JsonInput);
            _fieldSelector = new JsonFieldSelector(_inputTree);
            JsonValid = true;
        }
        catch (JsonException exception)
        {
            JsonValid = false;
            _inputError = exception.Message;
        }
    }

    public void Dispose()
    {
        _selectorDebounceCts?.Cancel();
        _selectorDebounceCts?.Dispose();
        _copyResetCts?.Cancel();
        _copyResetCts?.Dispose();
        _pasteResetCts?.Cancel();
        _pasteResetCts?.Dispose();
    }
}
