using System.Buffers;

namespace Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 5, iterationCount: 20)]
public class SelectorPipeline
{
    private const string PathExpression = "$.draw.events[*][eventNumber,description]";

    private byte[] _utf8Json = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _utf8Json = File.ReadAllBytes("example.json");
    }

    [Benchmark(Description = "Pipeline-String", Baseline = true)]
    public byte[] StringPath()
    {
        var json = Encoding.UTF8.GetString(_utf8Json);
        var projected = new JsonFieldSelector(json).Select(PathExpression);

        return Encoding.UTF8.GetBytes(projected);
    }

    [Benchmark(Description = "Pipeline-Bytes")]
    public ReadOnlyMemory<byte> BytePath()
    {
        var projection = new ArrayBufferWriter<byte>();
        new JsonFieldSelector(new ReadOnlyMemory<byte>(_utf8Json)).Select(PathExpression, projection);

        return projection.WrittenMemory;
    }
}
