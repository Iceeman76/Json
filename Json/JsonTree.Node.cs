using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Json;

public sealed partial class JsonTree
{
    public sealed partial class Node(string name = "")
    {
        public string Name { get; } = name;
        public string PathSegment { get; private init; } = name;
        public string Identity { get; private init; } = name;
        public string? Value { get; set; }
        public JsonValueKind Kind { get; set; }
        public bool InArray { get; set; }
        public IReadOnlyList<Node> Children => _children;

        private bool HasChildren => _children.Count > 0;

        private Node? _parent;
        private readonly List<Node> _children = [];

        private static readonly Regex IdentifierRegex = JavaScriptIdentifierRegex();

        internal Node AddNode(string name, bool isArrayIndex)
        {
            var pathSegment = isArrayIndex
                ? $"[{name}]"
                : IdentifierRegex.IsMatch(name) ? name : $"['{name}']";

            var identity = string.IsNullOrEmpty(Identity)
                ? pathSegment
                : $"{Identity}.{pathSegment}".Replace(".[", "[");

            var node = new Node(name)
            {
                _parent = this,
                PathSegment = pathSegment,
                Identity = identity
            };

            _children.Add(node);

            return node;
        }

        public IEnumerable<Node> Flatten()
        {
            if (_parent is not null) yield return this;

            foreach (var child in Children)
            {
                foreach (var descendant in child.Flatten())
                {
                    yield return descendant;
                }
            }
        }

        private void BuildRecursiveString(StringBuilder sb, int depth = 0)
        {
            if (_parent is null)
            {
                depth--;
            }
            else
            {
                sb.Append(' ', depth * 4).Append(Identity);

                if (HasChildren)
                {
                    sb.Append(':');
                }
                else if (Kind is JsonValueKind.Object)
                {
                    sb.Append(" = {}");
                }
                else if (Kind is JsonValueKind.Array)
                {
                    sb.Append(" = []");
                }
                else
                {
                    sb.Append(" = ").Append(Value ?? "null");
                }

                sb.Append($" (Kind={Kind}, Name={Name})");
                sb.AppendLine();
            }

            foreach (var node in _children)
            {
                node.BuildRecursiveString(sb, depth + 1);
            }
        }

        internal void BuildRecursiveJson(Utf8JsonWriter writer)
        {
            writer.WriteNode(this, InArray);

            foreach (var node in _children)
            {
                node.BuildRecursiveJson(writer);
            }

            writer.WriteEndNode(this);
        }

        public string BuildBranchString()
        {
            var sb = new StringBuilder();

            BuildRecursiveString(sb);

            return sb.ToString().TrimEnd();
        }

        public override string ToString()
        {
            return $"Name = {Name}, Value = {Value}, Kind = {Kind}, Identity = {Identity}";
        }

        [GeneratedRegex(@"^[A-Za-z_\$][A-Za-z0-9_\$]*$")]
        private static partial Regex JavaScriptIdentifierRegex();
    }
}
