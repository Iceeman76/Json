using System.Text.Json;

namespace Json;

internal static class PathExpressionWriter
{
    public static void Write(Utf8JsonWriter writer, JsonTree.Node root, PathQuery query)
    {
        var targets = EvaluateSteps(root, query.Steps);

        if (query.Terminal is null)
        {
            WriteRawResults(writer, targets, query.HasArrayWildcard);
        }
        else
        {
            WriteProjectionResults(writer, targets, query.HasArrayWildcard, query.Terminal);
        }
    }

    private static List<JsonTree.Node> EvaluateSteps(JsonTree.Node root, IReadOnlyList<PathStep> steps)
    {
        var current = new List<JsonTree.Node> { root };

        return steps.Aggregate(current, ApplyStep);
    }

    private static List<JsonTree.Node> ApplyStep(List<JsonTree.Node> nodes, PathStep step)
    {
        var next = new List<JsonTree.Node>();

        foreach (var node in nodes)
        {
            switch (step.Kind)
            {
                case StepKind.Name:
                    var child = FindChildByName(node, step.Name!);

                    if (child is not null)
                    {
                        next.Add(child);
                    }

                    break;
                case StepKind.AllChildren:
                case StepKind.AllElements:
                    next.AddRange(node.Children);
                    break;
                case StepKind.RecursiveName:
                    CollectDescendantsByName(node, step.Name!, next);
                    break;
            }
        }

        return next;
    }

    private static void CollectDescendantsByName(JsonTree.Node node, string name, List<JsonTree.Node> result)
    {
        foreach (var child in node.Children)
        {
            if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(child);
            }

            CollectDescendantsByName(child, name, result);
        }
    }

    private static JsonTree.Node? FindChildByName(JsonTree.Node parent, string name)
    {
        if (IsNegativeIndex(name, out var negativeIndex))
        {
            var resolved = parent.Children.Count + negativeIndex;

            return resolved >= 0 && resolved < parent.Children.Count ? parent.Children[resolved] : null;
        }

        return parent.Children.FirstOrDefault(child => string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNegativeIndex(string name, out int index)
    {
        index = 0;

        return name.Length > 1 && name[0] == '-' && int.TryParse(name, out index);
    }

    private static void WriteRawResults(Utf8JsonWriter writer, List<JsonTree.Node> targets, bool forceWrap)
    {
        if (!forceWrap && targets.Count == 1)
        {
            writer.WriteSubTree(targets[0]);
            return;
        }

        writer.WriteStartArray();

        foreach (var target in targets)
        {
            writer.WriteSubTree(target);
        }

        writer.WriteEndArray();
    }

    private static void WriteProjectionResults(Utf8JsonWriter writer, List<JsonTree.Node> targets, bool forceWrap, Projection projection)
    {
        if (forceWrap == false && targets.Count == 1)
        {
            WriteProjectedTarget(writer, targets[0], projection);
            return;
        }

        writer.WriteStartArray();

        foreach (var target in targets)
        {
            WriteProjectedTarget(writer, target, projection);
        }

        writer.WriteEndArray();
    }

    private static void WriteProjectedTarget(Utf8JsonWriter writer, JsonTree.Node target, Projection projection)
    {
        if (target.Kind == JsonValueKind.Array)
        {
            writer.WriteStartArray();

            foreach (var element in target.Children)
            {
                WriteProjectedObject(writer, element, projection);
            }

            writer.WriteEndArray();
            return;
        }

        WriteProjectedObject(writer, target, projection);
    }

    private static void WriteProjectedObject(Utf8JsonWriter writer, JsonTree.Node parent, Projection projection)
    {
        writer.WriteStartObject();

        foreach (var field in projection.Fields)
        {
            WriteField(writer, parent, field);
        }

        writer.WriteEndObject();
    }

    private static void WriteField(Utf8JsonWriter writer, JsonTree.Node parent, FieldExpr field)
    {
        var target = parent;

        foreach (var segment in field.Path)
        {
            var child = FindChildByName(target, segment);

            if (child is null)
            {
                return;
            }

            target = child;
        }

        if (field.Sub is null)
        {
            if (field.Alias is null)
            {
                target.BuildRecursiveJson(writer);
                return;
            }

            writer.WritePropertyName(field.Alias);
            writer.WriteSubTree(target);

            return;
        }

        var asArray = field.ExplicitArray || target.Kind == JsonValueKind.Array;

        writer.WritePropertyName(field.Alias ?? target.Name);

        if (asArray)
        {
            writer.WriteStartArray();

            foreach (var element in target.Children)
            {
                WriteProjectedObject(writer, element, field.Sub);
            }

            writer.WriteEndArray();

            return;
        }

        WriteProjectedObject(writer, target, field.Sub);
    }
}
