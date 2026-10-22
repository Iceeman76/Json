using System.Globalization;
using System.Text;

namespace Json;

internal static class PathParser
{
    public static PathQuery Parse(string expression)
    {
        expression = NormalizeWhitespace(expression);
        var cursor = new Cursor(expression);
        cursor.Expect('$');

        var steps = new List<PathStep>();
        var hasArrayWildcard = false;
        string? recursiveAlias = null;

        while (cursor.HasMore && cursor.Peek() != '|' && !IsTerminalProjection(cursor))
        {
            var step = ParseStep(cursor);
            steps.Add(step);

            if (step.Kind is StepKind.AllChildren
                or StepKind.AllElements
                or StepKind.RecursiveName
                or StepKind.RecursiveAll
                or StepKind.RecursiveAllElements
                or StepKind.RecursiveElement)
            {
                hasArrayWildcard = true;
            }

            if (step.Alias is not null)
            {
                recursiveAlias = step.Alias;
            }
        }

        var terminal = cursor.HasMore && cursor.Peek() == '[' ? ParseProjection(cursor) : null;
        var distinct = cursor.TryRead("|distinct");

        if (cursor.HasMore)
        {
            throw new FormatException($"Unexpected character at position {cursor.Position} in '{expression}'.");
        }

        return new PathQuery(steps, hasArrayWildcard, terminal, recursiveAlias, distinct);
    }

    private static string NormalizeWhitespace(string pathExpression)
    {
        if (string.IsNullOrEmpty(pathExpression))
        {
            return pathExpression;
        }

        var hasWhitespace = false;

        foreach (var c in pathExpression)
        {
            if (char.IsWhiteSpace(c))
            {
                hasWhitespace = true;
                break;
            }
        }

        if (!hasWhitespace)
        {
            return pathExpression;
        }

        var builder = new StringBuilder(pathExpression.Length);
        var quote = '\0';
        var escaped = false;

        foreach (var c in pathExpression)
        {
            if (quote is not '\0')
            {
                builder.Append(c);

                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c is '\'' or '"')
            {
                quote = c;
                builder.Append(c);
            }
            else if (char.IsWhiteSpace(c) is false)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static bool IsTerminalProjection(Cursor cursor)
    {
        return cursor.Peek() == '[' && cursor.LooksLikeWildcardBracket() is false && cursor.LooksLikeIndexBracket() is false && cursor.LooksLikeQuotedBracket() is false;
    }

    private static PathStep ParseStep(Cursor cursor)
    {
        if (cursor.Peek() == '.')
        {
            cursor.Advance();

            if (cursor.HasMore && cursor.Peek() == '.')
            {
                cursor.Advance();

                if (cursor.HasMore && cursor.Peek() == '*')
                {
                    cursor.Advance();
                    return new PathStep(StepKind.RecursiveAll, null);
                }

                if (cursor.HasMore && cursor.Peek() == '[')
                {
                    if (cursor.LooksLikeQuotedBracket())
                    {
                        return new PathStep(StepKind.RecursiveName, cursor.ReadQuotedBracket());
                    }

                    if (cursor.LooksLikeWildcardBracket())
                    {
                        cursor.Skip("[*]");
                        return new PathStep(StepKind.RecursiveAllElements, null);
                    }

                    if (cursor.LooksLikeAliasBracket())
                    {
                        var aliasedName = cursor.ReadAliasBracket(out var bracketAlias);
                        return new PathStep(StepKind.RecursiveName, aliasedName, bracketAlias);
                    }

                    return new PathStep(StepKind.RecursiveElement, cursor.ReadIndex());
                }

                return new PathStep(StepKind.RecursiveName, cursor.ReadIdentifier());
            }

            if (cursor.HasMore && cursor.Peek() == '*')
            {
                cursor.Advance();
                return new PathStep(StepKind.AllChildren, null);
            }

            return new PathStep(StepKind.Name, cursor.ReadIdentifier());
        }

        if (cursor.LooksLikeWildcardBracket())
        {
            cursor.Skip("[*]");

            return new PathStep(StepKind.AllElements, null);
        }

        if (cursor.LooksLikeQuotedBracket())
        {
            return new PathStep(StepKind.Name, cursor.ReadQuotedBracket());
        }

        return new PathStep(StepKind.Element, cursor.ReadIndex());
    }

    private static Projection ParseProjection(Cursor cursor)
    {
        cursor.Expect('[');

        var fields = new List<FieldExpr> { ParseField(cursor) };

        while (cursor.HasMore && cursor.Peek() == ',')
        {
            cursor.Advance();
            fields.Add(ParseField(cursor));
        }

        cursor.Expect(']');

        return new Projection(fields);
    }

    private static FieldExpr ParseField(Cursor cursor)
    {
        var firstIdentifier = cursor.ReadName();

        string? alias = null;

        if (cursor.HasMore && cursor.Peek() == ':')
        {
            alias = firstIdentifier;
            cursor.Advance();
            firstIdentifier = cursor.ReadName();
        }

        var path = new List<PathStep> { new(StepKind.Name, firstIdentifier) };

        while (cursor.HasMore)
        {
            if (cursor.Peek() == '.')
            {
                cursor.Advance();
                path.Add(new PathStep(StepKind.Name, cursor.ReadIdentifier()));
            }
            else if (cursor.LooksLikeIndexBracket())
            {
                path.Add(new PathStep(StepKind.Element, cursor.ReadIndex()));
            }
            else if (cursor.LooksLikeQuotedBracket())
            {
                path.Add(new PathStep(StepKind.Name, cursor.ReadQuotedBracket()));
            }
            else
            {
                break;
            }
        }

        var explicitArray = false;

        if (cursor.LooksLikeWildcardBracket())
        {
            cursor.Skip("[*]");
            explicitArray = true;
        }

        Projection? sub = null;

        if (cursor.HasMore && cursor.Peek() == '[')
        {
            sub = ParseProjection(cursor);
        }

        return new FieldExpr(path, alias, explicitArray, sub);
    }

    private sealed class Cursor(string text)
    {
        private int _position;

        public bool HasMore => _position < text.Length;

        public int Position => _position;

        public char Peek() => text[_position];

        public void Advance() => _position++;

        public void Expect(char c)
        {
            if (!HasMore || text[_position] != c)
            {
                throw new FormatException($"Expected '{c}' at position {_position} in '{text}'.");
            }

            _position++;
        }

        public void Skip(string token)
        {
            foreach (var c in token)
            {
                Expect(c);
            }
        }

        public bool TryRead(string token)
        {
            if (!text.AsSpan(_position).StartsWith(token, StringComparison.Ordinal))
            {
                return false;
            }

            _position += token.Length;
            return true;
        }

        public bool LooksLikeWildcardBracket()
        {
            return _position + 2 < text.Length
                && text[_position] == '['
                && text[_position + 1] == '*'
                && text[_position + 2] == ']';
        }

        public bool LooksLikeIndexBracket()
        {
            if (_position + 1 >= text.Length || text[_position] != '[')
            {
                return false;
            }

            var next = text[_position + 1];

            return char.IsAsciiDigit(next) || next == '-';
        }

        public bool LooksLikeQuotedBracket()
        {
            if (_position + 1 >= text.Length || text[_position] is not '[' || text[_position + 1] is not ('\'' or '"'))
            {
                return false;
            }

            var quote = text[_position + 1];

            for (var scan = _position + 2; scan < text.Length; scan++)
            {
                if (text[scan] == '\\')
                {
                    scan++;
                }
                else if (text[scan] == quote)
                {
                    return scan + 1 < text.Length && text[scan + 1] == ']';
                }
            }

            return true;
        }

        public string ReadName() => HasMore && text[_position] is '\'' or '"' ? ReadQuotedName() : ReadIdentifier();

        public string ReadQuotedBracket()
        {
            Expect('[');
            var name = ReadQuotedName();
            Expect(']');

            return name;
        }

        private string ReadQuotedName()
        {
            var quote = text[_position++];
            var name = new StringBuilder();

            while (HasMore)
            {
                var character = text[_position++];

                if (character == quote)
                {
                    return name.ToString();
                }

                if (character == '\\')
                {
                    if (HasMore is false)
                    {
                        throw new FormatException("Unterminated escape in property name.");
                    }

                    character = text[_position++];

                    if (character == 'u')
                    {
                        if (_position + 4 > text.Length || ushort.TryParse(text.AsSpan(_position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var unicode) is false)
                        {
                            throw new FormatException("Invalid Unicode escape in property name.");
                        }

                        name.Append((char)unicode);
                        _position += 4;
                        continue;
                    }

                    character = character switch
                    {
                        '\\' => '\\',
                        '\'' => '\'',
                        '"' => '"',
                        '/' => '/',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        'b' => '\b',
                        'f' => '\f',
                        _ => throw new FormatException("Invalid escape in property name.")
                    };
                }

                name.Append(character);
            }

            throw new FormatException("Unterminated quoted property name.");
        }

        public bool LooksLikeAliasBracket()
        {
            if (_position >= text.Length || text[_position] != '[')
            {
                return false;
            }

            var scan = _position + 1;

            if (scan >= text.Length || !IsIdentifierStart(text[scan]))
            {
                return false;
            }

            scan++;

            while (scan < text.Length && IsIdentifierPart(text[scan]))
            {
                scan++;
            }

            return scan < text.Length && text[scan] == ':';
        }

        public string ReadAliasBracket(out string alias)
        {
            Expect('[');
            alias = ReadIdentifier();
            Expect(':');
            var name = ReadIdentifier();
            Expect(']');

            return name;
        }

        public string ReadIndex()
        {
            Expect('[');

            var start = _position;

            if (HasMore && text[_position] == '-')
            {
                _position++;
            }

            var digitsStart = _position;

            while (HasMore && char.IsAsciiDigit(text[_position]))
            {
                _position++;
            }

            if (_position == digitsStart)
            {
                throw new FormatException($"Expected array index at position {_position} in '{text}'.");
            }

            var index = text[start.._position];

            Expect(']');

            return index;
        }

        public string ReadIdentifier()
        {
            var start = _position;

            if (!HasMore || !IsIdentifierStart(text[_position]))
            {
                throw new FormatException($"Expected identifier at position {_position} in '{text}'.");
            }

            _position++;

            while (HasMore && IsIdentifierPart(text[_position]))
            {
                _position++;
            }

            return text[start.._position];
        }

        private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c is '_' or '$';

        private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';
    }
}
