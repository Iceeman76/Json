using System.Text;
using System.Text.Json;

namespace DotNetLab.Json;

public sealed partial class JsonTree
{
    public sealed partial class Node(string name = "")
    {
        public string Name { get; } = name;
        public string PathSegment { get; private init; } = name;
        public string JsonPath => _jsonPath ??= BuildJsonPath();
        public object? Value { get; set; }

        private string? _jsonPath;
        private JsonTree? _tree;
        public JsonValueKind Kind { get; set; }
        public bool InArray { get; private init; }
        public IReadOnlyList<Node> Children => _children;

        private bool HasChildren => _children.Count > 0;

        private Node? _parent;
        private readonly List<Node> _children = [];

        internal void AttachTo(JsonTree tree)
        {
            _tree = tree;
        }

        internal Node AddNode(string name, bool isArrayIndex)
        {
            var node = new Node(name)
            {
                _parent = this,
                _tree = _tree,
                InArray = isArrayIndex,
                PathSegment = BuildPathSegment(name, isArrayIndex)
            };

            _children.Add(node);
            _tree?.InvalidateCaches();

            return node;
        }

        private static string BuildPathSegment(string name, bool isArrayIndex)
        {
            if (isArrayIndex)
            {
                return $"[{name}]";
            }

            if (IsJavaScriptIdentifier(name))
            {
                return name;
            }

            var escapedName = name.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

            return $"['{escapedName}']";
        }

        internal Node? FindChild(PathStep step)
        {
            if (step.Kind == StepKind.Element)
            {
                if (Kind is not JsonValueKind.Array || int.TryParse(step.Name, out var index) is false)
                {
                    return null;
                }

                var resolvedIndex = index < 0 ? _children.Count + index : index;

                return resolvedIndex >= 0 && resolvedIndex < _children.Count ? _children[resolvedIndex] : null;
            }

            if (Kind is not JsonValueKind.Object)
            {
                return null;
            }

            return _children.FirstOrDefault(child => string.Equals(child.Name, step.Name, StringComparison.Ordinal))
                ?? _children.FirstOrDefault(child => string.Equals(child.Name, step.Name, StringComparison.OrdinalIgnoreCase));
        }

        private string BuildJsonPath()
        {
            if (_parent is null)
            {
                return Name;
            }

            var parentJsonPath = _parent.JsonPath;

            return PathSegment[0] == '['
                ? string.Concat(parentJsonPath, PathSegment)
                : string.Concat(parentJsonPath, ".", PathSegment);
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

        public Node[] Flatten()
        {
            var result = new List<Node>();

            CollectNodes(result);

            return result.ToArray();
        }

        private void CollectNodes(List<Node> result)
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
            if (_parent is null && HasChildren)
            {
                depth--;
            }
            else
            {
                sb.Append(' ', depth * 4).Append(JsonPath);

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
            return $"Name = {Name}, Value = {Value}, Kind = {Kind}, JsonPath = {JsonPath}";
        }
    }
}
