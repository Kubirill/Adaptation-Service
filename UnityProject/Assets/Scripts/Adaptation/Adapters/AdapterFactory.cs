using System;
using AdaptationCore;

namespace AdaptationUnity.Adapters
{
    public static class AdapterFactory
    {
        public static IAdaptationAdapter Create(string adapterName)
        {
            if (string.IsNullOrWhiteSpace(adapterName))
            {
                return new BaselineAdapter();
            }

            switch (adapterName.Trim().ToUpperInvariant())
            {
                case "B1":
                case "INPROCESS":
                    return new B1InProcessAdapter();
                case "B2":
                case "JSON":
                    return new B2JsonRoundTripAdapter();
                case "B3":
                case "FILE":
                    return new B3FileRoundTripAdapter();
                case "BASELINE":
                default:
                    return new BaselineAdapter();
            }
        }
    }
}
