namespace E1_SerializationBenchmark.Reporting;

public sealed record SummaryRecord(
    FormatType Format,
    MessageType Message,
    OperationType Operation,
    int PayloadBytes,
    double MeanMicroseconds,
    double P50Microseconds,
    double P95Microseconds,
    double P99Microseconds,
    long AllocatedBytes,
    long Gen0Collections,
    long Gen1Collections,
    long Gen2Collections)
{
    public string CsvLine()
    {
        return string.Join(',',
            Format,
            Message,
            Operation,
            PayloadBytes,
            MeanMicroseconds.ToString("F2"),
            P50Microseconds.ToString("F2"),
            P95Microseconds.ToString("F2"),
            P99Microseconds.ToString("F2"),
            AllocatedBytes,
            Gen0Collections,
            Gen1Collections,
            Gen2Collections);
    }

    public static string CsvHeader()
    {
        return "format,message,operation,payload_bytes,mean_us,p50_us,p95_us,p99_us,alloc_bytes,gen0,gen1,gen2";
    }
}
