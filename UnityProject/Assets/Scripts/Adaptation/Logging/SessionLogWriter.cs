using System;
using System.Globalization;
using System.IO;
using System.Text;
using AdaptationCore;

namespace AdaptationUnity.Logging
{
    public sealed class SessionLogWriter : IDisposable
    {
        private StreamWriter _frameWriter;
        private StreamWriter _adapterWriter;
        private StreamWriter _sceneWriter;
        private StreamWriter _auditWriter;
        private StreamWriter _serviceErrorWriter;
        private StreamWriter _b2BreakdownWriter;
        private readonly object _b2BreakdownLock = new object();
        private string _trialId = string.Empty;

        public void Initialize(string outputDir)
        {
            Directory.CreateDirectory(outputDir);

            _trialId = new DirectoryInfo(outputDir).Name;
            _frameWriter = CreateWriter(Path.Combine(outputDir, "frame_times.csv"), "session_id,session_index,frame_index,warmup,delta_ms");
            _adapterWriter = CreateWriter(Path.Combine(outputDir, "adapter_calls.csv"), "session_id,session_index,warmup,adapter,call_ms");
            _sceneWriter = CreateWriter(Path.Combine(outputDir, "scene_transitions.csv"), "session_id,session_index,warmup,from_scene,to_scene,transition_ms");
            _serviceErrorWriter = CreateWriter(Path.Combine(outputDir, "service_errors.csv"), "session_id,session_index,warmup,attempt,error,call_ms");
            _b2BreakdownWriter = CreateWriter(
                Path.Combine(outputDir, "b2_breakdown.csv"),
                "trial_id,session_index,warmup,correlation_id,t_client_serialize_ms,t_http_rtt_ms,t_server_compute_ms,t_client_deserialize_ms,t_total_client_ms,retries_count,timeout_flag,http_status,error_code"
            );
            _auditWriter = new StreamWriter(Path.Combine(outputDir, "audit.jsonl"), false, Encoding.UTF8)
            {
                AutoFlush = true
            };
        }

        public void LogFrameTime(string sessionId, int sessionIndex, int frameIndex, bool warmup, float deltaSeconds)
        {
            if (_frameWriter == null)
            {
                return;
            }

            var deltaMs = deltaSeconds * 1000f;
            _frameWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4:0.000}", sessionId, sessionIndex, frameIndex, warmup ? 1 : 0, deltaMs));
        }

        public void LogAdapterCall(string sessionId, int sessionIndex, bool warmup, string adapterName, double durationMs)
        {
            if (_adapterWriter == null)
            {
                return;
            }

            _adapterWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4:0.000}", sessionId, sessionIndex, warmup ? 1 : 0, adapterName, durationMs));
        }

        public void LogSceneTransition(string sessionId, int sessionIndex, bool warmup, string fromScene, string toScene, double durationMs)
        {
            if (_sceneWriter == null)
            {
                return;
            }

            _sceneWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5:0.000}", sessionId, sessionIndex, warmup ? 1 : 0, fromScene, toScene, durationMs));
        }

        public void LogServiceError(string sessionId, int sessionIndex, bool warmup, int attempt, string error, double durationMs)
        {
            if (_serviceErrorWriter == null)
            {
                return;
            }

            _serviceErrorWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},\"{4}\",{5:0.000}", sessionId, sessionIndex, warmup ? 1 : 0, attempt, Escape(error), durationMs));
        }

        public void LogAudit(string sessionId, int sessionIndex, bool warmup, AdaptationEvent sessionEvent, AdaptationDecision decision, AdaptationAuditRecord auditRecord)
        {
            if (_auditWriter == null)
            {
                return;
            }

            var line = new StringBuilder(512);
            line.Append("{\"session_id\":\"").Append(Escape(sessionId)).Append("\",");
            line.Append("\"session_index\":").Append(sessionIndex).Append(",");
            line.Append("\"warmup\":").Append(warmup ? "true" : "false").Append(",");
            line.Append("\"timestamp_utc\":\"").Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append("\",");
            line.Append("\"inputs\":").Append(JsonForEvent(sessionEvent)).Append(",");
            line.Append("\"config_version_hash\":\"").Append(Escape(auditRecord?.config_version_hash)).Append("\",");
            line.Append("\"seed\":").Append((auditRecord?.seed ?? sessionEvent.seed).ToString(CultureInfo.InvariantCulture)).Append(",");
            line.Append("\"intermediate\":").Append(JsonForIntermediate(auditRecord)).Append(",");
            line.Append("\"output\":").Append(JsonForDecision(decision)).Append("}");

            _auditWriter.WriteLine(line.ToString());
        }

        public void Dispose()
        {
            _frameWriter?.Dispose();
            _adapterWriter?.Dispose();
            _sceneWriter?.Dispose();
            _serviceErrorWriter?.Dispose();
            _b2BreakdownWriter?.Dispose();
            _auditWriter?.Dispose();
        }

        public void LogB2Breakdown(
            int sessionIndex,
            bool warmup,
            string correlationId,
            double serializeMs,
            double httpMs,
            double serverMs,
            double deserializeMs,
            double totalMs,
            int retriesCount,
            bool timeout,
            int httpStatus,
            string errorCode)
        {
            if (_b2BreakdownWriter == null)
            {
                return;
            }

            lock (_b2BreakdownLock)
            {
                _b2BreakdownWriter.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},\"{3}\",{4:0.000},{5:0.000},{6:0.000},{7:0.000},{8:0.000},{9},{10},{11},\"{12}\"",
                    Escape(_trialId),
                    sessionIndex,
                    warmup ? 1 : 0,
                    Escape(correlationId),
                    serializeMs,
                    httpMs,
                    serverMs,
                    deserializeMs,
                    totalMs,
                    retriesCount,
                    timeout ? 1 : 0,
                    httpStatus,
                    Escape(errorCode)
                ));
            }
        }

        private static StreamWriter CreateWriter(string path, string header)
        {
            var writer = new StreamWriter(path, false, Encoding.UTF8)
            {
                AutoFlush = true
            };
            writer.WriteLine(header);
            return writer;
        }

        private static string JsonForEvent(AdaptationEvent sessionEvent)
        {
            if (sessionEvent == null)
            {
                return "null";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"session_id\":\"{0}\",\"scene_id\":\"{1}\",\"result_z\":{2:0.000},\"time_t\":{3:0.000},\"attempts_a\":{4},\"seed\":{5},\"config_version\":\"{6}\"}}",
                Escape(sessionEvent.session_id),
                Escape(sessionEvent.scene_id),
                sessionEvent.result_z,
                sessionEvent.time_t,
                sessionEvent.attempts_a,
                sessionEvent.seed,
                Escape(sessionEvent.config_version)
            );
        }

        private static string JsonForDecision(AdaptationDecision decision)
        {
            if (decision == null)
            {
                return "null";
            }

            var builder = new StringBuilder(256);
            builder.Append("{\"next_scene_id\":\"").Append(Escape(decision.next_scene_id)).Append("\",");
            builder.Append("\"npc_params\":{");
            var firstParam = true;
            for (var i = 0; i < decision.npc_params.Count; i++)
            {
                var param = decision.npc_params[i];
                if (param == null || string.IsNullOrWhiteSpace(param.name))
                {
                    continue;
                }

                if (!firstParam)
                {
                    builder.Append(",");
                }
                firstParam = false;
                builder.Append("\"").Append(Escape(param.name)).Append("\":");
                builder.Append(param.value.ToString("0.000", CultureInfo.InvariantCulture));
            }
            builder.Append("},");
            builder.Append("\"explanation\":{");
            var firstExplain = true;
            for (var i = 0; i < decision.explanation.Count; i++)
            {
                var entry = decision.explanation[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.name))
                {
                    continue;
                }

                if (!firstExplain)
                {
                    builder.Append(",");
                }
                firstExplain = false;
                builder.Append("\"").Append(Escape(entry.name)).Append("\":");
                builder.Append("\"").Append(Escape(entry.value)).Append("\"");
            }
            builder.Append("},");
            builder.Append("\"seed\":").Append(decision.seed).Append(",");
            builder.Append("\"config_version\":\"").Append(Escape(decision.config_version)).Append("\"");
            builder.Append("}");

            return builder.ToString();
        }

        private static string JsonForIntermediate(AdaptationAuditRecord auditRecord)
        {
            if (auditRecord == null || auditRecord.intermediates == null || auditRecord.intermediates.Count == 0)
            {
                return "{}";
            }

            var builder = new StringBuilder(128);
            builder.Append("{");
            var first = true;
            foreach (var kvp in auditRecord.intermediates)
            {
                if (!first)
                {
                    builder.Append(",");
                }
                first = false;
                builder.Append("\"").Append(Escape(kvp.Key)).Append("\":");
                builder.Append(kvp.Value.ToString("0.000", CultureInfo.InvariantCulture));
            }
            builder.Append("}");
            return builder.ToString();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
