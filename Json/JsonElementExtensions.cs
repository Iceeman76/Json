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
            JsonValueKind.Number => GetNumber(element),
            _ => throw new JsonException($"Unsupported JSON value kind: {element.ValueKind}")
        };
    }

    private static object GetNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var integer))
        {
            return integer;
        }

        var exactNumber = new JsonNumber(element);
        var normalizedNumber = JsonNumber.Normalize(exactNumber);

        if (element.TryGetDecimal(out var decimalNumber) && JsonNumber.Normalize(decimalNumber) == normalizedNumber)
        {
            return decimalNumber;
        }

        var doubleNumber = element.GetDouble();

        if (double.IsFinite(doubleNumber) && JsonNumber.Normalize(doubleNumber) == normalizedNumber)
        {
            return doubleNumber;
        }

        return exactNumber;
    }
}
