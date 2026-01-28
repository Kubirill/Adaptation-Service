using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using AdaptationCore;

namespace ServiceGrpc
{
    public sealed class ServiceAuditWriter
    {
        private readonly string _auditPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public ServiceAuditWriter(string auditRoot)
        {
            if (string.IsNullOrWhiteSpace(auditRoot))
            {
                _auditPath = string.Empty;
                return;
            }

            Directory.CreateDirectory(auditRoot);
            _auditPath = Path.Combine(auditRoot, "audit.jsonl");
            _jsonOptions = new JsonSerializerOptions
            {
                IncludeFields = true,
                PropertyNamingPolicy = null,
                DictionaryKeyPolicy = null
            };
        }

        public void Append(string sessionId, AdaptationAuditRecord audit)
        {
            if (string.IsNullOrWhiteSpace(_auditPath) || audit == null)
            {
                return;
            }

            var line = new ServiceAuditLine
            {
                session_id = sessionId ?? string.Empty,
                timestamp_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                inputs = audit.inputs,
                output = audit.output,
                config_version_hash = audit.config_version_hash ?? string.Empty,
                seed = audit.seed,
                intermediate = audit.intermediates
            };

            var json = JsonSerializer.Serialize(line, _jsonOptions);
            File.AppendAllText(_auditPath, json + Environment.NewLine, Encoding.UTF8);
        }

        public static string FlattenIntermediates(AdaptationAuditRecord audit)
        {
            if (audit?.intermediates == null || audit.intermediates.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var first = true;
            foreach (var kvp in audit.intermediates)
            {
                if (!first)
                {
                    builder.Append(";");
                }
                first = false;
                builder.Append(kvp.Key);
                builder.Append("=");
                builder.Append(kvp.Value.ToString("0.000", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }
    }

    internal sealed class ServiceAuditLine
    {
        public string session_id;
        public string timestamp_utc;
        public AdaptationEvent inputs;
        public AdaptationDecision output;
        public string config_version_hash;
        public int seed;
        public Dictionary<string, float> intermediate;
    }
}
