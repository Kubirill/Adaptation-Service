using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using AdaptationCore;
using AdaptationGrpc;
using Grpc.Core;

namespace ServiceGrpc
{
    public sealed class AdaptationGrpcService : Adaptation.AdaptationBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null
        };

        private readonly ConfigPackage _config;
        private readonly ServiceAuditWriter _auditWriter;

        public AdaptationGrpcService(ConfigPackage config, ServiceAuditWriter auditWriter)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
        }

        public override Task<AdaptationGrpc.AdaptationDecision> ComputeNext(SessionResult request, ServerCallContext context)
        {
            var sessionEvent = new AdaptationEvent
            {
                session_id = request.SessionId ?? string.Empty,
                scene_id = request.SceneId ?? string.Empty,
                result_z = request.ResultZ,
                time_t = request.TimeT,
                attempts_a = request.Attempts,
                seed = request.Seed,
                config_version = request.ConfigVersion ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(sessionEvent.config_version))
            {
                sessionEvent.config_version = _config.version;
            }

            var correlationId = ResolveCorrelationId(request, context);

            var computeTimer = Stopwatch.StartNew();
            try
            {
                var result = AdaptationEngine.ComputeNext(sessionEvent, _config);
                computeTimer.Stop();

                _auditWriter.Append(sessionEvent.session_id, result.Audit);

                var response = BuildResponse(result.Decision, correlationId);
                response.ConfigVersion = sessionEvent.config_version ?? string.Empty;

                WriteTrailers(context, correlationId, computeTimer.Elapsed.TotalMilliseconds, result.Audit);
                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                computeTimer.Stop();
                WriteTrailers(context, correlationId, computeTimer.Elapsed.TotalMilliseconds, null);
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        private static AdaptationGrpc.AdaptationDecision BuildResponse(AdaptationCore.AdaptationDecision decision, string correlationId)
        {
            var response = new AdaptationGrpc.AdaptationDecision
            {
                NextSceneId = decision?.next_scene_id ?? string.Empty,
                Seed = decision?.seed ?? 0,
                ConfigVersion = decision?.config_version ?? string.Empty,
                CorrelationId = correlationId ?? string.Empty,
                NpcParamsJson = BuildNpcParamsJson(decision),
                ExplanationJson = BuildExplanationJson(decision)
            };

            if (decision?.npc_params != null)
            {
                foreach (var param in decision.npc_params)
                {
                    if (param == null)
                    {
                        continue;
                    }
                    response.NpcParams.Add(new AdaptationGrpc.NpcParam
                    {
                        Name = param.name ?? string.Empty,
                        Value = param.value
                    });
                }
            }

            if (decision?.explanation != null)
            {
                foreach (var entry in decision.explanation)
                {
                    if (entry == null)
                    {
                        continue;
                    }
                    response.Explanation.Add(new AdaptationGrpc.ExplanationEntry
                    {
                        Name = entry.name ?? string.Empty,
                        Value = entry.value ?? string.Empty
                    });
                }
            }

            return response;
        }

        private static string BuildNpcParamsJson(AdaptationCore.AdaptationDecision decision)
        {
            if (decision?.npc_params == null || decision.npc_params.Count == 0)
            {
                return "{}";
            }

            var map = new Dictionary<string, float>();
            foreach (var param in decision.npc_params)
            {
                if (param == null || string.IsNullOrWhiteSpace(param.name))
                {
                    continue;
                }
                map[param.name] = param.value;
            }

            return JsonSerializer.Serialize(map, JsonOptions);
        }

        private static string BuildExplanationJson(AdaptationCore.AdaptationDecision decision)
        {
            if (decision?.explanation == null || decision.explanation.Count == 0)
            {
                return "{}";
            }

            var map = new Dictionary<string, string>();
            foreach (var entry in decision.explanation)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.name))
                {
                    continue;
                }
                map[entry.name] = entry.value ?? string.Empty;
            }

            return JsonSerializer.Serialize(map, JsonOptions);
        }

        private static string ResolveCorrelationId(SessionResult request, ServerCallContext context)
        {
            if (!string.IsNullOrWhiteSpace(request.CorrelationId))
            {
                return request.CorrelationId;
            }

            if (context.RequestHeaders != null)
            {
                foreach (var entry in context.RequestHeaders)
                {
                    if (entry.Key.Equals("x-correlation-id", StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value ?? string.Empty;
                    }
                }
            }

            return Guid.NewGuid().ToString("N");
        }

        private static void WriteTrailers(ServerCallContext context, string correlationId, double computeMs, AdaptationAuditRecord audit)
        {
            context.ResponseTrailers.Add("x-server-compute-ms", computeMs.ToString("0.000", CultureInfo.InvariantCulture));
            context.ResponseTrailers.Add("x-correlation-id", correlationId ?? string.Empty);

            if (audit != null)
            {
                context.ResponseTrailers.Add("x-config-hash", audit.config_version_hash ?? string.Empty);
                context.ResponseTrailers.Add("x-intermediate", ServiceAuditWriter.FlattenIntermediates(audit));
            }
        }
    }
}
