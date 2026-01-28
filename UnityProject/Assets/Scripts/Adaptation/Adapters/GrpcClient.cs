using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using AdaptationCore;
using AdaptationUnity.Logging;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;

namespace AdaptationUnity.Adapters
{
    public sealed class GrpcClient
    {
        private static readonly object ChannelLock = new object();
        private static GrpcChannel SharedChannel;
        private static AdaptationGrpc.Adaptation.AdaptationClient SharedClient;
        private static string SharedAddress;

        private readonly RunConfig _config;
        private SessionLogWriter _logWriter;
        private string _sessionId = string.Empty;
        private int _sessionIndex;
        private bool _warmup;

        public GrpcClient(RunConfig config)
        {
            _config = config;
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
            var correlationId = Guid.NewGuid().ToString("N");

            var serializeTimer = Stopwatch.StartNew();
            var request = BuildRequest(sessionEvent, correlationId);
            serializeTimer.Stop();

            var totalTimer = Stopwatch.StartNew();
            var grpcMs = 0.0;
            var deserializeMs = 0.0;
            var serverMs = 0.0;
            var retriesCount = 0;
            var timeoutFlag = false;
            var grpcStatus = string.Empty;
            var errorCode = string.Empty;

            var attempts = Math.Max(1, _config.ServiceRetries + 1);
            Exception lastError = null;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                var headers = new Metadata
                {
                    { "x-correlation-id", correlationId }
                };

                var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(100, _config.ServiceTimeoutMs));
                var timer = Stopwatch.StartNew();
                try
                {
                    var client = GetClient(_config.ServiceGrpcUrl);
                    var call = client.ComputeNextAsync(request, new CallOptions(headers: headers, deadline: deadline));
                    var response = call.ResponseAsync.GetAwaiter().GetResult();
                    timer.Stop();
                    grpcMs = timer.Elapsed.TotalMilliseconds;

                    var trailers = call.GetTrailers();
                    serverMs = ParseServerMs(trailers);
                    var responseCorrelationId = GetMetadataValue(trailers, "x-correlation-id");
                    if (!string.IsNullOrWhiteSpace(responseCorrelationId))
                    {
                        correlationId = responseCorrelationId;
                    }

                    var deserializeTimer = Stopwatch.StartNew();
                    var decision = BuildDecision(response, sessionEvent);
                    deserializeTimer.Stop();
                    deserializeMs = deserializeTimer.Elapsed.TotalMilliseconds;
                    auditRecord = BuildAudit(sessionEvent, decision, trailers);

                    retriesCount = attempt - 1;
                    totalTimer.Stop();
                    grpcStatus = StatusCode.OK.ToString();
                    _logWriter?.LogR3GrpcBreakdown(
                        _sessionIndex,
                        _warmup,
                        correlationId,
                        serializeTimer.Elapsed.TotalMilliseconds,
                        grpcMs,
                        serverMs,
                        deserializeMs,
                        totalTimer.Elapsed.TotalMilliseconds,
                        retriesCount,
                        timeoutFlag,
                        grpcStatus,
                        errorCode
                    );
                    return decision;
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    lastError = ex;
                    grpcMs = timer.Elapsed.TotalMilliseconds;

                    if (ex is RpcException rpcEx)
                    {
                        grpcStatus = rpcEx.StatusCode.ToString();
                        if (rpcEx.StatusCode == StatusCode.DeadlineExceeded)
                        {
                            timeoutFlag = true;
                            errorCode = "DeadlineExceeded";
                        }
                        else
                        {
                            errorCode = rpcEx.StatusCode.ToString();
                        }
                    }
                    else if (ex is OperationCanceledException)
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
            _logWriter?.LogR3GrpcBreakdown(
                _sessionIndex,
                _warmup,
                correlationId,
                serializeTimer.Elapsed.TotalMilliseconds,
                grpcMs,
                serverMs,
                deserializeMs,
                totalTimer.Elapsed.TotalMilliseconds,
                Math.Max(0, retriesCount),
                timeoutFlag,
                grpcStatus,
                errorCode
            );
            throw lastError ?? new Exception("gRPC call failed.");
        }

        private static AdaptationGrpc.SessionResult BuildRequest(AdaptationEvent sessionEvent, string correlationId)
        {
            return new AdaptationGrpc.SessionResult
            {
                SessionId = sessionEvent.session_id ?? string.Empty,
                SceneId = sessionEvent.scene_id ?? string.Empty,
                ResultZ = sessionEvent.result_z,
                TimeT = sessionEvent.time_t,
                Attempts = sessionEvent.attempts_a,
                Seed = sessionEvent.seed,
                ConfigVersion = sessionEvent.config_version ?? string.Empty,
                CorrelationId = correlationId ?? string.Empty
            };
        }

        private static AdaptationDecision BuildDecision(AdaptationGrpc.AdaptationDecision response, AdaptationEvent sessionEvent)
        {
            var decision = new AdaptationDecision
            {
                next_scene_id = response.NextSceneId,
                seed = response.Seed,
                config_version = response.ConfigVersion
            };

            if (response.NpcParams != null && response.NpcParams.Count > 0)
            {
                foreach (var param in response.NpcParams)
                {
                    decision.npc_params.Add(new NpcParam
                    {
                        name = param.Name,
                        value = param.Value
                    });
                }
            }
            else
            {
                var map = ParseFloatMap(response.NpcParamsJson);
                foreach (var kvp in map)
                {
                    decision.npc_params.Add(new NpcParam
                    {
                        name = kvp.Key,
                        value = kvp.Value
                    });
                }
            }

            if (response.Explanation != null && response.Explanation.Count > 0)
            {
                foreach (var entry in response.Explanation)
                {
                    decision.explanation.Add(new ExplanationEntry
                    {
                        name = entry.Name,
                        value = entry.Value
                    });
                }
            }
            else
            {
                var map = ParseStringMap(response.ExplanationJson);
                foreach (var kvp in map)
                {
                    decision.explanation.Add(new ExplanationEntry
                    {
                        name = kvp.Key,
                        value = kvp.Value
                    });
                }
            }

            if (string.IsNullOrWhiteSpace(decision.next_scene_id))
            {
                decision.next_scene_id = sessionEvent.scene_id;
            }

            return decision;
        }

        private static AdaptationAuditRecord BuildAudit(AdaptationEvent sessionEvent, AdaptationDecision decision, Metadata trailers)
        {
            var audit = new AdaptationAuditRecord
            {
                inputs = sessionEvent,
                output = decision,
                seed = sessionEvent.seed,
                config_version_hash = GetMetadataValue(trailers, "x-config-hash")
            };

            var intermediates = GetMetadataValue(trailers, "x-intermediate");
            if (!string.IsNullOrWhiteSpace(intermediates))
            {
                audit.intermediates = ParseIntermediates(intermediates);
            }

            return audit;
        }

        private static double ParseServerMs(Metadata trailers)
        {
            var value = GetMetadataValue(trailers, "x-server-compute-ms");
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
            return 0.0;
        }

        private static string GetMetadataValue(Metadata metadata, string key)
        {
            if (metadata == null)
            {
                return string.Empty;
            }

            foreach (var entry in metadata)
            {
                if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value ?? string.Empty;
                }
            }
            return string.Empty;
        }

        private static Grpc.Net.Client.GrpcChannel GetChannel(string address)
        {
            lock (ChannelLock)
            {
                if (SharedChannel != null && string.Equals(SharedAddress, address, StringComparison.OrdinalIgnoreCase))
                {
                    return SharedChannel;
                }

                var handler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler())
                {
                    HttpVersion = System.Net.HttpVersion.Version11
                };
                var channel = Grpc.Net.Client.GrpcChannel.ForAddress(address, new GrpcChannelOptions
                {
                    HttpHandler = handler
                });

                SharedChannel = channel;
                SharedAddress = address;
                SharedClient = new AdaptationGrpc.Adaptation.AdaptationClient(channel);
                return channel;
            }
        }

        private static AdaptationGrpc.Adaptation.AdaptationClient GetClient(string address)
        {
            GetChannel(address);
            return SharedClient;
        }

        private static Dictionary<string, float> ParseFloatMap(string json)
        {
            var map = new Dictionary<string, float>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return map;
            }

            var trimmed = json.Trim();
            if (trimmed.Length < 2)
            {
                return map;
            }

            if (trimmed[0] == '{')
            {
                trimmed = trimmed.Substring(1);
            }
            if (trimmed.EndsWith("}"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }

            var pairs = SplitTopLevel(trimmed);
            foreach (var pair in pairs)
            {
                var index = pair.IndexOf(':');
                if (index <= 0)
                {
                    continue;
                }

                var key = Unquote(pair.Substring(0, index).Trim());
                var valueText = pair.Substring(index + 1).Trim();
                if (float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    map[key] = value;
                }
            }

            return map;
        }

        private static Dictionary<string, string> ParseStringMap(string json)
        {
            var map = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return map;
            }

            var trimmed = json.Trim();
            if (trimmed.Length < 2)
            {
                return map;
            }

            if (trimmed[0] == '{')
            {
                trimmed = trimmed.Substring(1);
            }
            if (trimmed.EndsWith("}"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }

            var pairs = SplitTopLevel(trimmed);
            foreach (var pair in pairs)
            {
                var index = pair.IndexOf(':');
                if (index <= 0)
                {
                    continue;
                }

                var key = Unquote(pair.Substring(0, index).Trim());
                var valueText = pair.Substring(index + 1).Trim();
                map[key] = Unquote(valueText);
            }

            return map;
        }

        private static List<string> SplitTopLevel(string raw)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return results;
            }

            var start = 0;
            var inString = false;
            for (var i = 0; i < raw.Length; i++)
            {
                var ch = raw[i];
                if (ch == '"' && (i == 0 || raw[i - 1] != '\\'))
                {
                    inString = !inString;
                }
                else if (ch == ',' && !inString)
                {
                    results.Add(raw.Substring(start, i - start));
                    start = i + 1;
                }
            }

            if (start < raw.Length)
            {
                results.Add(raw.Substring(start));
            }

            return results;
        }

        private static string Unquote(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2);
            }

            return trimmed.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        private static Dictionary<string, float> ParseIntermediates(string raw)
        {
            var map = new Dictionary<string, float>();
            var pairs = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=');
                if (parts.Length != 2)
                {
                    continue;
                }
                if (float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    map[parts[0]] = value;
                }
            }
            return map;
        }
    }
}
