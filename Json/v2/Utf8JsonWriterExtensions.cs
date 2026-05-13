using System.Text.Json;

namespace Json.v2;

internal static class Utf8JsonWriterExtensions
{
    public static void WriteNode(this Utf8JsonWriter writer, IJsonTreeNode node, bool inArray)
    {
        switch (node.Kind)
        {
            case JsonValueKind.Null:
                WriteNullNode(writer, node, inArray);
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                WriteBooleanNode(writer, node, inArray);
                break;
            case JsonValueKind.String:
                WriteStringNode(writer, node, inArray);
                break;
            case JsonValueKind.Number:
                WriteNumberNode(writer, node, inArray);
                break;
            case JsonValueKind.Array:
                WriteStartArrayNode(writer, node, inArray);
                break;
            case JsonValueKind.Object:
                WriteStartObjectNode(writer, node, inArray);
                break;
            case JsonValueKind.Undefined:
            default:
                throw new InvalidOperationException($"Node '{node.Identity}' has Kind = {node.Kind} and cannot be written.");
        }
    }

    private static void WriteNumberNode(Utf8JsonWriter writer, IJsonTreeNode node, bool inArray)
    {
        var element = node.Element!.Value;

        if (element.TryGetInt64(out var longValue))
        {
            WriteLongNode(writer, node, inArray, longValue);
        }
        else if (element.TryGetDecimal(out var decimalValue))
        {
            WriteDecimalNode(writer, node, inArray, decimalValue);
        }
        else if (element.TryGetDouble(out var doubleValue))
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
        var value = node.Element?.GetString() ?? node.Value;

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
        var value = node.Element?.GetBoolean() ?? Convert.ToBoolean(node.Value);

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
        if (node.Kind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            return;
        }

        if (node.Kind is JsonValueKind.Array)
        {
            writer.WriteStartArray();
            return;
        }

        writer.WriteStartObject();
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
        if (node.Kind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            return;
        }

        if (node.Kind is JsonValueKind.Array)
        {
            writer.WriteEndArray();
            return;
        }

        writer.WriteEndObject();
    }
}
