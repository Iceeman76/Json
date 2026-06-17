namespace Json;

internal enum StepKind
{
    Name,
    AllChildren,
    AllElements,
    RecursiveName,
    RecursiveAll,
    RecursiveAllElements,
    RecursiveElement
}

internal sealed record PathStep(StepKind Kind, string? Name);

internal sealed record FieldExpr(IReadOnlyList<string> Path, string? Alias, bool ExplicitArray, Projection? Sub);

internal sealed record Projection(IReadOnlyList<FieldExpr> Fields);

internal sealed record PathQuery(IReadOnlyList<PathStep> Steps, bool HasArrayWildcard, Projection? Terminal);
