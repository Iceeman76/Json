namespace Json.v3;

internal static class PathParser
{
    public static PathQuery Parse(string expression)
    {
        var cursor = new Cursor(expression);
        cursor.Expect('$');

        var steps = new List<PathStep>();
        var hasArrayWildcard = false;

        while (cursor.HasMore && !IsTerminalProjection(cursor))
        {
            var step = ParseStep(cursor);
            steps.Add(step);

            if (step.Kind == StepKind.AllElements)
            {
                hasArrayWildcard = true;
            }
        }

        var terminal = cursor.HasMore ? ParseProjection(cursor) : null;

        return new PathQuery(steps, hasArrayWildcard, terminal);
    }

    private static bool IsTerminalProjection(Cursor cursor)
    {
        return cursor.Peek() == '[' && !cursor.LooksLikeWildcardBracket();
    }

    private static PathStep ParseStep(Cursor cursor)
    {
        if (cursor.Peek() == '.')
        {
            cursor.Advance();

            if (cursor.HasMore && cursor.Peek() == '*')
            {
                cursor.Advance();
                return new PathStep(StepKind.AllChildren, null);
            }

            return new PathStep(StepKind.Name, cursor.ReadIdentifier());
        }

        cursor.Skip("[*]");
        
        return new PathStep(StepKind.AllElements, null);
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
        var path = new List<string> { cursor.ReadIdentifier() };

        while (cursor.HasMore && cursor.Peek() == '.')
        {
            cursor.Advance();
            path.Add(cursor.ReadIdentifier());
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

        return new FieldExpr(path, explicitArray, sub);
    }

    private sealed class Cursor(string text)
    {
        private int _position;

        public bool HasMore => _position < text.Length;

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

        public bool LooksLikeWildcardBracket()
        {
            return _position + 2 < text.Length
                && text[_position] == '['
                && text[_position + 1] == '*'
                && text[_position + 2] == ']';
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
