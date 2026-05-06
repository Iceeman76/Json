namespace Benchmarks;

[MemoryDiagnoser]
[SimpleJob]
public class JsonTreeBenchmarks
{
    private string _jsonString = string.Empty;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _jsonString = File.ReadAllText("example.json", Encoding.UTF8);
    }

    [Benchmark]
    public JsonTree Create() => JsonTree.Create(_jsonString);
}
