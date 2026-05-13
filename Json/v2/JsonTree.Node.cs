using System.Text;
using System.Text.Json;
namespace Json.v2;

public sealed partial class JsonTree
{
    public sealed class Node(string name = "") : IJsonTreeNode
    {
        public string Name { get; } = name;
        public string PathSegment { get; private init; } = name;
        public string Identity { get; private init; } = name;
        public string? Value { get; set; }
        public JsonValueKind Kind { get; set; }
        public bool InArray { get; set; }
        public IReadOnlyList<IJsonTreeNode> Children => _children;

        private bool HasChildren => _children.Count > 0;

        private Node? _parent;
        private readonly List<Node> _children = new(64);

        internal Node AddNode(string name, bool isArrayIndex)
        {
            var pathSegment = isArrayIndex
                ? $"[{name}]"
                : IsJavaScriptIdentifier(name)
                    ? name
                    : $"['{name}']";

            var identity = string.IsNullOrEmpty(Identity)
                ? pathSegment
                : pathSegment[0] == '['
                    ? $"{Identity}{pathSegment}"
                    : $"{Identity}.{pathSegment}";

            var node = new Node(name)
            {
                _parent = this,
                PathSegment = pathSegment,
                Identity = identity
            };

            _children.Add(node);

            return node;
        }

        private static bool IsJavaScriptIdentifier(string name)
        {
            if (name.Length == 0)
            {
                return false;
            }

            var first = name[0];
            
            if (!(char.IsLetter(first) || first == '_' || first == '$'))
            {
                return false;
            }

            for (var i = 1; i < name.Length; i++)
            {
                var c = name[i];
                
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '$'))
                {
                    return false;
                }
            }

            return true;
        }

        public IJsonTreeNode[] Flatten()
        {
            var result = new List<IJsonTreeNode>();

            CollectNodes(result);

            return result.ToArray();
        }

        private void CollectNodes(List<IJsonTreeNode> result)
        {
            if (_parent is not null)
            {
                result.Add(this);
            }

            foreach (var child in _children)
            {
                child.CollectNodes(result);
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

        public void BuildRecursiveJson(Utf8JsonWriter writer)
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
    }
}
