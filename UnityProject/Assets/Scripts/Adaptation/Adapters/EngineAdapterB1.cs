using System;
using System.IO;
using AdaptationCore;
using UnityEngine;

namespace AdaptationUnity.Adapters
{
    public sealed class EngineAdapterB1 : IAdaptationAdapterWithAudit
    {
        public string AdapterName => "B1";

        private readonly ConfigPackage _config;

        public EngineAdapterB1()
        {
            var configVersion = RunConfig.Current.ConfigVersion;
            var configRoot = ResolveConfigRoot();
            _config = ConfigLoader.LoadByVersion(configRoot, configVersion);
        }

        public AdaptationDecision ComputeNext(AdaptationEvent sessionEvent)
        {
            return ComputeNext(sessionEvent, out _);
        }

        public AdaptationDecision ComputeNext(AdaptationEvent sessionEvent, out AdaptationAuditRecord auditRecord)
        {
            var eventJson = JsonUtility.ToJson(sessionEvent);
            var mappedEvent = JsonUtility.FromJson<AdaptationEvent>(eventJson);

            var result = AdaptationEngine.ComputeNext(mappedEvent, _config);
            var decisionJson = JsonUtility.ToJson(result.Decision);
            var mappedDecision = JsonUtility.FromJson<AdaptationDecision>(decisionJson);

            auditRecord = result.Audit;
            return mappedDecision;
        }

        private static string ResolveConfigRoot()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var configRoot = Path.Combine(repoRoot, "Configs");
            if (!Directory.Exists(configRoot))
            {
                throw new DirectoryNotFoundException($"Config root not found: {configRoot}");
            }
            return configRoot;
        }
    }
}
