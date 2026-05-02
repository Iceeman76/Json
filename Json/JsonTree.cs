using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Json;

public sealed partial class JsonTree
{
    private readonly Node _rootNode;
    private Node[]? _flattened;
    private Dictionary<string, Node>? _index;
    private Dictionary<string, Node>? _indexIgnoreCase;

    private JsonTree(Node rootNode)
    {
        _rootNode = rootNode;
    }

    public const int DefaultMaxDepth = 64;

    public static JsonTree Create(string jsonString, string rootName = "$", int maxDepth = DefaultMaxDepth)
    {
        var documentOptions = new JsonDocumentOptions { MaxDepth = maxDepth };

        var rootNode = new Node(rootName);
        var tree = new JsonTree(rootNode);

        using var doc = JsonDocument.Parse(jsonString, documentOptions);
        tree.DoTraverse(doc.RootElement, tree._rootNode);

        return tree;
    }

    public Node? FindNode(string identity, StringComparison comparison = StringComparison.Ordinal)
    {
        if (_rootNode.Identity.Equals(identity, comparison))
        {
            return _rootNode;
        }

        if (comparison is StringComparison.OrdinalIgnoreCase)
        {
            _indexIgnoreCase ??= Flatten().ToDictionary(x => x.Identity, StringComparer.OrdinalIgnoreCase);
            return _indexIgnoreCase.GetValueOrDefault(identity);
        }

        _index ??= Flatten().ToDictionary(x => x.Identity, StringComparer.Ordinal);
        return _index.GetValueOrDefault(identity);
    }

    private void DoTraverse(JsonElement element, Node node)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                node.Kind = JsonValueKind.Object;
                TraverseObject(element, node);
                break;
            case JsonValueKind.Array:
                node.Kind = JsonValueKind.Array;
                TraverseArray(element, node);
                break;
            case JsonValueKind.Null:
                node.Kind = JsonValueKind.Null;
                break;
            default:
                node.Value = element.ToString();
                node.Kind = element.ValueKind;
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
            node.InArray = true;
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

        writer.WriteStartNode(_rootNode);

        foreach (var node in _rootNode.Children)
        {
            node.BuildRecursiveJson(writer);
        }

        writer.WriteEndNode(_rootNode);
        writer.Flush();

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public Node[] Flatten() => _flattened ??= _rootNode.Flatten().ToArray();

    public override string ToString() => _rootNode.BuildBranchString();
}
