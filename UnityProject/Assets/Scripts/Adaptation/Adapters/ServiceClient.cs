using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using AdaptationCore;
using AdaptationUnity.Logging;
using UnityEngine;

namespace AdaptationUnity.Adapters
{
    public sealed class ServiceClient : IAdapterTiming
    {
        private static readonly HttpClient SharedClient = CreateSharedClient();
        private readonly HttpClient _httpClient;
        private readonly RunConfig _config;
        private SessionLogWriter _logWriter;
        private string _sessionId = string.Empty;
        private int _sessionIndex;
        private bool _warmup;
        private double _lastNetMs;
        private double _lastLocalMs;
        private double _lastDecisionMs;
        private bool _hasTiming;

        public ServiceClient(RunConfig config)
        {
            _config = config;
            _httpClient = SharedClient;
        }

        public void SetLogger(SessionLogWriter logWriter)
        {
            _logWriter = logWriter;
        }

        public void SetSessionContext(string sessionId, int sessionIndex, bool warmup)
        {
            _sessionId = sessionId ?? string.Empty;
            _sessionIndex = sessionIndex;
            _warmup = warmup;
        }

        public AdaptationDecision ComputeNext(AdaptationEvent sessionEvent, out AdaptationAuditRecord auditRecord)
        {
            auditRecord = null;
            var url = _config.ServiceUrl?.TrimEnd('/') + "/computeNext";

            var correlationId = Guid.NewGuid().ToString("N");
            var serializeTimer = Stopwatch.StartNew();
            var payload = BuildPayload(sessionEvent, _config.ProfileId);
            serializeTimer.Stop();

            var totalTimer = Stopwatch.StartNew();
            var httpMs = 0.0;
            var deserializeMs = 0.0;
            var serverMs = 0.0;
            var retriesCount = 0;
            var timeoutFlag = false;
            var httpStatus = 0;
            var errorCode = string.Empty;

            var attempts = Math.Max(1, _config.ServiceRetries + 1);
            Exception lastError = null;
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                request.Headers.Add("X-Correlation-Id", correlationId);

                var timer = Stopwatch.StartNew();
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Max(100, _config.ServiceTimeoutMs)));
                    using var response = _httpClient.SendAsync(request, cts.Token).GetAwaiter().GetResult();
                    var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    timer.Stop();
                    httpMs = timer.Elapsed.TotalMilliseconds;
                    httpStatus = (int)response.StatusCode;
                    serverMs = ParseServerMs(response);

                    if (!response.IsSuccessStatusCode)
                    {
                        errorCode = "HTTP_" + httpStatus.ToString(CultureInfo.InvariantCulture);
                        throw new HttpRequestException("Service returned status " + httpStatus.ToString(CultureInfo.InvariantCulture));
                    }

                    var deserializeTimer = Stopwatch.StartNew();
                    var decision = JsonUtility.FromJson<AdaptationDecision>(body);
                    deserializeTimer.Stop();
                    deserializeMs = deserializeTimer.Elapsed.TotalMilliseconds;
                    var auditTimer = Stopwatch.StartNew();
                    auditRecord = BuildAudit(sessionEvent, response);
                    auditTimer.Stop();
                    var localMs = serializeTimer.Elapsed.TotalMilliseconds + deserializeMs + auditTimer.Elapsed.TotalMilliseconds;
                    StoreTiming(httpMs, localMs, _lastDecisionMs);

                    retriesCount = attempt - 1;
                    totalTimer.Stop();
                    _logWriter?.LogB2Breakdown(
                        _sessionIndex,
                        _warmup,
                        correlationId,
                        serializeTimer.Elapsed.TotalMilliseconds,
                        httpMs,
                        serverMs,
                        deserializeMs,
                        totalTimer.Elapsed.TotalMilliseconds,
                        retriesCount,
                        timeoutFlag,
                        httpStatus,
                        errorCode
                    );
                    return decision;
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    lastError = ex;
                    httpMs = timer.Elapsed.TotalMilliseconds;
                    if (ex is OperationCanceledException)
                    {
                        timeoutFlag = true;
                        errorCode = "Timeout";
                    }
                    else
                    {
                        errorCode = ex.GetType().Name;
                    }
                    _logWriter?.LogServiceError(sessionEvent.session_id, _sessionIndex, _warmup, attempt, ex.Message, timer.Elapsed.TotalMilliseconds);
                    if (attempt < attempts)
                    {
                        Thread.Sleep(Math.Max(0, _config.ServiceRetryDelayMs));
                    }
                    retriesCount = attempt - 1;
                }
            }

            totalTimer.Stop();
            _logWriter?.LogB2Breakdown(
                _sessionIndex,
                _warmup,
                correlationId,
                serializeTimer.Elapsed.TotalMilliseconds,
                httpMs,
                serverMs,
                deserializeMs,
                totalTimer.Elapsed.TotalMilliseconds,
                Math.Max(0, retriesCount),
                timeoutFlag,
                httpStatus,
                errorCode
            );
            throw lastError ?? new Exception("Service call failed.");
        }

        private void StoreTiming(double netMs, double localMs, double decisionMs)
        {
            _lastNetMs = netMs;
            _lastLocalMs = localMs;
            _lastDecisionMs = decisionMs;
            _hasTiming = true;
        }

        public bool TryGetLastTiming(out double netMs, out double localMs, out double decisionMs)
        {
            if (_hasTiming)
            {
                netMs = _lastNetMs;
                localMs = _lastLocalMs;
                decisionMs = _lastDecisionMs;
                _hasTiming = false;
                return true;
            }

            netMs = 0;
            localMs = 0;
            decisionMs = 0;
            return false;
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

        private static double ParseServerMs(HttpResponseMessage response)
        {
            var header = GetHeader(response, "X-Server-Compute-Ms");
            if (double.TryParse(header, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
            return 0.0;
        }

        private static HttpClient CreateSharedClient()
        {
            var client = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            return client;
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
