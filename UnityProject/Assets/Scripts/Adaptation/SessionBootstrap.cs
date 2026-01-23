using UnityEngine;

namespace AdaptationUnity
{
    public static class SessionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Object.FindObjectOfType<SessionRunner>() != null)
            {
                return;
            }

            var runnerObject = new GameObject("SessionRunner");
            runnerObject.AddComponent<SessionRunner>();
        }
    }
}
