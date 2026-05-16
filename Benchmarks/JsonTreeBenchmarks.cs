namespace Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 5, iterationCount: 20)]
public class TreeCreate
{
    private string _jsonString = null!;

    [Params("v1", "v2", "v3")]
    public string Version { get; set; } = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _jsonString = File.ReadAllText("example.json", Encoding.UTF8);
    }

    [Benchmark(Description = "Tree-Create")]
    public IJsonTree Run() => Version switch
    {
        "v1" => Json.v1.JsonTree.Create(_jsonString),
        "v2" => Json.v2.JsonTree.Create(_jsonString),
        "v3" => Json.v3.JsonTree.Create(_jsonString),
        _ => throw new ArgumentOutOfRangeException()
    };
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 5, iterationCount: 20)]
public abstract class JsonTreeBenchmarkBase
{
    protected IJsonTree Tree = null!;

    [Params("v1", "v2", "v3")]
    public string Version { get; set; } = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var jsonString = File.ReadAllText("example.json", Encoding.UTF8);

        Tree = Version switch
        {
            "v1" => Json.v1.JsonTree.Create(jsonString),
            "v2" => Json.v2.JsonTree.Create(jsonString),
            "v3" => Json.v3.JsonTree.Create(jsonString),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public class TreeFlatten : JsonTreeBenchmarkBase
{
    [Benchmark(Description = "Tree-Flatten")]
    public IJsonTreeNode[] Run() => Tree.Flatten();
}

public class TreeFindNode : JsonTreeBenchmarkBase
{
    [Benchmark(Description = "Tree-FindNode")]
    public IJsonTreeNode? Run() => Tree.FindNode("$.draw.events[0].description");
}

public class TreeToJsonString : JsonTreeBenchmarkBase
{
    [Benchmark(Description = "Tree-ToJsonString")]
    public string Run() => Tree.ToJsonString();
}

public class TreeToString : JsonTreeBenchmarkBase
{
    [Benchmark(Description = "Tree-ToString")]
    public string Run() => Tree.ToString()!;
}
