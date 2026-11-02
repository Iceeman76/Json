using System.Text.Json;

namespace DotNetLab.Json;

internal sealed class JsonNodeValueComparer : IEqualityComparer<JsonTree.Node>
{
    public static JsonNodeValueComparer Instance { get; } = new();

    public bool Equals(JsonTree.Node? firstNode, JsonTree.Node? secondNode)
    {
        if (ReferenceEquals(firstNode, secondNode))
        {
            return true;
        }

        if (firstNode!.Kind != secondNode!.Kind)
        {
            return false;
        }

        return firstNode.Kind switch
        {
            JsonValueKind.Array => firstNode.Children.SequenceEqual(secondNode.Children, this),
            JsonValueKind.Object => ObjectsEqual(firstNode.Children, secondNode.Children),
            _ => ScalarsEqual(firstNode, secondNode)
        };
    }

    public int GetHashCode(JsonTree.Node node)
    {
        var hash = new HashCode();
        hash.Add(node.Kind);

        switch (node.Kind)
        {
            case JsonValueKind.Object:
                hash.Add(GetObjectHashCode(node));
                break;
            case JsonValueKind.Array:
                AddArrayToHash(ref hash, node);
                break;
            case JsonValueKind.Undefined:
            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            default:
                hash.Add(GetScalarHashCode(node));
                break;
        }

        return hash.ToHashCode();
    }

    private static bool ObjectsEqual(IReadOnlyList<JsonTree.Node> firstProperties, IReadOnlyList<JsonTree.Node> secondProperties)
    {
        if (firstProperties.Count != secondProperties.Count)
        {
            return false;
        }

        if (firstProperties.SequenceEqual(secondProperties, PropertyValueComparer.Shared))
        {
            return true;
        }

        var remainingProperties = new Dictionary<JsonTree.Node, int>(secondProperties.Count, PropertyValueComparer.Shared);

        foreach (var property in secondProperties)
        {
            remainingProperties.TryGetValue(property, out var count);
            remainingProperties[property] = count + 1;
        }

        foreach (var firstProperty in firstProperties)
        {
            if (remainingProperties.Remove(firstProperty, out var count) is false)
            {
                return false;
            }

            if (count > 1)
            {
                remainingProperties.Add(firstProperty, count - 1);
            }
        }

        return true;
    }

    private sealed class PropertyValueComparer : IEqualityComparer<JsonTree.Node>
    {
        public static PropertyValueComparer Shared { get; } = new();

        public bool Equals(JsonTree.Node? firstProperty, JsonTree.Node? secondProperty) => string.Equals(firstProperty!.Name, secondProperty!.Name, StringComparison.Ordinal) && Instance.Equals(firstProperty, secondProperty);

        public int GetHashCode(JsonTree.Node property) => HashCode.Combine(StringComparer.Ordinal.GetHashCode(property.Name), Instance.GetHashCode(property));
    }

    private int GetObjectHashCode(JsonTree.Node node)
    {
        var propertiesHash = 0;

        foreach (var property in node.Children)
        {
            propertiesHash ^= HashCode.Combine(StringComparer.Ordinal.GetHashCode(property.Name), GetHashCode(property));
        }

        return propertiesHash;
    }

    private void AddArrayToHash(ref HashCode hash, JsonTree.Node node)
    {
        foreach (var element in node.Children)
        {
            hash.Add(GetHashCode(element));
        }
    }

    private static bool ScalarsEqual(JsonTree.Node firstNode, JsonTree.Node secondNode)
    {
        if (firstNode.Kind != JsonValueKind.Number)
        {
            return Equals(firstNode.Value, secondNode.Value);
        }

        return JsonNumber.Normalize(firstNode.Value!) == JsonNumber.Normalize(secondNode.Value!);
    }

    private static int GetScalarHashCode(JsonTree.Node node)
    {
        if (node.Kind != JsonValueKind.Number)
        {
            return node.Value?.GetHashCode() ?? 0;
        }

        return JsonNumber.Normalize(node.Value!).GetHashCode();
    }
}
