using System;
using AdaptationCore;

namespace AdaptationUnity.Adapters
{
    public static class AdapterFactory
    {
        public static IAdaptationAdapter Create(string adapterName, AdaptationUnity.Logging.SessionLogWriter logWriter)
        {
            if (string.IsNullOrWhiteSpace(adapterName))
            {
                return AttachLogger(new BaselineAdapter(), logWriter);
            }

            switch (adapterName.Trim().ToUpperInvariant())
            {
                case "B1":
                case "INPROCESS":
                    return AttachLogger(new EngineAdapterB1(), logWriter);
                case "B2":
                case "JSON":
                    return AttachLogger(new EngineAdapterB2(), logWriter);
                case "B3":
                case "FILE":
                    return AttachLogger(new B3FileRoundTripAdapter(), logWriter);
                case "BASELINE":
                default:
                    return AttachLogger(new BaselineAdapter(), logWriter);
            }
        }

        private static IAdaptationAdapter AttachLogger(IAdaptationAdapter adapter, AdaptationUnity.Logging.SessionLogWriter logWriter)
        {
            if (adapter is IAdapterWithLogger withLogger && logWriter != null)
            {
                withLogger.SetLogger(logWriter);
            }
            return adapter;
        }
    }
}
