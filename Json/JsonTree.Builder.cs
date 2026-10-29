using System.Text.Json;

namespace DotNetLab.Json;

public sealed partial class JsonTree
{
    public static JsonTree Create() => new(new Node("$") { Kind = JsonValueKind.Object });
    public static JsonTree CreateArray() => new(new Node("$") { Kind = JsonValueKind.Array });

    public JsonTree Add(Action<Node> configure)
    {
        configure(Root);

        return this;
    }

    public JsonTree Add(string name, object? value)
    {
        Root.Add(name, value);

        return this;
    }

    public JsonTree AddNull(string name)
    {
        Root.AddNull(name);

        return this;
    }

    public JsonTree AddObject(string name, Action<Node> configure)
    {
        Root.AddObject(name, configure);

        return this;
    }

    public JsonTree AddArray(string name, Action<Node> configure)
    {
        Root.AddArray(name, configure);

        return this;
    }

    public sealed partial class Node
    {
        public Node Add(string name, object? value)
        {
            EnsureObject();

            var node = AddNode(name, isArrayIndex: false);
            SetValue(node, value);

            return this;
        }

        public Node Add(object? value)
        {
            EnsureArray();

            var node = AddNode(_children.Count.ToString(), isArrayIndex: true);
            SetValue(node, value);

            return this;
        }

        public Node AddNull(string name) => Add(name, null);

        public Node AddNull() => Add(null);

        public Node AddObject(string name, Action<Node> configure)
        {
            EnsureObject();

            var node = AddNode(name, isArrayIndex: false);
            node.Kind = JsonValueKind.Object;
            configure(node);

            return this;
        }

        public Node AddObject(Action<Node> configure)
        {
            EnsureArray();

            var node = AddNode(_children.Count.ToString(), isArrayIndex: true);
            node.Kind = JsonValueKind.Object;
            configure(node);

            return this;
        }

        public Node AddArray(string name, Action<Node> configure)
        {
            EnsureObject();

            var node = AddNode(name, isArrayIndex: false);
            node.Kind = JsonValueKind.Array;
            configure(node);

            return this;
        }

        public Node AddArray(Action<Node> configure)
        {
            EnsureArray();

            var node = AddNode(_children.Count.ToString(), isArrayIndex: true);
            node.Kind = JsonValueKind.Array;
            configure(node);

            return this;
        }

        private static void SetValue(Node node, object? value)
        {
            switch (value)
            {
                case null:
                    node.Kind = JsonValueKind.Null;
                    node.Value = null;
                    break;
                case string stringValue:
                    node.Kind = JsonValueKind.String;
                    node.Value = stringValue;
                    break;
                case bool boolValue:
                    node.Kind = boolValue ? JsonValueKind.True : JsonValueKind.False;
                    node.Value = boolValue;
                    break;
                case byte or sbyte or short or ushort or int or uint or long:
                    node.Kind = JsonValueKind.Number;
                    node.Value = Convert.ToInt64(value);
                    break;
                case ulong ulongValue:
                    node.Kind = JsonValueKind.Number;
                    node.Value = ulongValue <= long.MaxValue ? Convert.ToInt64(ulongValue) : Convert.ToDecimal(ulongValue);
                    break;
                case float floatValue:
                    node.Kind = JsonValueKind.Number;
                    node.Value = Convert.ToDouble(floatValue);
                    break;
                case double doubleValue:
                    node.Kind = JsonValueKind.Number;
                    node.Value = doubleValue;
                    break;
                case decimal decimalValue:
                    node.Kind = JsonValueKind.Number;
                    node.Value = decimalValue;
                    break;
                case JsonNumber number:
                    node.Kind = JsonValueKind.Number;
                    node.Value = number;
                    break;
                default:
                    throw new ArgumentException($"Unsupported JSON value type: {value.GetType().FullName}", nameof(value));
            }
        }

        private void EnsureObject()
        {
            if (Kind is not JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Node '{JsonPath}' has Kind = {Kind}; object properties can only be added to object nodes.");
            }
        }

        private void EnsureArray()
        {
            if (Kind is not JsonValueKind.Array)
            {
                throw new InvalidOperationException($"Node '{JsonPath}' has Kind = {Kind}; array items can only be added to array nodes.");
            }
        }
    }
}
