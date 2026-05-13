namespace Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 5, iterationCount: 10)]
public class JsonTreeBenchmarks
{
    private string _jsonString = null!;

    [Params("v1", "v2")]
    private string Version { get; set; } = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _jsonString = File.ReadAllText("example.json", Encoding.UTF8);
    }

    [Benchmark]
    public IJsonTree CreateTree() => Version switch
    {
        "v1" => Json.v1.JsonTree.Create(_jsonString),
        "v2" => Json.v2.JsonTree.Create(_jsonString),
        _ => throw new ArgumentOutOfRangeException()
    };
}
