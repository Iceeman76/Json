using System.Text.Json;

namespace Json;

public interface IJsonTree
{
    IJsonTreeNode? FindNode(string jsonPath);
    IJsonTreeNode[] Flatten();
    string ToJsonString(JsonWriterOptions options = default);
}

public interface IJsonTreeNode
{
    string Name { get; }
    string PathSegment { get; }
    string JsonPath { get; }
    object? Value { get; set; }
    JsonValueKind Kind { get; set; }
    bool InArray { get; set; }
    IReadOnlyList<IJsonTreeNode> Children { get; }
    IJsonTreeNode[] Flatten();
    void BuildRecursiveJson(Utf8JsonWriter writer);
}
