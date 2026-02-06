using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Experiments.E2_AuditBench.Writers;

internal sealed class ProvAuditWriter : IAuditWriter
{
    private readonly StreamWriter _writer;
    private readonly Encoding _encoding = Encoding.UTF8;
    private readonly bool _flushPerRecord;
    private readonly int _newlineLength;

    public ProvAuditWriter(string directory, bool flushPerRecord)
    {
        Directory.CreateDirectory(directory);
        LogFilePath = Path.Combine(directory, "prov.jsonl");
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
        var record = new ProvRecord(sessionEvent, decision, configHash);
        var json = JsonSerializer.Serialize(record, JsonDefaults.Canonical);
        _writer.WriteLine(json);
        if (_flushPerRecord)
        {
            _writer.Flush();
        }

        return _encoding.GetByteCount(json) + _newlineLength;
    }

    public void Flush()
    {
        _writer.Flush();
    }

    public void Dispose()
    {
        _writer.Dispose();
    }

    private sealed class ProvRecord
    {
        public ProvRecord(SessionEventData ek, DecisionData dk, string configHash)
        {
            correlation_id = ek.session_id;
            ek_entity = new ProvEntity($"ek:{correlation_id}", new Dictionary<string, object>
            {
                ["ek"] = ek,
                ["ek_json"] = JsonSerializer.Serialize(ek, JsonDefaults.Canonical),
                ["config_version"] = ek.config_version,
                ["seed"] = ek.seed,
                ["config_hash"] = configHash
            });

            dk_entity = new ProvEntity($"dk:{correlation_id}", new Dictionary<string, object>
            {
                ["dk"] = dk,
                ["dk_json"] = JsonSerializer.Serialize(dk, JsonDefaults.Canonical),
                ["explanation_json"] = JsonSerializer.Serialize(dk.explanation, JsonDefaults.Canonical)
            });

            activity = new ProvActivity($"act:{correlation_id}", DateTime.UtcNow, DateTime.UtcNow, "ComputeNext");
            agent = new ProvAgent("adaptation_module", "system");
            relations = new List<ProvRelation>
            {
                new("used", activity.id, ek_entity.id, agent.id),
                new("wasGeneratedBy", activity.id, dk_entity.id, agent.id),
                new("wasAssociatedWith", activity.id, agent.id, agent.id)
            };
        }

        public string correlation_id { get; }
        public ProvEntity ek_entity { get; }
        public ProvEntity dk_entity { get; }
        public ProvActivity activity { get; }
        public ProvAgent agent { get; }
        public List<ProvRelation> relations { get; }
    }

    private sealed class ProvEntity
    {
        public ProvEntity(string id, Dictionary<string, object> attributes)
        {
            this.id = id;
            this.attributes = attributes;
        }

        public string id { get; }
        public Dictionary<string, object> attributes { get; }
    }

    private sealed class ProvActivity
    {
        public ProvActivity(string id, DateTime start, DateTime end, string name)
        {
            this.id = id;
            start_time = start;
            end_time = end;
            activity_name = name;
        }

        public string id { get; }
        public DateTime start_time { get; }
        public DateTime end_time { get; }
        public string activity_name { get; }
    }

    private sealed class ProvAgent
    {
        public ProvAgent(string id, string type)
        {
            this.id = id;
            agent_type = type;
        }

        public string id { get; }
        public string agent_type { get; }
    }

    private sealed class ProvRelation
    {
        public ProvRelation(string relationType, string activityId, string entityId, string agentId)
        {
            type = relationType;
            activity_id = activityId;
            entity_id = entityId;
            agent_id = agentId;
        }

        public string type { get; }
        public string activity_id { get; }
        public string entity_id { get; }
        public string agent_id { get; }
    }
}