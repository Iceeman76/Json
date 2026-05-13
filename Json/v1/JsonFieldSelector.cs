using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Json.v1;

public sealed partial record JsonFieldSelector : IJsonFieldSelector
{
    private readonly JsonTree _tree;

    private readonly JsonWriterOptions _options = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public JsonFieldSelector(string jsonString)
    {
        _tree = JsonTree.Create(jsonString);
    }

    public string Select(string pathExpression)
    {
        if (pathExpression == "$")
        {
            return _tree.ToJsonString();
        }

        var projectionMatch = ProjectionRegex().Match(pathExpression);

        if (projectionMatch.Success)
        {
            return SelectProjection(pathExpression, projectionMatch);
        }

        if (pathExpression.Contains("[*]") || pathExpression.Contains(".*"))
        {
            return SelectWildcard(pathExpression);
        }

        var node = _tree.FindNode(pathExpression)!;

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, _options);

        writer.WriteSubTree(node);
        writer.Flush();

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private string SelectWildcard(string pathExpression)
    {
        var expression = pathExpression.Replace("[*]", "\x01").Replace("*", "\x02");
        var escaped = Regex.Escape(expression).Replace("\x01", @"\[\d+\]").Replace("\x02", @"[^.\[\]]+");
        var pattern = $"^{escaped}$";

        var matchedNodes = _tree.Flatten().Where(node => Regex.IsMatch(node.Identity, pattern, RegexOptions.IgnoreCase));
        var wrapInArray = pathExpression.Contains("[*]");

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, _options);

        if (wrapInArray) writer.WriteStartArray();

        foreach (var node in matchedNodes)
        {
            writer.WriteSubTree(node);
        }

        if (wrapInArray) writer.WriteEndArray();

        writer.Flush();

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private string SelectProjection(string pathExpression, Match projectionMatch)
    {
        var fields = projectionMatch.Groups[1].Value.Split(',');
        var basePath = pathExpression[..^projectionMatch.Value.Length];

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, _options);

        if (basePath.Contains("[*]"))
        {
            var basePattern = "^" + Regex.Escape(basePath.Replace("[*]", "\x01")).Replace("\x01", @"\[\d+\]") + "$";
            var nodes = _tree.Flatten().Where(node => Regex.IsMatch(node.Identity, basePattern));

            writer.WriteStartArray();

            foreach (var node in nodes)
            {
                WriteProjectionAsObject(writer, node.Identity, fields);
            }

            writer.WriteEndArray();
        }
        else
        {
            WriteProjectionAsObject(writer, basePath, fields);
        }

        writer.Flush();

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private void WriteProjectionAsObject(Utf8JsonWriter writer, string basePath, string[] fields)
    {
        writer.WriteStartObject();

        foreach (var field in fields)
        {
            var identity = $"{basePath}.{field}";
            var node = _tree.FindNode(identity);

            node?.BuildRecursiveJson(writer);
        }

        writer.WriteEndObject();
    }

    [GeneratedRegex(@"\[([a-zA-Z_][a-zA-Z0-9_]*(?:,[a-zA-Z_][a-zA-Z0-9_]*)+)\]$")]
    private static partial Regex ProjectionRegex();
}
