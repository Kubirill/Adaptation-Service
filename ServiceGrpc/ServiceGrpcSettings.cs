using System;
using Microsoft.Extensions.Configuration;

namespace ServiceGrpc
{
    internal sealed class ServiceGrpcSettings
    {
        public string ConfigRoot { get; set; } = string.Empty;
        public string ConfigVersion { get; set; } = "v1";
        public string AuditRoot { get; set; } = string.Empty;
        public string GrpcUrl { get; set; } = "http://0.0.0.0:6002";
        public string HealthUrl { get; set; } = "http://0.0.0.0:6003";

        public static ServiceGrpcSettings Load(IConfiguration config)
        {
            return new ServiceGrpcSettings
            {
                ConfigRoot = config["ConfigRoot"] ?? config["configRoot"] ?? string.Empty,
                ConfigVersion = config["ConfigVersion"] ?? config["configVersion"] ?? "v1",
                AuditRoot = config["AuditRoot"] ?? config["auditRoot"] ?? string.Empty,
                GrpcUrl = config["GrpcUrl"] ?? config["grpcUrl"] ?? "http://0.0.0.0:6002",
                HealthUrl = config["HealthUrl"] ?? config["healthUrl"] ?? "http://0.0.0.0:6003"
            };
        }
    }
}
