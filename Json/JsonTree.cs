using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Json;

public sealed partial class JsonTree
{
    internal Node Root { get; }
    private Node[]? _flattened;
    private Dictionary<string, Node>? _index;

    private JsonTree(Node rootNode)
    {
        Root = rootNode;
        Root.AttachTo(this);
    }

    public static JsonTree Parse(string jsonString, int maxDepth = 64)
    {
        var documentOptions = new JsonDocumentOptions { MaxDepth = maxDepth };

        using var doc = JsonDocument.Parse(jsonString, documentOptions);

        return FromDocument(doc);
    }

    public static JsonTree Parse(ReadOnlyMemory<byte> utf8Json, int maxDepth = 64)
    {
        var documentOptions = new JsonDocumentOptions { MaxDepth = maxDepth };

        using var doc = JsonDocument.Parse(utf8Json, documentOptions);

        return FromDocument(doc);
    }

    public static JsonTree Parse(Stream utf8Json, int maxDepth = 64)
    {
        var documentOptions = new JsonDocumentOptions { MaxDepth = maxDepth };

        using var doc = JsonDocument.Parse(utf8Json, documentOptions);

        return FromDocument(doc);
    }

    private static JsonTree FromDocument(JsonDocument doc)
    {
        var tree = new JsonTree(new Node("$"));
        tree.DoTraverse(doc.RootElement, tree.Root);

        return tree;
    }

    public Node? FindNode(string jsonPath)
    {
        if (Root.JsonPath.Equals(jsonPath, StringComparison.OrdinalIgnoreCase))
        {
            return Root;
        }

        _index ??= Flatten().ToDictionary(x => x.JsonPath, StringComparer.OrdinalIgnoreCase);

        return _index.GetValueOrDefault(jsonPath);
    }

    private void DoTraverse(JsonElement element, Node node)
    {
        node.Kind = element.ValueKind;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                TraverseObject(element, node);
                break;
            case JsonValueKind.Array:
                TraverseArray(element, node);
                break;
            case JsonValueKind.Null:
                node.Value = null;
                break;
            default:
                node.Value = element.GetValue();
                break;
        }
    }

    private void TraverseObject(JsonElement element, Node parent)
    {
        foreach (var property in element.EnumerateObject())
        {
            var node = parent.AddNode(property.Name, isArrayIndex: false);
            DoTraverse(property.Value, node);
        }
    }

    private void TraverseArray(JsonElement element, Node parent)
    {
        var index = 0;

        foreach (var item in element.EnumerateArray())
        {
            var node = parent.AddNode(index.ToString(), isArrayIndex: true);
            DoTraverse(item, node);
            index++;
        }
    }

    public string ToJsonString(JsonWriterOptions options = default)
    {
        if (options.Encoder is null)
        {
            options = options with { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        }

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, options);

        writer.WriteStartNode(Root);

        foreach (var node in Root.Children)
        {
            node.BuildRecursiveJson(writer);
        }

        writer.WriteEndNode(Root);
        writer.Flush();

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public Node[] Flatten() => _flattened ??= Root.Flatten().ToArray();

    private void InvalidateCaches()
    {
        _flattened = null;
        _index = null;
    }

    public override string ToString() => Root.BuildBranchString();
}
