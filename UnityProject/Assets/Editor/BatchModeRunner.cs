using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AdaptationUnity.Editor
{
    public static class BatchModeRunner
    {
        public static void Run()
        {
            RunConfig.ApplyFromArgs(Environment.GetCommandLineArgs());
            var config = RunConfig.Current;

            var scenePath = ResolveScenePath(config);
            if (!string.IsNullOrWhiteSpace(scenePath))
            {
                EditorSceneManager.OpenScene(scenePath);
            }

            var completionPath = ResolveCompletionPath(config);
            if (!string.IsNullOrWhiteSpace(completionPath))
            {
                EditorPrefs.SetString(BatchModeCompletionWatcher.PrefKey, completionPath);
            }

            EditorApplication.isPlaying = true;
        }

        private static string ResolveScenePath(RunConfig config)
        {
            if (config.SceneSequence.Count > 0)
            {
                var sceneName = config.SceneSequence[0];
                var candidate = Path.Combine("Assets", "Scenes", sceneName + ".unity");
                if (File.Exists(candidate))
                {
                    return candidate.Replace("\\", "/");
                }
            }

            var defaultScene = Path.Combine("Assets", "Scenes", "SampleScene.unity");
            if (File.Exists(defaultScene))
            {
                return defaultScene.Replace("\\", "/");
            }

            Debug.LogWarning("No scene found for batch run.");
            return string.Empty;
        }

        private static string ResolveCompletionPath(RunConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.OutputDirectory))
            {
                return string.Empty;
            }

            return Path.Combine(config.OutputDirectory, "run_complete.txt");
        }
    }
}
