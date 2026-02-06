using System;

namespace AdaptationUnity.Adapters
{
    internal static class DecisionBuildTiming
    {
        [ThreadStatic]
        private static Action<double> s_reporter;

        public static IDisposable Begin(Action<double> reporter)
        {
            var previous = s_reporter;
            s_reporter = reporter;
            return new Scope(previous);
        }

        public static void Report(double milliseconds)
        {
            s_reporter?.Invoke(milliseconds);
        }

        private sealed class Scope : IDisposable
        {
            private readonly Action<double> _previous;
            private bool _disposed;

            public Scope(Action<double> previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                s_reporter = _previous;
                _disposed = true;
            }
        }
    }
}
