using System.Text.Json;

namespace Json;

internal static class PathExpressionWriter
{
    public static void Write(Utf8JsonWriter writer, JsonTree.Node root, PathQuery query)
    {
        var targets = EvaluateSteps(root, query.Steps);

        if (query.Distinct)
        {
            ApplyDistinct(targets, query.Terminal is null
                ? JsonNodeValueComparer.Instance
                : new ProjectedNodeComparer(query.Terminal));
        }

        if (query.Terminal is not null)
        {
            WriteProjectionResults(writer, targets, query.HasArrayWildcard, query.Terminal, query.Distinct);
        }
        else if (query.RecursiveAlias is not null)
        {
            WriteAliasedResults(writer, targets, query.RecursiveAlias);
        }
        else
        {
            WriteRawResults(writer, targets, query.HasArrayWildcard);
        }
    }

    private static List<JsonTree.Node> EvaluateSteps(JsonTree.Node root, IReadOnlyList<PathStep> steps)
    {
        var current = new List<JsonTree.Node> { root };

        return steps.Aggregate(current, ApplyStep);
    }

    private static void ApplyDistinct(List<JsonTree.Node> targets, IEqualityComparer<JsonTree.Node> comparer)
    {
        var seen = new HashSet<JsonTree.Node>(comparer);
        var writeIndex = 0;

        for (var readIndex = 0; readIndex < targets.Count; readIndex++)
        {
            var target = targets[readIndex];

            if (seen.Add(target))
            {
                targets[writeIndex++] = target;
            }
        }

        if (writeIndex < targets.Count)
        {
            targets.RemoveRange(writeIndex, targets.Count - writeIndex);
        }
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

    private static void WriteAliasedResults(Utf8JsonWriter writer, List<JsonTree.Node> targets, string alias)
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

    private static void WriteProjectionResults(Utf8JsonWriter writer, List<JsonTree.Node> targets, bool forceWrap, Projection projection, bool distinct)
    {
        if (forceWrap == false && targets.Count == 1)
        {
            WriteProjectedTarget(writer, targets[0], projection, distinct);
            return;
        }

        writer.WriteStartArray();

        foreach (var target in targets)
        {
            WriteProjectedTarget(writer, target, projection, distinct);
        }

        writer.WriteEndArray();
    }

    private static void WriteProjectedTarget(Utf8JsonWriter writer, JsonTree.Node target, Projection projection, bool distinct)
    {
        if (target.Kind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            var elements = distinct ? target.Children.Distinct(new ProjectedNodeComparer(projection)) : target.Children;

            foreach (var element in elements)
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
            var child = target.FindChild(segment);

            if (child is null)
            {
                return;
            }

            target = child;
        }

        if (field.Sub is null)
        {
            writer.WritePropertyName(GetFieldName(field, target));
            writer.WriteSubTree(target);

            return;
        }

        var asArray = field.ExplicitArray || target.Kind == JsonValueKind.Array;

        writer.WritePropertyName(GetFieldName(field, target));

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

    private static string GetFieldName(FieldExpr field, JsonTree.Node target) => field.Alias ?? (target.InArray ? field.Path.Last(step => step.Kind == StepKind.Name).Name! : target.Name);

    private sealed class ProjectedNodeComparer(Projection projection) : IEqualityComparer<JsonTree.Node>
    {
        public bool Equals(JsonTree.Node? firstNode, JsonTree.Node? secondNode) => ProjectedTargetEquals(firstNode!, secondNode!, projection);

        public int GetHashCode(JsonTree.Node node) => ProjectedTargetHash(node, projection);

        private static bool ProjectedTargetEquals(JsonTree.Node firstNode, JsonTree.Node secondNode, Projection current)
        {
            if (firstNode.Kind == JsonValueKind.Array || secondNode.Kind == JsonValueKind.Array)
            {
                return firstNode.Kind == secondNode.Kind && ProjectedArrayEquals(firstNode.Children, secondNode.Children, current);
            }

            return ProjectedObjectEquals(firstNode, secondNode, current);
        }

        private static bool ProjectedArrayEquals(IReadOnlyList<JsonTree.Node> firstElements, IReadOnlyList<JsonTree.Node> secondElements, Projection current) =>
            firstElements.Count == secondElements.Count && firstElements.Zip(secondElements).All(pair => ProjectedObjectEquals(pair.First, pair.Second, current));

        private static bool ProjectedObjectEquals(JsonTree.Node firstNode, JsonTree.Node secondNode, Projection current)
        {
            foreach (var field in current.Fields)
            {
                var firstValue = Resolve(firstNode, field);
                var secondValue = Resolve(secondNode, field);

                if (firstValue is null || secondValue is null)
                {
                    if (ReferenceEquals(firstValue, secondValue) is false)
                    {
                        return false;
                    }

                    continue;
                }

                if (string.Equals(GetFieldName(field, firstValue), GetFieldName(field, secondValue), StringComparison.Ordinal) is false)
                {
                    return false;
                }

                var equal = field.Sub is null
                    ? JsonNodeValueComparer.Instance.Equals(firstValue, secondValue)
                    : field.ExplicitArray
                        ? ProjectedArrayEquals(firstValue.Children, secondValue.Children, field.Sub)
                        : ProjectedTargetEquals(firstValue, secondValue, field.Sub);

                if (equal is false)
                {
                    return false;
                }
            }

            return true;
        }

        private static int ProjectedTargetHash(JsonTree.Node node, Projection current)
        {
            var hash = new HashCode();

            if (node.Kind == JsonValueKind.Array)
            {
                hash.Add(ProjectedArrayHash(node.Children, current));
            }
            else
            {
                hash.Add(ProjectedObjectHash(node, current));
            }

            return hash.ToHashCode();
        }

        private static int ProjectedArrayHash(IReadOnlyList<JsonTree.Node> elements, Projection current)
        {
            var hash = new HashCode();

            foreach (var element in elements)
            {
                hash.Add(ProjectedObjectHash(element, current));
            }

            return hash.ToHashCode();
        }

        private static int ProjectedObjectHash(JsonTree.Node node, Projection current)
        {
            var hash = new HashCode();

            foreach (var field in current.Fields)
            {
                var value = Resolve(node, field);

                if (value is null)
                {
                    hash.Add(0);
                    continue;
                }

                hash.Add(GetFieldName(field, value), StringComparer.Ordinal);
                hash.Add(field.Sub is null
                    ? JsonNodeValueComparer.Instance.GetHashCode(value)
                    : field.ExplicitArray
                        ? ProjectedArrayHash(value.Children, field.Sub)
                        : ProjectedTargetHash(value, field.Sub));
            }

            return hash.ToHashCode();
        }

        private static JsonTree.Node? Resolve(JsonTree.Node parent, FieldExpr field)
        {
            var target = parent;

            foreach (var segment in field.Path)
            {
                var child = target.FindChild(segment);

                if (child is null)
                {
                    return null;
                }

                target = child;
            }

            return target;
        }
    }
}
