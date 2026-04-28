using System.Globalization;
using System.Text.Json;

namespace Json;

internal static class Utf8JsonWriterExtensions
{
    private static readonly Dictionary<JsonValueKind, Action<Utf8JsonWriter, JsonTree.Node>> MethodRouter = new()
    {
        { JsonValueKind.Null, WriteNullNode },
        { JsonValueKind.True, WriteBooleanNode },
        { JsonValueKind.String, WriteStringNode },
        { JsonValueKind.Number, WriteNumberNode },
        { JsonValueKind.False, WriteBooleanNode },
        { JsonValueKind.Array, WriteStartArrayNode },
        { JsonValueKind.Object, WriteStartObjectNode }
    };

    public static void WriteNode(this Utf8JsonWriter writer, JsonTree.Node node)
    {
        MethodRouter[node.Kind].Invoke(writer, node);
    }

    private static void WriteNumberNode(Utf8JsonWriter writer, JsonTree.Node node)
    {
        if (long.TryParse(node.Value, NumberFormatInfo.InvariantInfo, out var longValue))
        {
            WriteLongNode(writer, node, longValue);
        }
        else if (decimal.TryParse(node.Value, NumberFormatInfo.InvariantInfo, out var decimalValue))
        {
            WriteDecimalNode(writer, node, decimalValue);
        }
        else if (double.TryParse(node.Value, NumberFormatInfo.InvariantInfo, out var doubleValue))
        {
            WriteDoubleNode(writer, node, doubleValue);
        }
        else
        {
            throw new Exception($"Could not fit the value {node.Value} in any supported number type");
        }
    }

    private static void WriteLongNode(Utf8JsonWriter writer, JsonTree.Node node, long value)
    {
        if (node.InArray)
        {
            writer.WriteNumberValue(value);
        }
        else
        {
            writer.WriteNumber(node.Name.Wash(), value);
        }
    }

    private static void WriteDoubleNode(Utf8JsonWriter writer, JsonTree.Node node, double value)
    {
        if (node.InArray)
        {
            writer.WriteNumberValue(value);
        }
        else
        {
            writer.WriteNumber(node.Name.Wash(), value);
        }
    }

    private static void WriteDecimalNode(Utf8JsonWriter writer, JsonTree.Node node, decimal value)
    {
        if (node.InArray)
        {
            writer.WriteNumberValue(value);
        }
        else
        {
            writer.WriteNumber(node.Name.Wash(), value);
        }
    }

    private static void WriteStringNode(Utf8JsonWriter writer, JsonTree.Node node)
    {
        if (node.InArray)
        {
            writer.WriteStringValue(node.Value);
        }
        else
        {
            writer.WriteString(node.Name.Wash(), node.Value);
        }
    }

    private static void WriteNullNode(Utf8JsonWriter writer, JsonTree.Node node)
    {
        if (node.InArray)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNull(node.Name.Wash());
        }
    }

    private static void WriteBooleanNode(Utf8JsonWriter writer, JsonTree.Node node)
    {
        var value = Convert.ToBoolean(node.Value);

        if (node.InArray)
        {
            writer.WriteBooleanValue(value);
        }
        else
        {
            writer.WriteBoolean(node.Name.Wash(), value);
        }
    }

    private static void WriteStartObjectNode(Utf8JsonWriter writer, JsonTree.Node node)
    {
        if (node.InArray)
        {
            writer.WriteStartObject();
        }
        else
        {
            writer.WriteStartObject(node.Name.Wash());
        }
    }

    private static void WriteStartArrayNode(Utf8JsonWriter writer, JsonTree.Node node)
    {
        if (node.InArray)
        {
            writer.WriteStartArray();
        }
        else
        {
            writer.WriteStartArray(node.Name.Wash());
        }
    }

    public static void WriteStartNode(this Utf8JsonWriter writer, JsonTree.Node node)
    {
        if (node.Kind is JsonValueKind.Array)
        {
            writer.WriteStartArray();
        }
        else if (node.Kind is JsonValueKind.Object)
        {
            writer.WriteStartObject();
        }
    }

    public static void WriteEndNode(this Utf8JsonWriter writer, JsonTree.Node node)
    {
        if (node.Kind is JsonValueKind.Array)
        {
            writer.WriteEndArray();
        }
        else if (node.Kind is JsonValueKind.Object)
        {
            writer.WriteEndObject();
        }
    }
}

file static class Extensions
{
    public static string Wash(this string name) => name.Replace("'", "").Replace("[", "").Replace("]", "");
}