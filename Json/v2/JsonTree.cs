using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Json.v2;

public sealed partial class JsonTree : IJsonTree
{
    private readonly Node _rootNode;
    private IJsonTreeNode[]? _flattened;
    private Dictionary<string, IJsonTreeNode>? _index;

    private JsonTree(Node rootNode)
    {
        _rootNode = rootNode;
    }

    public static JsonTree Create(string jsonString, int maxDepth = 64)
    {
        var documentOptions = new JsonDocumentOptions { MaxDepth = maxDepth };

        var rootNode = new Node("$");
        var tree = new JsonTree(rootNode);

        using var doc = JsonDocument.Parse(jsonString, documentOptions);
        tree.DoTraverse(doc.RootElement, tree._rootNode);

        return tree;
    }

    public IJsonTreeNode? FindNode(string identity)
    {
        if (_rootNode.Identity.Equals(identity, StringComparison.OrdinalIgnoreCase))
        {
            return _rootNode;
        }

        _index ??= Flatten().ToDictionary(x => x.Identity, StringComparer.OrdinalIgnoreCase);

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

    public IJsonTreeNode[] Flatten() => _flattened ??= _rootNode.Flatten().ToArray();

    public override string ToString() => _rootNode.BuildBranchString();
}
