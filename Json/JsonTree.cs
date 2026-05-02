using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Json;

public sealed partial class JsonTree
{
    private readonly Node _rootNode;
    private Node[]? _flattened;

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
        return Flatten().FirstOrDefault(x => x.Identity.Equals(identity, comparison));
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
        var hasProperties = false;

        foreach (var property in element.EnumerateObject())
        {
            hasProperties = true;
            var node = parent.AddNode(property.Name, isArrayIndex: false);
            DoTraverse(property.Value, node);
        }

        if (hasProperties == false)
        {
            parent.Value = "{}";
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

        if (index == 0)
        {
            parent.Value = "[]";
        }
    }

    public string ToJsonString(JsonWriterOptions options = default)
    {
        if (options.Encoder is null)
        {
            options = options with { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        }

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, options);

        writer.WriteStartNode(_rootNode);

        foreach (var node in _rootNode.Children)
        {
            node.BuildRecursiveJson(writer);
        }

        writer.WriteEndNode(_rootNode);
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public Node[] Flatten() => _flattened ??= _rootNode.Flatten().ToArray();

    public override string ToString() => _rootNode.BuildBranchString();
}
