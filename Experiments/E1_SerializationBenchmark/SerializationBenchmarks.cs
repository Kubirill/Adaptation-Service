using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace E1_SerializationBenchmark;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 5, iterationCount: 12, launchCount: 1)]
public class SerializationBenchmarks
{
    private readonly Serialization.SerializationFixture _fixture = Serialization.SerializationFixture.Instance;

    [ParamsAllValues]
    public FormatType Format { get; set; }

    [ParamsAllValues]
    public MessageType Message { get; set; }

    [ParamsAllValues]
    public OperationType Operation { get; set; }

    [Benchmark]
    public object Operate()
    {
        return Operation switch
        {
            OperationType.Serialize => _fixture.Serialize(Format, Message),
            OperationType.Deserialize => _fixture.Deserialize(Format, Message),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
