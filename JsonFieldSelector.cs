using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Json;

public sealed record JsonFieldSelector
{
    private readonly JsonTree _tree;

    private readonly JsonWriterOptions _options = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public JsonFieldSelector(string jsonString) : this(JsonTree.Create(jsonString))
    {
    }

    public JsonFieldSelector(JsonTree tree)
    {
        _tree = tree;
    }

    public string Select(string pathExpression)
    {
        if (pathExpression == "$")
            return _tree.ToJsonString();

        var projectionMatch = Regex.Match(pathExpression, @"\[([a-zA-Z_][a-zA-Z0-9_]*(?:,[a-zA-Z_][a-zA-Z0-9_]*)+)\]$");
        if (projectionMatch.Success)
            return SelectProjection(pathExpression, projectionMatch);

        if (pathExpression.Contains("[*]") || pathExpression.Contains(".*"))
            return SelectWildcard(pathExpression);

        var node = _tree.FindNode(pathExpression)!;

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, _options);

        writer.WriteSubTree(node);
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private string SelectWildcard(string pathExpression)
    {
        var pattern = "^" + Regex.Escape(pathExpression.Replace("[*]", "\x01").Replace("*", "\x02"))
            .Replace("\x01", @"\[\d+\]")
            .Replace("\x02", @"[^.\[\]]+") + "$";

        var matchedNodes = _tree.Flatten().Where(n => Regex.IsMatch(n.Identity, pattern));
        var wrapInArray = pathExpression.Contains("[*]");

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, _options);

        if (wrapInArray) writer.WriteStartArray();
        foreach (var node in matchedNodes)
            writer.WriteSubTree(node);
        if (wrapInArray) writer.WriteEndArray();
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private string SelectProjection(string pathExpression, Match projectionMatch)
    {
        var fields = projectionMatch.Groups[1].Value.Split(',');
        var basePath = pathExpression[..^projectionMatch.Value.Length];

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, _options);

        if (basePath.Contains("[*]"))
        {
            var basePattern = "^" + Regex.Escape(basePath.Replace("[*]", "\x01"))
                .Replace("\x01", @"\[\d+\]") + "$";

            writer.WriteStartArray();
            foreach (var node in _tree.Flatten().Where(n => Regex.IsMatch(n.Identity, basePattern)))
                WriteProjectionAsObject(writer, node.Identity, fields);
            writer.WriteEndArray();
        }
        else
        {
            WriteProjectionAsObject(writer, basePath, fields);
        }

        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteProjectionAsObject(Utf8JsonWriter writer, string basePath, string[] fields)
    {
        writer.WriteStartObject();
        foreach (var field in fields)
        {
            var node = _tree.FindNode($"{basePath}.{field}");
            if (node is not null)
                node.BuildRecursiveJson(writer);
        }
        writer.WriteEndObject();
    }
}
