namespace Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 5, iterationCount: 20)]
public class TreeCreate
{
    private string _jsonString = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _jsonString = File.ReadAllText("example.json", Encoding.UTF8);
    }

    [Benchmark(Description = "Tree-Create")]
    public JsonTree Run() => JsonTree.Create(_jsonString);
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 5, iterationCount: 20)]
public abstract class JsonTreeBenchmarkBase
{
    protected JsonTree Tree { get; private set; } = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var jsonString = File.ReadAllText("example.json", Encoding.UTF8);

        Tree = JsonTree.Create(jsonString);
    }
}

public class TreeFlatten : JsonTreeBenchmarkBase
{
    [Benchmark(Description = "Tree-Flatten")]
    public JsonTree.Node[] Run() => Tree.Flatten();
}

public class TreeFindNode : JsonTreeBenchmarkBase
{
    [Benchmark(Description = "Tree-FindNode")]
    public JsonTree.Node? Run() => Tree.FindNode("$.draw.events[0].description");
}

public class TreeToJsonString : JsonTreeBenchmarkBase
{
    [Benchmark(Description = "Tree-ToJsonString")]
    public string Run() => Tree.ToJsonString();
}

public class TreeToString : JsonTreeBenchmarkBase
{
    [Benchmark(Description = "Tree-ToString")]
    public string Run() => Tree.ToString();
}
