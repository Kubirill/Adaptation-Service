using System;
using System.Collections.Generic;
using System.Globalization;

namespace AdaptationUnity
{
    public sealed class RunConfig
    {
        public static RunConfig Current { get; private set; } = new RunConfig();

        public string AdapterName = "Baseline";
        public int Sessions = 5;
        public int Seed = 1234;
        public string ConfigVersion = "v1";
        public string OutputDirectory = string.Empty;
        public float SessionDurationSeconds = 2.0f;
        public int MaxFrames = 300;
        public int Attempts = 1;
        public List<string> SceneSequence = new List<string>();
        public string ServiceUrl = "http://localhost:5000";
        public int ServiceTimeoutMs = 3000;
        public int ServiceRetries = 2;
        public int ServiceRetryDelayMs = 250;
        public string ProfileId = string.Empty;

        public static void ApplyFromArgs(string[] args)
        {
            var cfg = new RunConfig();
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.Equals("-adapter", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.AdapterName = args[++i];
                }
                else if (arg.Equals("-sessions", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.Sessions = ParseInt(args[++i], cfg.Sessions);
                }
                else if (arg.Equals("-seed", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.Seed = ParseInt(args[++i], cfg.Seed);
                }
                else if (arg.Equals("-configVersion", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.ConfigVersion = args[++i];
                }
                else if (arg.Equals("-outDir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.OutputDirectory = args[++i];
                }
                else if (arg.Equals("-sessionDuration", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.SessionDurationSeconds = ParseFloat(args[++i], cfg.SessionDurationSeconds);
                }
                else if (arg.Equals("-maxFrames", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.MaxFrames = ParseInt(args[++i], cfg.MaxFrames);
                }
                else if (arg.Equals("-attempts", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.Attempts = ParseInt(args[++i], cfg.Attempts);
                }
                else if (arg.Equals("-scenes", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.SceneSequence = new List<string>(args[++i].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                }
                else if (arg.Equals("-serviceUrl", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.ServiceUrl = args[++i];
                }
                else if (arg.Equals("-serviceTimeoutMs", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.ServiceTimeoutMs = ParseInt(args[++i], cfg.ServiceTimeoutMs);
                }
                else if (arg.Equals("-serviceRetries", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.ServiceRetries = ParseInt(args[++i], cfg.ServiceRetries);
                }
                else if (arg.Equals("-serviceRetryDelayMs", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.ServiceRetryDelayMs = ParseInt(args[++i], cfg.ServiceRetryDelayMs);
                }
                else if (arg.Equals("-profileId", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    cfg.ProfileId = args[++i];
                }
            }

            Current = cfg;
        }

        private static int ParseInt(string value, int fallback)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private static float ParseFloat(string value, float fallback)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
            return fallback;
        }
    }
}
