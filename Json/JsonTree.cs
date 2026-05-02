using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Json;

public partial record JsonTree
{
    private readonly Node _rootNode;
    private Node[]? _flattened;

    private readonly JsonWriterOptions _defaultOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private JsonTree(Node rootNode)
    {
        _rootNode = rootNode;
    }

    public static JsonTree Create(string jsonString, string rootName = "$")
    {
        using var doc = JsonDocument.Parse(jsonString);
        var rootNode = new Node(rootName);
        var tree = new JsonTree(rootNode);

        tree.DoTraverse(doc.RootElement, tree._rootNode);

        return tree;
    }

    public Node? FindNode(string identity)
    {
        return Flatten().FirstOrDefault(x => x.Identity.Equals(identity, StringComparison.OrdinalIgnoreCase));
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
        var properties = element.EnumerateObject().ToArray();

        if (properties.Length == 0)
        {
            parent.Value = "{}";
        }
        else
        {
            foreach (var property in properties)
            {
                var node = parent.AddNode(property.Name, isArrayIndex: false);
                DoTraverse(property.Value, node);
            }
        }
    }

    private void TraverseArray(JsonElement element, Node parent)
    {
        var items = element.EnumerateArray().ToArray();

        if (items.Length == 0)
        {
            parent.Value = "[]";
        }
        else
        {
            for (var i = 0; i < items.Length; i++)
            {
                var node = parent.AddNode($"[{i}]", isArrayIndex: true);
                node.InArray = true;
                DoTraverse(items[i], node);
            }
        }
    }

    public string ToJsonString() => ToJsonString(_defaultOptions);

    public string ToJsonString(JsonWriterOptions options)
    {
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
