namespace DotNetLab.Json;

internal enum StepKind
{
    Name,
    Element,
    AllChildren,
    AllElements,
    RecursiveName,
    RecursiveAll,
    RecursiveAllElements,
    RecursiveElement
}

internal sealed record PathStep(StepKind Kind, string? Name, string? Alias = null);

internal sealed record FieldExpr(IReadOnlyList<PathStep> Path, string? Alias, bool ExplicitArray, Projection? Sub);

internal sealed record Projection(IReadOnlyList<FieldExpr> Fields);

internal sealed record PathQuery(IReadOnlyList<PathStep> Steps, bool HasArrayWildcard, Projection? Terminal, string? RecursiveAlias = null, bool Distinct = false);
