using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using AdaptationCore;
using AdaptationUnity.Adapters;
using AdaptationUnity.Logging;
using AdaptationUnity.Npc;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace AdaptationUnity
{
    public sealed class SessionRunner : MonoBehaviour
    {
        private static SessionRunner _instance;

        private static readonly ProfilerMarker BuildEventMarker = new ProfilerMarker("BuildEvent");
        private static readonly ProfilerMarker AdapterCallMarker = new ProfilerMarker("AdapterCall");
        private static readonly ProfilerMarker ApplyDecisionMarker = new ProfilerMarker("ApplyDecision");
        private static readonly ProfilerMarker SceneLoadMarker = new ProfilerMarker("SceneLoad");

        private IAdaptationAdapter _adapter;
        private SessionLogWriter _logWriter;
        private DummyNpcController _npcController;
        private RunConfig _config;

        private int _sessionIndex;
        private int _frameIndex;
        private float _sessionStart;
        private string _currentSessionId;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            RunConfig.ApplyFromArgs(Environment.GetCommandLineArgs());
            _config = RunConfig.Current;
        }

        private void Start()
        {
            _adapter = AdapterFactory.Create(_config.AdapterName);
            _logWriter = new SessionLogWriter();
            _logWriter.Initialize(ResolveOutputDirectory());
            StartCoroutine(RunSessions());
        }

        private IEnumerator RunSessions()
        {
            for (_sessionIndex = 0; _sessionIndex < _config.Sessions; _sessionIndex++)
            {
                yield return StartCoroutine(RunSingleSession(_sessionIndex));
            }

            _logWriter.Dispose();

            if (Application.isBatchMode)
            {
                Application.Quit(0);
            }
        }

        private IEnumerator RunSingleSession(int index)
        {
            _currentSessionId = $"session_{index:0000}";
            _frameIndex = 0;
            _sessionStart = Time.realtimeSinceStartup;

            var sceneName = SceneManager.GetActiveScene().name;
            var sessionSeed = _config.Seed + index;
            UnityEngine.Random.InitState(sessionSeed);

            while (Time.realtimeSinceStartup - _sessionStart < _config.SessionDurationSeconds && _frameIndex < _config.MaxFrames)
            {
                _logWriter.LogFrameTime(_currentSessionId, _frameIndex, Time.deltaTime);
                _frameIndex++;
                yield return null;
            }

            AdaptationEvent sessionEvent;
            using (BuildEventMarker.Auto())
            {
                sessionEvent = new AdaptationEvent
                {
                    session_id = _currentSessionId,
                    scene_id = sceneName,
                    result_z = UnityEngine.Random.value,
                    time_t = Time.realtimeSinceStartup - _sessionStart,
                    attempts_a = _config.Attempts,
                    seed = sessionSeed,
                    config_version = _config.ConfigVersion
                };
            }

            AdaptationDecision decision;
            var adapterTimer = Stopwatch.StartNew();
            using (AdapterCallMarker.Auto())
            {
                decision = _adapter.ComputeNext(sessionEvent);
            }
            adapterTimer.Stop();
            _logWriter.LogAdapterCall(_currentSessionId, _adapter.AdapterName, adapterTimer.Elapsed.TotalMilliseconds);

            using (ApplyDecisionMarker.Auto())
            {
                EnsureNpc();
                _npcController.ApplyParams(decision.npc_params);
            }

            _logWriter.LogAudit(_currentSessionId, sessionEvent, decision);

            var nextScene = ResolveNextScene(decision.next_scene_id);
            var transitionTimer = Stopwatch.StartNew();
            using (SceneLoadMarker.Auto())
            {
                var load = SceneManager.LoadSceneAsync(nextScene);
                while (!load.isDone)
                {
                    yield return null;
                }
            }
            transitionTimer.Stop();
            _logWriter.LogSceneTransition(_currentSessionId, sceneName, nextScene, transitionTimer.Elapsed.TotalMilliseconds);
        }

        private void EnsureNpc()
        {
            if (_npcController != null)
            {
                return;
            }

            _npcController = FindObjectOfType<DummyNpcController>();
            if (_npcController == null)
            {
                var npcObject = new GameObject("DummyNpc");
                _npcController = npcObject.AddComponent<DummyNpcController>();
            }
        }

        private string ResolveNextScene(string requestedScene)
        {
            if (!string.IsNullOrWhiteSpace(requestedScene))
            {
                return requestedScene;
            }

            var activeScene = SceneManager.GetActiveScene().name;
            if (_config.SceneSequence.Count == 0)
            {
                return activeScene;
            }

            var index = _config.SceneSequence.IndexOf(activeScene);
            if (index < 0)
            {
                return _config.SceneSequence[0];
            }

            var nextIndex = (index + 1) % _config.SceneSequence.Count;
            return _config.SceneSequence[nextIndex];
        }

        private string ResolveOutputDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_config.OutputDirectory))
            {
                Directory.CreateDirectory(_config.OutputDirectory);
                return _config.OutputDirectory;
            }

            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var outputDir = Path.Combine(repoRoot, "Experiments", "out", _config.AdapterName, timestamp);
            Directory.CreateDirectory(outputDir);
            return outputDir;
        }
    }
}
