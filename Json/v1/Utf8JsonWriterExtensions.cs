using System.Globalization;
using System.Text.Json;

namespace Json.v1;

internal static class Utf8JsonWriterExtensions
{
    private static readonly Dictionary<JsonValueKind, Action<Utf8JsonWriter, IJsonTreeNode, bool>> MethodRouter = new()
    {
        { JsonValueKind.Null, WriteNullNode },
        { JsonValueKind.True, WriteBooleanNode },
        { JsonValueKind.String, WriteStringNode },
        { JsonValueKind.Number, WriteNumberNode },
        { JsonValueKind.False, WriteBooleanNode },
        { JsonValueKind.Array, WriteStartArrayNode },
        { JsonValueKind.Object, WriteStartObjectNode }
    };

    public static void WriteNode(this Utf8JsonWriter writer, IJsonTreeNode node, bool inArray)
    {
        if (node.Kind is JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"Node '{node.Identity}' has Kind = Undefined and cannot be written.");
        }

        MethodRouter[node.Kind].Invoke(writer, node, inArray);
    }

    private static void WriteNumberNode(Utf8JsonWriter writer, IJsonTreeNode node, bool inArray)
    {
        if (long.TryParse(node.Value as string, NumberFormatInfo.InvariantInfo, out var longValue))
        {
            WriteLongNode(writer, node, inArray, longValue);
        }
        else if (decimal.TryParse(node.Value as string, NumberFormatInfo.InvariantInfo, out var decimalValue))
        {
            WriteDecimalNode(writer, node, inArray, decimalValue);
        }
        else if (double.TryParse(node.Value as string, NumberFormatInfo.InvariantInfo, out var doubleValue))
        {
            WriteDoubleNode(writer, node, inArray, doubleValue);
        }
        else
        {
            throw new Exception($"Could not fit the value {node.Value} in any supported number type");
        }
    }

    private static void WriteLongNode(Utf8JsonWriter writer, IJsonTreeNode node, bool inArray, long value)
    {
        if (inArray)
        {
            writer.WriteNumberValue(value);
        }
        else
        {
            writer.WriteNumber(node.Name, value);
        }
    }

    private static void WriteDoubleNode(Utf8JsonWriter writer, IJsonTreeNode node, bool inArray, double value)
    {
        if (inArray)
        {
            writer.WriteNumberValue(value);
        }
        else
        {
            writer.WriteNumber(node.Name, value);
        }
    }

    private static void WriteDecimalNode(Utf8JsonWriter writer, IJsonTreeNode node, bool inArray, decimal value)
    {
        if (inArray)
        {
            writer.WriteNumberValue(value);
        }
        else
        {
            writer.WriteNumber(node.Name, value);
        }
    }

    private static void WriteStringNode(Utf8JsonWriter writer, IJsonTreeNode node, bool inArray)
    {
        var value = node.Value as string;

        if (inArray)
        {
            writer.WriteStringValue(value);
        }
        else
        {
            writer.WriteString(node.Name, value);
        }
    }

    private static void WriteNullNode(Utf8JsonWriter writer, IJsonTreeNode node, bool inArray)
    {
        if (inArray)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNull(node.Name);
        }
    }

    private static void WriteBooleanNode(Utf8JsonWriter writer, IJsonTreeNode node, bool inArray)
    {
        var value = Convert.ToBoolean(node.Value);

        if (inArray)
        {
            writer.WriteBooleanValue(value);
        }
        else
        {
            writer.WriteBoolean(node.Name, value);
        }
    }

    private static void WriteStartObjectNode(Utf8JsonWriter writer, IJsonTreeNode node, bool inArray)
    {
        if (inArray)
        {
            writer.WriteStartObject();
        }
        else
        {
            writer.WriteStartObject(node.Name);
        }
    }

    private static void WriteStartArrayNode(Utf8JsonWriter writer, IJsonTreeNode node, bool inArray)
    {
        if (inArray)
        {
            writer.WriteStartArray();
        }
        else
        {
            writer.WriteStartArray(node.Name);
        }
    }

    public static void WriteStartNode(this Utf8JsonWriter writer, IJsonTreeNode node)
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

    public static void WriteSubTree(this Utf8JsonWriter writer, IJsonTreeNode node)
    {
        if (node.Kind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            writer.WriteNode(node, inArray: true);
            return;
        }

        writer.WriteStartNode(node);

        foreach (var child in node.Children)
        {
            child.BuildRecursiveJson(writer);
        }

        writer.WriteEndNode(node);
    }

    public static void WriteEndNode(this Utf8JsonWriter writer, IJsonTreeNode node)
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
