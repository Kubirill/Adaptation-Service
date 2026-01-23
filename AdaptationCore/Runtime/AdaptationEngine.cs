using System;
using System.Collections.Generic;

namespace AdaptationCore
{
    public static class AdaptationEngine
    {
        public static AdaptationResult ComputeNext(AdaptationEvent sessionEvent, ConfigPackage config)
        {
            if (sessionEvent == null)
            {
                throw new ArgumentNullException(nameof(sessionEvent));
            }
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var timeFactor = sessionEvent.time_t * config.time_scale;
            var score = (sessionEvent.result_z * config.result_weight) + (timeFactor * config.time_weight);

            var baseAggression = GetParam(config.npc_base_params, "aggression", 0.2f);
            var baseCuriosity = GetParam(config.npc_base_params, "curiosity", 0.3f);
            var basePatience = GetParam(config.npc_base_params, "patience", 0.6f);

            var aggression = Clamp01(baseAggression + score);
            var curiosity = Clamp01(baseCuriosity + timeFactor);
            var patience = Clamp01(basePatience - score * 0.5f);

            var decision = new AdaptationDecision
            {
                next_scene_id = sessionEvent.scene_id,
                seed = sessionEvent.seed,
                config_version = sessionEvent.config_version
            };

            decision.npc_params.Add(new NpcParam { name = "aggression", value = aggression });
            decision.npc_params.Add(new NpcParam { name = "curiosity", value = curiosity });
            decision.npc_params.Add(new NpcParam { name = "patience", value = patience });

            decision.explanation.Add(new ExplanationEntry { name = "adapter", value = "B1" });
            decision.explanation.Add(new ExplanationEntry { name = "config_version", value = sessionEvent.config_version });
            decision.explanation.Add(new ExplanationEntry { name = "config_hash", value = config.version_hash });
            decision.explanation.Add(new ExplanationEntry { name = "score", value = score.ToString("0.000") });

            var audit = new AdaptationAuditRecord
            {
                inputs = sessionEvent,
                output = decision,
                config_version_hash = config.version_hash,
                seed = sessionEvent.seed,
                intermediates = new Dictionary<string, float>
                {
                    { "time_factor", timeFactor },
                    { "score", score },
                    { "base_aggression", baseAggression },
                    { "base_curiosity", baseCuriosity },
                    { "base_patience", basePatience }
                }
            };

            return new AdaptationResult
            {
                Decision = decision,
                Audit = audit
            };
        }

        private static float GetParam(Dictionary<string, float> map, string key, float fallback)
        {
            if (map != null && map.TryGetValue(key, out var value))
            {
                return value;
            }
            return fallback;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }
            if (value > 1f)
            {
                return 1f;
            }
            return value;
        }
    }
}
