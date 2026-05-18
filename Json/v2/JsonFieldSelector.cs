using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Json.v2;

public sealed record JsonFieldSelector : IJsonFieldSelector
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

        if (TryParseProjection(pathExpression, out var basePath, out var fields))
        {
            return SelectProjection(basePath, fields);
        }

        if (TryParseBracketAccess(pathExpression, out var singleBasePath, out var singleField))
        {
            return SelectProjection(singleBasePath, [singleField]);
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

    private static bool TryParseBracketAccess(string path, out string basePath, out string field)
    {
        basePath = "";
        field = "";

        if (path.Length < 4 || path[^1] != ']')
        {
            return false;
        }

        var openBracket = path.LastIndexOf('[');

        if (openBracket < 1)
        {
            return false;
        }

        var inner = path.AsSpan((openBracket + 1)..^1);

        if (inner.IndexOf(',') >= 0 || !IsIdentifier(inner.ToString()))
        {
            return false;
        }

        basePath = path[..openBracket];
        field = inner.ToString();

        return true;
    }

    private static bool TryParseProjection(string path, out string basePath, out string[] fields)
    {
        basePath = "";
        fields = [];

        if (path.Length < 4 || path[^1] != ']')
        {
            return false;
        }

        // Find the first '[' after the base path (skip past dots and identifiers)
        var openBracket = FindFirstProjectionBracket(path);

        if (openBracket < 1)
        {
            return false;
        }

        // Find the matching closing bracket
        var closeBracket = FindMatchingBracket(path, openBracket);

        if (closeBracket != path.Length - 1)
        {
            return false;
        }

        var inner = path[(openBracket + 1)..closeBracket];

        // Split on top-level commas (not inside nested brackets)
        var parts = SplitTopLevel(inner);

        if (parts.Length == 0)
        {
            return false;
        }

        foreach (var part in parts)
        {
            var fieldName = part.Contains('[') ? part[..part.IndexOf('[')] : part;

            if (IsIdentifier(fieldName) == false)
            {
                return false;
            }
        }

        basePath = path[..openBracket];
        fields = parts;

        return true;
    }

    private static int FindFirstProjectionBracket(string path)
    {
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] == '[')
            {
                var close = FindMatchingBracket(path, i);

                if (close < 0)
                {
                    return -1;
                }

                if (close == path.Length - 1)
                {
                    return i;
                }

                i = close;
            }
        }

        return -1;
    }

    private static int FindMatchingBracket(string path, int openIndex)
    {
        var depth = 0;

        for (var i = openIndex; i < path.Length; i++)
        {
            if (path[i] == '[')
            {
                depth++;
            }
            else if (path[i] == ']')
            {
                depth--;

                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string[] SplitTopLevel(string inner)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '[')
            {
                depth++;
            }
            else if (inner[i] == ']')
            {
                depth--;
            }
            else if (inner[i] == ',' && depth == 0)
            {
                parts.Add(inner[start..i]);
                start = i + 1;
            }
        }

        parts.Add(inner[start..]);

        return parts.ToArray();
    }

    private static bool IsIdentifier(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        var first = name[0];

        if (!(char.IsAsciiLetter(first) || first == '_'))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];

            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesWildcardPattern(string jsonPath, string pattern)
    {
        return MatchesWildcard(jsonPath.AsSpan(), pattern.AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesWildcard(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, StringComparison comparison)
    {
        while (pattern.IsEmpty == false)
        {
            if (pattern.StartsWith("[*]"))
            {
                pattern = pattern[3..];

                if (!text.StartsWith("[", comparison))
                {
                    return false;
                }

                var close = text.IndexOf(']');

                if (close < 2)
                {
                    return false;
                }

                var digits = text[1..close];

                foreach (var c in digits)
                {
                    if (!char.IsAsciiDigit(c))
                    {
                        return false;
                    }
                }

                text = text[(close + 1)..];
            }
            else if (pattern[0] == '*')
            {
                pattern = pattern[1..];

                var end = FindSegmentEnd(text);

                if (end == 0)
                {
                    return false;
                }

                text = text[end..];
            }
            else
            {
                if (text.IsEmpty || !text[..1].Equals(pattern[..1], comparison))
                {
                    return false;
                }

                text = text[1..];
                pattern = pattern[1..];
            }
        }

        return text.IsEmpty;
    }

    private static int FindSegmentEnd(ReadOnlySpan<char> text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (c is '.' or '[' or ']')
            {
                return i;
            }
        }

        return text.Length;
    }

    private string SelectWildcard(string pathExpression)
    {
        var matchedNodes = _tree.Flatten().Where(node => MatchesWildcardPattern(node.JsonPath, pathExpression)).ToList();
        var wrapInArray = pathExpression.Contains("[*]") || matchedNodes.Count > 1;

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

    private string SelectProjection(string basePath, string[] fields)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, _options);

        if (basePath.Contains("[*]"))
        {
            var nodes = _tree.Flatten().Where(node => MatchesWildcardPattern(node.JsonPath, basePath));

            writer.WriteStartArray();

            foreach (var node in nodes)
            {
                WriteProjectionAsObject(writer, node.JsonPath, fields);
            }

            writer.WriteEndArray();
        }
        else
        {
            var baseNode = _tree.FindNode(basePath);

            if (baseNode is { Kind: JsonValueKind.Array })
            {
                var arrayNodes = _tree.Flatten()
                    .Where(n => MatchesWildcardPattern(n.JsonPath, $"{basePath}[*]"));

                writer.WriteStartArray();

                foreach (var arrayNode in arrayNodes)
                {
                    WriteProjectionAsObject(writer, arrayNode.JsonPath, fields);
                }

                writer.WriteEndArray();
            }
            else
            {
                WriteProjectionAsObject(writer, basePath, fields);
            }
        }

        writer.Flush();

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private void WriteProjectionAsObject(Utf8JsonWriter writer, string basePath, string[] fields)
    {
        writer.WriteStartObject();

        foreach (var field in fields)
        {
            WriteFieldProjection(writer, basePath, field);
        }

        writer.WriteEndObject();
    }

    private void WriteFieldProjection(Utf8JsonWriter writer, string basePath, string field)
    {
        var bracketIndex = field.IndexOf('[');

        if (bracketIndex < 0)
        {
            var jsonPath = $"{basePath}.{field}";
            var node = _tree.FindNode(jsonPath);
            node?.BuildRecursiveJson(writer);
            return;
        }

        var fieldName = field[..bracketIndex];
        var subSelector = field[bracketIndex..];
        var baseJsonPath = $"{basePath}.{fieldName}";
        var baseNode = _tree.FindNode(baseJsonPath);

        if (baseNode == null)
        {
            return;
        }

        if (subSelector == "[*]")
        {
            baseNode.BuildRecursiveJson(writer);
            return;
        }

        if (subSelector.StartsWith("[*]") && subSelector.Length > 3)
        {
            var remainingSelector = subSelector[3..];
            var innerFields = SplitTopLevel(remainingSelector[1..^1]);

            WriteArrayProjection(writer, fieldName, baseJsonPath, innerFields);
            return;
        }

        var subFields = SplitTopLevel(subSelector[1..^1]);

        if (baseNode.Kind == JsonValueKind.Array)
        {
            WriteArrayProjection(writer, fieldName, baseJsonPath, subFields);
            return;
        }

        writer.WritePropertyName(fieldName);
        WriteProjectionAsObject(writer, baseJsonPath, subFields);
    }

    private void WriteArrayProjection(Utf8JsonWriter writer, string fieldName, string baseJsonPath, string[] innerFields)
    {
        var arrayNodes = _tree.Flatten()
            .Where(n => MatchesWildcardPattern(n.JsonPath, $"{baseJsonPath}[*]"));

        writer.WritePropertyName(fieldName);
        writer.WriteStartArray();

        foreach (var arrayNode in arrayNodes)
        {
            WriteProjectionAsObject(writer, arrayNode.JsonPath, innerFields);
        }

        writer.WriteEndArray();
    }
}
