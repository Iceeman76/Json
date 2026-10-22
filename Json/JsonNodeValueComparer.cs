using System.Text.Json;

namespace Json;

internal sealed class JsonNodeValueComparer : IEqualityComparer<JsonTree.Node>
{
    public static JsonNodeValueComparer Instance { get; } = new();

    public bool Equals(JsonTree.Node? firstNode, JsonTree.Node? secondNode)
    {
        if (firstNode!.Kind != secondNode!.Kind)
        {
            return false;
        }

        if (firstNode.Kind == JsonValueKind.Array)
        {
            return firstNode.Children.SequenceEqual(secondNode.Children, this);
        }

        if (firstNode.Kind == JsonValueKind.Object)
        {
            return ObjectsEqual(firstNode.Children, secondNode.Children);
        }

        return ScalarsEqual(firstNode, secondNode);
    }

    public int GetHashCode(JsonTree.Node node)
    {
        var hash = new HashCode();
        hash.Add(node.Kind);

        if (node.Kind == JsonValueKind.Object)
        {
            hash.Add(GetObjectHashCode(node));
        }
        else if (node.Kind == JsonValueKind.Array)
        {
            AddArrayToHash(ref hash, node);
        }
        else
        {
            hash.Add(GetScalarHashCode(node));
        }

        return hash.ToHashCode();
    }

    private bool ObjectsEqual(IReadOnlyList<JsonTree.Node> firstProperties, IReadOnlyList<JsonTree.Node> secondProperties)
    {
        if (firstProperties.Count != secondProperties.Count)
        {
            return false;
        }

        var matchedProperties = new bool[secondProperties.Count];

        foreach (var firstProperty in firstProperties)
        {
            var matchingIndex = FindMatchingPropertyIndex(firstProperty, secondProperties, matchedProperties);

            if (matchingIndex < 0)
            {
                return false;
            }

            matchedProperties[matchingIndex] = true;
        }

        return true;
    }

    private int FindMatchingPropertyIndex(JsonTree.Node property, IReadOnlyList<JsonTree.Node> candidates, bool[] matchedProperties)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            if (matchedProperties[index])
            {
                continue;
            }

            var candidate = candidates[index];

            if (string.Equals(property.Name, candidate.Name, StringComparison.Ordinal) && Equals(property, candidate))
            {
                return index;
            }
        }

        return -1;
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
