namespace Json;

internal static class PathParser
{
    public static PathQuery Parse(string expression)
    {
        var cursor = new Cursor(expression);
        cursor.Expect('$');

        var steps = new List<PathStep>();
        var hasArrayWildcard = false;
        string? recursiveAlias = null;

        while (cursor.HasMore && cursor.Peek() != '|' && !IsTerminalProjection(cursor))
        {
            var step = ParseStep(cursor);
            steps.Add(step);

            if (step.Kind is StepKind.AllElements
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

    private static bool IsTerminalProjection(Cursor cursor)
    {
        return cursor.Peek() == '[' && !cursor.LooksLikeWildcardBracket() && !cursor.LooksLikeIndexBracket();
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

        return new PathStep(StepKind.Name, cursor.ReadIndex());
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
        var firstIdentifier = cursor.ReadIdentifier();

        string? alias = null;

        if (cursor.HasMore && cursor.Peek() == ':')
        {
            alias = firstIdentifier;
            cursor.Advance();
            firstIdentifier = cursor.ReadIdentifier();
        }

        var path = new List<string> { firstIdentifier };

        while (cursor.HasMore)
        {
            if (cursor.Peek() == '.')
            {
                cursor.Advance();
                path.Add(cursor.ReadIdentifier());
            }
            else if (cursor.LooksLikeIndexBracket())
            {
                path.Add(cursor.ReadIndex());
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

            while (HasMore && char.IsAsciiDigit(text[_position]))
            {
                _position++;
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

        private static bool IsIdentifierStart(char c) => char.IsAsciiLetter(c) || c == '_';

        private static bool IsIdentifierPart(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
    }
}
