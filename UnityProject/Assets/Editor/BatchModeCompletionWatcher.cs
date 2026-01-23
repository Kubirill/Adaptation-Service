using System.IO;
using UnityEditor;

namespace AdaptationUnity.Editor
{
    [InitializeOnLoad]
    public static class BatchModeCompletionWatcher
    {
        public const string PrefKey = "AdaptationRunCompletePath";

        static BatchModeCompletionWatcher()
        {
            EditorApplication.update += PollForCompletion;
        }

        private static void PollForCompletion()
        {
            var completionPath = EditorPrefs.GetString(PrefKey, string.Empty);
            if (string.IsNullOrWhiteSpace(completionPath))
            {
                return;
            }

            if (!File.Exists(completionPath))
            {
                return;
            }

            EditorPrefs.DeleteKey(PrefKey);
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
    }
}
