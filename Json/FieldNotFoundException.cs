namespace Json;

public sealed class FieldNotFoundException(string field, JsonTree.Node parent) : Exception(BuildMessage(field, parent))
{
    public string Field { get; } = field;
    public string Path { get; } = parent.JsonPath;

    private static string BuildMessage(string field, JsonTree.Node parent)
    {
        var available = parent.Children
            .Where(child => child.InArray == false)
            .Select(child => child.Name)
            .ToArray();

        var availableText = available.Length > 0
            ? string.Join(", ", available)
            : "<none>";

        return $"Field '{field}' was not found at '{parent.JsonPath}'. Available fields: {availableText}.";
    }
}
