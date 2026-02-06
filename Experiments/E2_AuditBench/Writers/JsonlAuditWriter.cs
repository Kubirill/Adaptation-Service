using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Experiments.E2_AuditBench;

namespace Experiments.E2_AuditBench.Writers;

internal sealed class JsonlAuditWriter : IAuditWriter
{
    private readonly StreamWriter _writer;
    private readonly Encoding _encoding = Encoding.UTF8;
    private readonly bool _flushPerRecord;
    private readonly int _newlineLength;

    public JsonlAuditWriter(string directory, bool flushPerRecord)
    {
        Directory.CreateDirectory(directory);
        LogFilePath = Path.Combine(directory, "audit_jsonl.jsonl");
        _writer = new StreamWriter(LogFilePath, false, _encoding)
        {
            NewLine = "\n",
            AutoFlush = false
        };

        _flushPerRecord = flushPerRecord;
        _newlineLength = _encoding.GetByteCount(_writer.NewLine);
    }

    public string? LogFilePath { get; }

    public long Write(SessionEventData sessionEvent, DecisionData decision, string configHash)
    {
        var record = new JsonAuditRecord(sessionEvent, decision, configHash);
        var json = JsonSerializer.Serialize(record, JsonDefaults.Canonical);
        _writer.WriteLine(json);
        if (_flushPerRecord)
        {
            _writer.Flush();
        }

        return _encoding.GetByteCount(json) + _newlineLength;
    }

    public void Flush() => _writer.Flush();

    public void Dispose() => _writer.Dispose();

    private sealed class JsonAuditRecord
    {
        public JsonAuditRecord(SessionEventData sessionEvent, DecisionData decision, string configHash)
        {
            correlation_id = sessionEvent.session_id;
            timestamp = DateTime.UtcNow;
            seed = sessionEvent.seed;
            config_version = sessionEvent.config_version;
            config_hash = configHash;
            ek = sessionEvent;
            dk = decision;
            explanation = decision.explanation;
        }

        public string correlation_id { get; }
        public DateTime timestamp { get; }
        public int seed { get; }
        public string config_version { get; }
        public string config_hash { get; }
        public SessionEventData ek { get; }
        public DecisionData dk { get; }
        public List<ExplanationEntryData> explanation { get; }
    }
}