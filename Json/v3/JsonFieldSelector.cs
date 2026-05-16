using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Json.v3;

public sealed record JsonFieldSelector : IJsonFieldSelector
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly JsonTree _tree;

    public JsonFieldSelector(string jsonString)
    {
        _tree = JsonTree.Create(jsonString);
    }

    public string Select(string pathExpression)
    {
        var query = PathParser.Parse(pathExpression);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, WriterOptions);

        PathExpressionWriter.Write(writer, _tree.Root, query);

        writer.Flush();

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
