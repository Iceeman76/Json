using System.Text.Json;

namespace Json;

public static class JsonElementExtensions
{
    public static object? GetValue(this JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var value) => value,
            JsonValueKind.Number when element.TryGetDecimal(out var value) => value,
            JsonValueKind.Number => element.GetDouble(),
            _ => throw new JsonException($"Unsupported JSON value kind: {element.ValueKind}")
        };
    }
}
