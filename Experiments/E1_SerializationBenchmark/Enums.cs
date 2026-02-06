namespace E1_SerializationBenchmark;

public enum FormatType
{
    Json,
    Protobuf,
    FlatBuffers
}

public enum MessageType
{
    Event,
    Decision
}

public enum OperationType
{
    Serialize,
    Deserialize
}
