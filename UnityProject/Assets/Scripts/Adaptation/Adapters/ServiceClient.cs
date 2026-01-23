using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using AdaptationCore;
using AdaptationUnity.Logging;
using UnityEngine;

namespace AdaptationUnity.Adapters
{
    public sealed class ServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly RunConfig _config;
        private SessionLogWriter _logWriter;

        public ServiceClient(RunConfig config)
        {
            _config = config;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(Math.Max(100, _config.ServiceTimeoutMs))
            };
        }

        public void SetLogger(SessionLogWriter logWriter)
        {
            _logWriter = logWriter;
        }

        public AdaptationDecision ComputeNext(AdaptationEvent sessionEvent, out AdaptationAuditRecord auditRecord)
        {
            auditRecord = null;
            var url = _config.ServiceUrl?.TrimEnd('/') + "/computeNext";

            var payload = BuildPayload(sessionEvent, _config.ProfileId);
            var attempts = Math.Max(1, _config.ServiceRetries + 1);
            Exception lastError = null;
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var timer = Stopwatch.StartNew();
                try
                {
                    using var response = _httpClient.PostAsync(url, content).GetAwaiter().GetResult();
                    var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();

                    auditRecord = BuildAudit(sessionEvent, response);
                    return JsonUtility.FromJson<AdaptationDecision>(body);
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    lastError = ex;
                    _logWriter?.LogServiceError(sessionEvent.session_id, attempt, ex.Message, timer.Elapsed.TotalMilliseconds);
                    if (attempt < attempts)
                    {
                        Thread.Sleep(Math.Max(0, _config.ServiceRetryDelayMs));
                    }
                }
            }

            throw lastError ?? new Exception("Service call failed.");
        }

        private static string BuildPayload(AdaptationEvent sessionEvent, string profileId)
        {
            var request = new ComputeNextRequest
            {
                session_id = sessionEvent.session_id,
                scene_id = sessionEvent.scene_id,
                result_z = sessionEvent.result_z,
                time_t = sessionEvent.time_t,
                attempts_a = sessionEvent.attempts_a,
                seed = sessionEvent.seed,
                config_version = sessionEvent.config_version,
                profile_id = profileId ?? string.Empty
            };

            return JsonUtility.ToJson(request);
        }

        private static AdaptationAuditRecord BuildAudit(AdaptationEvent sessionEvent, HttpResponseMessage response)
        {
            var audit = new AdaptationAuditRecord
            {
                inputs = sessionEvent,
                seed = sessionEvent.seed,
                config_version_hash = GetHeader(response, "X-Config-Hash")
            };

            var intermediates = GetHeader(response, "X-Intermediate");
            if (!string.IsNullOrWhiteSpace(intermediates))
            {
                audit.intermediates = ParseIntermediates(intermediates);
            }

            return audit;
        }

        private static string GetHeader(HttpResponseMessage response, string name)
        {
            if (response.Headers.TryGetValues(name, out var values))
            {
                foreach (var value in values)
                {
                    return value;
                }
            }
            return string.Empty;
        }

        private static System.Collections.Generic.Dictionary<string, float> ParseIntermediates(string raw)
        {
            var map = new System.Collections.Generic.Dictionary<string, float>();
            var pairs = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=');
                if (parts.Length != 2)
                {
                    continue;
                }
                if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    map[parts[0]] = value;
                }
            }
            return map;
        }

        [Serializable]
        private sealed class ComputeNextRequest
        {
            public string session_id;
            public string scene_id;
            public float result_z;
            public float time_t;
            public int attempts_a;
            public int seed;
            public string config_version;
            public string profile_id;
        }
    }
}
