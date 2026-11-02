namespace Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class SelectorDistinctWideObjects
{
    private JsonFieldSelector _selector = null!;

    [Params(1000, 4000, 8000)]
    public int PropertyCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var properties = Enumerable.Range(0, PropertyCount).Select(index => $"\"property{index}\":{index}");
        var jsonObject = "{" + string.Join(',', properties) + "}";
        _selector = new JsonFieldSelector($"[{jsonObject},{jsonObject}]");
    }

    [Benchmark]
    public string Select() => _selector.Select("$[*]|distinct");
}
