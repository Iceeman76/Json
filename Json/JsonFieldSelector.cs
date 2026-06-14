using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Json;

public sealed record JsonFieldSelector 
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly JsonTree _tree;

    public JsonFieldSelector(string jsonString)
    {
        _tree = JsonTree.Parse(jsonString);
    }

    public JsonFieldSelector(ReadOnlyMemory<byte> utf8Json)
    {
        _tree = JsonTree.Parse(utf8Json);
    }

    public JsonFieldSelector(Stream utf8Json)
    {
        _tree = JsonTree.Parse(utf8Json);
    }

    public string Select(string pathExpression)
    {
        var buffer = new ArrayBufferWriter<byte>();
        Select(pathExpression, buffer);

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public void Select(string pathExpression, IBufferWriter<byte> output)
    {
        using var writer = new Utf8JsonWriter(output, WriterOptions);

        var fastPathNode = TryFastPathLookup(pathExpression);

        if (fastPathNode is not null)
        {
            writer.WriteSubTree(fastPathNode);
        }
        else
        {
            var query = PathParser.Parse(pathExpression);
            PathExpressionWriter.Write(writer, _tree.Root, query);
        }

        writer.Flush();
    }

    private JsonTree.Node? TryFastPathLookup(string pathExpression)
    {
        return HasProjectionOrWildcard(pathExpression) ? null : _tree.FindNode(pathExpression);
    }

    private static bool HasProjectionOrWildcard(string pathExpression)
    {
        foreach (var c in pathExpression)
        {
            if (c is '[' or '*' or ',' or ':')
            {
                return true;
            }
        }

        return false;
    }
}
