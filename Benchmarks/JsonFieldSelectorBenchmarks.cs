namespace Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 5, iterationCount: 20)]
public abstract class JsonFieldSelectorBenchmarkBase
{
    protected IJsonFieldSelector Selector = null!;

    [Params("v1", "v2")]
    public string Version { get; set; } = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var jsonString = File.ReadAllText("example.json", Encoding.UTF8);

        Selector = Version switch
        {
            "v1" => new Json.v1.JsonFieldSelector(jsonString),
            "v2" => new Json.v2.JsonFieldSelector(jsonString),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public class SelectorRoot : JsonFieldSelectorBenchmarkBase
{
    [Benchmark(Description = "Selector-Root")]
    public string Run() => Selector.Select("$");
}

public class SelectorSimplePath : JsonFieldSelectorBenchmarkBase
{
    [Benchmark(Description = "Selector-SimplePath")]
    public string Run() => Selector.Select("$.draw.drawState");
}

public class SelectorWildcard : JsonFieldSelectorBenchmarkBase
{
    [Benchmark(Description = "Selector-Wildcard")]
    public string Run() => Selector.Select("$.draw.events[*].description");
}

public class SelectorProjection : JsonFieldSelectorBenchmarkBase
{
    [Benchmark(Description = "Selector-Projection")]
    public string Run() => Selector.Select("$.draw.events[*][eventNumber,description]");
}
