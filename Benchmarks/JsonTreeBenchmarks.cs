namespace Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 5, iterationCount: 10)]
public class JsonTreeBenchmarks
{
    private string _jsonString = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _jsonString = File.ReadAllText("example.json", Encoding.UTF8);
    }

    [Benchmark]
    public JsonTree CreateTree() => JsonTree.Create(_jsonString);
}
