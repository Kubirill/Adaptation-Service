using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Experiments.E2_AuditBench.Writers;

internal sealed class OtelAuditWriter : IAuditWriter
{
    private readonly ActivitySource _activitySource = new("AdaptationAudit");
    private readonly JsonlActivityExporter _exporter;
    private readonly TracerProvider _tracerProvider;

    public OtelAuditWriter(string directory, bool flushPerSession)
    {
        Directory.CreateDirectory(directory);
        LogFilePath = Path.Combine(directory, "otel_spans.jsonl");
        _exporter = new JsonlActivityExporter(LogFilePath, flushPerSession);
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("AdaptationAudit")
            .AddProcessor(new SimpleActivityExportProcessor(_exporter))
            .Build();
    }

    public string? LogFilePath { get; }

    public long Write(SessionEventData sessionEvent, DecisionData decision, string configHash)
    {
        using var activity = _activitySource.StartActivity("adaptation.decision", ActivityKind.Internal);
        if (activity is null)
        {
            return 0;
        }

        activity.SetTag("correlation_id", sessionEvent.session_id);
        activity.SetTag("seed", sessionEvent.seed);
        activity.SetTag("config_version", sessionEvent.config_version);
        activity.SetTag("config_hash", configHash);
        activity.SetTag("next_scene_id", decision.next_scene_id);
        activity.SetTag("timestamp", DateTime.UtcNow.ToString("o"));
        activity.SetTag("ek_json", JsonSerializer.Serialize(sessionEvent, JsonDefaults.Canonical));
        activity.SetTag("dk_json", JsonSerializer.Serialize(decision, JsonDefaults.Canonical));
        activity.SetTag("explanation_json", JsonSerializer.Serialize(decision.explanation, JsonDefaults.Canonical));

        activity.Stop();
        return _exporter.ConsumeLastRecordLength();
    }

    public void Flush()
    {
        _exporter.Flush();
    }

    public void Dispose()
    {
        _tracerProvider.Dispose();
        _exporter.CloseWriter();
        _exporter.Dispose();
    }
}

internal sealed class JsonlActivityExporter : BaseExporter<Activity>
{
    private readonly StreamWriter _writer;
    private readonly Encoding _encoding = Encoding.UTF8;
    private readonly bool _flushImmediately;
    private readonly int _newlineLength;
    private long _lastRecordLength;

    public JsonlActivityExporter(string logPath, bool flushImmediately)
    {
        _writer = new StreamWriter(logPath, false, _encoding)
        {
            NewLine = "\n",
            AutoFlush = false
        };

        _flushImmediately = flushImmediately;
        _newlineLength = _encoding.GetByteCount(_writer.NewLine);
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            var record = new ActivityRecord(activity);
            var json = JsonSerializer.Serialize(record, JsonDefaults.Canonical);
            _writer.WriteLine(json);
            if (_flushImmediately)
            {
                _writer.Flush();
            }

            var bytes = _encoding.GetByteCount(json) + _newlineLength;
            Interlocked.Exchange(ref _lastRecordLength, bytes);
        }

        return ExportResult.Success;
    }

    public void Flush()
    {
        _writer.Flush();
    }

    public void CloseWriter()
    {
        _writer.Flush();
        _writer.Dispose();
    }

    public long ConsumeLastRecordLength()
    {
        return Interlocked.Exchange(ref _lastRecordLength, 0);
    }

    private sealed class ActivityRecord
    {
        public ActivityRecord(Activity activity)
        {
            name = activity.DisplayName;
            trace_id = activity.TraceId.ToString();
            span_id = activity.SpanId.ToString();
            start_time = activity.StartTimeUtc;
            end_time = activity.StartTimeUtc.Add(activity.Duration);
            attributes = new Dictionary<string, string>();

            foreach (var tag in activity.Tags)
            {
                attributes[tag.Key] = tag.Value?.ToString() ?? string.Empty;
            }
        }

        public string name { get; }
        public string trace_id { get; }
        public string span_id { get; }
        public DateTime start_time { get; }
        public DateTime end_time { get; }
        public Dictionary<string, string> attributes { get; }
    }
}
