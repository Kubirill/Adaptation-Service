using System.Collections.Generic;
using AdaptationCore;
using UnityEngine;

namespace AdaptationUnity.Npc
{
    public sealed class DummyNpcController : MonoBehaviour
    {
        [Header("Debug Params")]
        public float aggression;
        public float curiosity;
        public float patience;

        private readonly Dictionary<string, float> _paramMap = new Dictionary<string, float>();

        public void ApplyParams(List<NpcParam> parameters)
        {
            _paramMap.Clear();
            foreach (var param in parameters)
            {
                if (param == null || string.IsNullOrWhiteSpace(param.name))
                {
                    continue;
                }

                _paramMap[param.name] = param.value;
                if (param.name == "aggression")
                {
                    aggression = param.value;
                }
                else if (param.name == "curiosity")
                {
                    curiosity = param.value;
                }
                else if (param.name == "patience")
                {
                    patience = param.value;
                }
            }
        }
    }
}
