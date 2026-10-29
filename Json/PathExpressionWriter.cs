using System.Text.Json;

namespace DotNetLab.Json;

internal static class PathExpressionWriter
{
    public static void Write(Utf8JsonWriter writer, JsonTree.Node root, PathQuery query)
    {
        var targets = EvaluateSteps(root, query.Steps);

        if (query.Terminal is not null && query.Distinct is false)
        {
            WriteProjectionResults(writer, targets, query.HasArrayWildcard, query.Terminal);

            return;
        }

        IEnumerable<JsonTree.Node> results = targets;

        if (query.Terminal is not null)
        {
            results = targets.Select(target => ProjectTarget(target, query.Terminal, query.Distinct));
        }

        if (query.Distinct)
        {
            results = results.Distinct(JsonNodeValueComparer.Instance);
        }

        if (query.Terminal is null && query.RecursiveAlias is not null)
        {
            WriteAliasedResults(writer, results, query.RecursiveAlias);
        }
        else
        {
            WriteRawResults(writer, results, query.HasArrayWildcard || targets.Count is not 1);
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
                case StepKind.Element:
                    var child = node.FindChild(step);

                    if (child is not null)
                    {
                        next.Add(child);
                    }

                    break;
                case StepKind.AllChildren:
                    next.AddRange(node.Children);
                    break;
                case StepKind.AllElements:
                    if (node.Kind == JsonValueKind.Array)
                    {
                        next.AddRange(node.Children);
                    }

                    break;
                case StepKind.RecursiveName:
                    CollectDescendantsByName(node, step.Name!, next);
                    break;
                case StepKind.RecursiveAll:
                    CollectAllDescendants(node, next);
                    break;
                case StepKind.RecursiveAllElements:
                    CollectDescendantArrayElements(node, next);
                    break;
                case StepKind.RecursiveElement:
                    CollectDescendantArrayElementAt(node, step.Name!, next);
                    break;
            }
        }

        return next;
    }

    private static void CollectDescendantsByName(JsonTree.Node node, string name, List<JsonTree.Node> result)
    {
        foreach (var child in node.Children)
        {
            if (child.InArray is false && string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(child);
            }

            CollectDescendantsByName(child, name, result);
        }
    }

    private static void CollectAllDescendants(JsonTree.Node node, List<JsonTree.Node> result)
    {
        foreach (var child in node.Children)
        {
            result.Add(child);
            CollectAllDescendants(child, result);
        }
    }

    private static void CollectDescendantArrayElements(JsonTree.Node node, List<JsonTree.Node> result)
    {
        if (node.Kind == JsonValueKind.Array)
        {
            result.AddRange(node.Children);
        }

        foreach (var child in node.Children)
        {
            CollectDescendantArrayElements(child, result);
        }
    }

    private static void CollectDescendantArrayElementAt(JsonTree.Node node, string index, List<JsonTree.Node> result)
    {
        if (node.Kind == JsonValueKind.Array)
        {
            var element = node.FindChild(new PathStep(StepKind.Element, index));

            if (element is not null)
            {
                result.Add(element);
            }
        }

        foreach (var child in node.Children)
        {
            CollectDescendantArrayElementAt(child, index, result);
        }
    }

    private static void WriteRawResults(Utf8JsonWriter writer, IEnumerable<JsonTree.Node> targets, bool forceWrap)
    {
        if (forceWrap is false)
        {
            writer.WriteSubTree(targets.Single());

            return;
        }

        writer.WriteStartArray();

        foreach (var target in targets)
        {
            writer.WriteSubTree(target);
        }

        writer.WriteEndArray();
    }

    private static void WriteAliasedResults(Utf8JsonWriter writer, IEnumerable<JsonTree.Node> targets, string alias)
    {
        writer.WriteStartArray();

        foreach (var target in targets)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(alias);
            writer.WriteSubTree(target);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static JsonTree.Node ProjectTarget(JsonTree.Node target, Projection projection, bool distinct)
    {
        if (target.Kind is not JsonValueKind.Array)
        {
            return ProjectObject(target, projection);
        }

        var result = new JsonTree.Node("$") { Kind = JsonValueKind.Array };
        var elements = target.Children.Select(element => ProjectObject(element, projection));

        if (distinct)
        {
            elements = elements.Distinct(JsonNodeValueComparer.Instance);
        }

        foreach (var element in elements)
        {
            CopyValue(element, result.AddNode(result.Children.Count.ToString(), isArrayIndex: true));
        }

        return result;
    }

    private static void WriteProjectionResults(Utf8JsonWriter writer, IReadOnlyList<JsonTree.Node> targets, bool forceWrap, Projection projection)
    {
        if (forceWrap is false && targets.Count == 1)
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

    private static void WriteProjectedObject(Utf8JsonWriter writer, JsonTree.Node source, Projection projection)
    {
        writer.WriteStartObject();

        foreach (var field in projection.Fields)
        {
            var target = Resolve(source, field.Path);

            if (target is null)
            {
                continue;
            }

            writer.WritePropertyName(GetFieldName(field, target));

            if (field.Sub is null && field.ExplicitArray is false)
            {
                writer.WriteSubTree(target);
            }
            else if (field.ExplicitArray || target.Kind == JsonValueKind.Array)
            {
                writer.WriteStartArray();

                if (target.Kind == JsonValueKind.Array)
                {
                    foreach (var element in target.Children)
                    {
                        if (field.Sub is null)
                        {
                            writer.WriteSubTree(element);
                        }
                        else
                        {
                            WriteProjectedObject(writer, element, field.Sub);
                        }
                    }
                }

                writer.WriteEndArray();
            }
            else
            {
                WriteProjectedObject(writer, target, field.Sub!);
            }
        }

        writer.WriteEndObject();
    }

    private static JsonTree.Node ProjectObject(JsonTree.Node source, Projection projection)
    {
        var result = new JsonTree.Node("$") { Kind = JsonValueKind.Object };
        ProjectFields(source, projection, result);

        return result;
    }

    private static void ProjectFields(JsonTree.Node source, Projection projection, JsonTree.Node destination)
    {
        destination.Kind = JsonValueKind.Object;

        foreach (var field in projection.Fields)
        {
            var target = Resolve(source, field.Path);

            if (target is null)
            {
                continue;
            }

            var property = destination.AddNode(GetFieldName(field, target), isArrayIndex: false);

            if (field.Sub is null && field.ExplicitArray is false)
            {
                CopyValue(target, property);
                continue;
            }

            if (field.ExplicitArray || target.Kind == JsonValueKind.Array)
            {
                property.Kind = JsonValueKind.Array;

                if (target.Kind == JsonValueKind.Array)
                {
                    ProjectArrayElements(target, field.Sub, property);
                }
            }
            else
            {
                ProjectFields(target, field.Sub!, property);
            }
        }
    }

    private static void ProjectArrayElements(JsonTree.Node source, Projection? projection, JsonTree.Node destination)
    {
        foreach (var element in source.Children)
        {
            var child = destination.AddNode(destination.Children.Count.ToString(), isArrayIndex: true);

            if (projection is null)
            {
                CopyValue(element, child);
            }
            else
            {
                ProjectFields(element, projection, child);
            }
        }
    }

    private static void CopyValue(JsonTree.Node source, JsonTree.Node destination)
    {
        destination.Kind = source.Kind;
        destination.Value = source.Value;

        foreach (var child in source.Children)
        {
            CopyValue(child, destination.AddNode(child.Name, child.InArray));
        }
    }

    private static JsonTree.Node? Resolve(JsonTree.Node parent, IReadOnlyList<PathStep> path)
    {
        var target = parent;

        foreach (var step in path)
        {
            target = target.FindChild(step);

            if (target is null)
            {
                return null;
            }
        }

        return target;
    }

    private static string GetFieldName(FieldExpr field, JsonTree.Node target) => field.Alias ?? (target.InArray ? field.Path.Last(step => step.Kind == StepKind.Name).Name! : target.Name);
}
