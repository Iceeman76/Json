using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Json;

public partial record JsonTree
{
    public sealed partial record Node(string Name = "")
    {
        public string? Value { get; set; }
        public JsonValueKind Kind { get; set; }
        public bool InArray { get; set; }
        public IEnumerable<Node> Children => _children;

        private Node? _parent;
        private readonly List<Node> _children = [];

        private bool HasChildren => _children.Count > 0;
        public string Identity => string.IsNullOrEmpty(_parent?.Identity) ? Name : $"{_parent?.Identity}.{Name}".Replace(".[", "[");

        private static readonly Regex IdentifierRegex = JavaScriptIdentifierRegex();

        internal Node AddNode(string name, bool isArrayIndex)
        {
            if (isArrayIndex == false && IdentifierRegex.IsMatch(name) == false)
            {
                name = $"['{name}']";
            }

            var node = new Node(name) { _parent = this };
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
                var indent = new string(' ', depth * 4);
                sb.Append(indent).Append(Identity);

                if (HasChildren)
                {
                    sb.Append(':');
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
            writer.WriteNode(this);

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

        [GeneratedRegex(@"^[A-Za-z_\$][A-Za-z0-9_\$]*$", RegexOptions.Compiled)]
        private static partial Regex JavaScriptIdentifierRegex();
    }
}
