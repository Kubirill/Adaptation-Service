using System.Globalization;
using AdaptationCore;

namespace AdaptationUnity.Adapters
{
    public static class AdapterDecisions
    {
        public static AdaptationDecision BuildDecision(AdaptationEvent sessionEvent, string adapterName, string rationale)
        {
            var decision = new AdaptationDecision
            {
                next_scene_id = sessionEvent.scene_id,
                seed = sessionEvent.seed,
                config_version = sessionEvent.config_version
            };

            decision.npc_params.Add(new NpcParam
            {
                name = "aggression",
                value = Clamp01(0.25f + sessionEvent.result_z * 0.5f)
            });
            decision.npc_params.Add(new NpcParam
            {
                name = "curiosity",
                value = Clamp01(0.2f + sessionEvent.time_t * 0.05f)
            });
            decision.npc_params.Add(new NpcParam
            {
                name = "patience",
                value = Clamp01(0.7f - sessionEvent.result_z * 0.3f)
            });

            decision.explanation.Add(new ExplanationEntry
            {
                name = "adapter",
                value = adapterName
            });
            decision.explanation.Add(new ExplanationEntry
            {
                name = "rationale",
                value = rationale
            });
            decision.explanation.Add(new ExplanationEntry
            {
                name = "inputs",
                value = string.Format(CultureInfo.InvariantCulture, "z={0:0.000},t={1:0.000}", sessionEvent.result_z, sessionEvent.time_t)
            });

            return decision;
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
