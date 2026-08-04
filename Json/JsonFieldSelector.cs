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
        pathExpression = TrimWhitespace(pathExpression);

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

    private static string TrimWhitespace(string pathExpression)
    {
        if (string.IsNullOrEmpty(pathExpression))
        {
            return pathExpression;
        }

        var hasWhitespace = false;

        foreach (var c in pathExpression)
        {
            if (char.IsWhiteSpace(c))
            {
                hasWhitespace = true;
                break;
            }
        }

        if (!hasWhitespace)
        {
            return pathExpression;
        }

        var builder = new StringBuilder(pathExpression.Length);

        foreach (var c in pathExpression)
        {
            if (!char.IsWhiteSpace(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private JsonTree.Node? TryFastPathLookup(string pathExpression)
    {
        return HasProjectionOrWildcard(pathExpression) ? null : _tree.FindNode(pathExpression);
    }

    private static bool HasProjectionOrWildcard(string pathExpression)
    {
        for (var i = 0; i < pathExpression.Length; i++)
        {
            var c = pathExpression[i];

            if (c is '[' or '*' or ',' or ':' or '|')
            {
                return true;
            }

            if (c == '.' && i + 1 < pathExpression.Length && pathExpression[i + 1] == '.')
            {
                return true;
            }
        }

        return false;
    }
}
