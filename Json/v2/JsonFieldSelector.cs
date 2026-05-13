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

    private static bool TryParseProjection(string path, out string basePath, out string[] fields)
    {
        basePath = "";
        fields = [];

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

        if (inner.IndexOf(',') < 0)
        {
            return false;
        }

        var parts = inner.ToString().Split(',');

        foreach (var part in parts)
        {
            if (IsIdentifier(part) == false)
            {
                return false;
            }
        }

        basePath = path[..openBracket];
        fields = parts;

        return true;
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

    private static bool MatchesWildcardPattern(string identity, string pattern)
    {
        return MatchesWildcard(identity.AsSpan(), pattern.AsSpan(), StringComparison.OrdinalIgnoreCase);
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
        var matchedNodes = _tree.Flatten().Where(node => MatchesWildcardPattern(node.Identity, pathExpression));
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

    private string SelectProjection(string basePath, string[] fields)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, _options);

        if (basePath.Contains("[*]"))
        {
            var nodes = _tree.Flatten().Where(node => MatchesWildcardPattern(node.Identity, basePath));

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
}
