using System;
using System.IO;
using AdaptationCore;
using UnityEngine;

namespace AdaptationUnity.Adapters
{
    public sealed class B3FileRoundTripAdapter : IAdaptationAdapter
    {
        public string AdapterName => "B3";

        public AdaptationDecision ComputeNext(AdaptationEvent sessionEvent)
        {
            var json = JsonUtility.ToJson(sessionEvent);
            var tempPath = Path.Combine(Path.GetTempPath(), "adaptation_event_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(tempPath, json);
                var readJson = File.ReadAllText(tempPath);
                var roundTrip = JsonUtility.FromJson<AdaptationEvent>(readJson);
                return AdapterDecisions.BuildDecision(roundTrip, AdapterName, "file_round_trip");
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }
}
