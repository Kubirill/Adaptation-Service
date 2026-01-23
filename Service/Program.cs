using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using AdaptationCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.IncludeFields = true;
    options.SerializerOptions.PropertyNamingPolicy = null;
    options.SerializerOptions.DictionaryKeyPolicy = null;
});

var app = builder.Build();

var settings = ServiceSettings.Load(builder.Configuration);
var config = ConfigLoader.LoadByVersion(settings.ConfigRoot, settings.ConfigVersion);

var auditWriter = new ServiceAuditWriter(settings.AuditRoot);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/computeNext", (ComputeNextRequest request, HttpResponse response) =>
{
    var sessionEvent = request.ToEvent();
    if (string.IsNullOrWhiteSpace(sessionEvent.config_version))
    {
        sessionEvent.config_version = config.version;
    }

    var result = AdaptationEngine.ComputeNext(sessionEvent, config);
    auditWriter.Append(sessionEvent.session_id, result.Audit);

    response.Headers["X-Config-Hash"] = result.Audit.config_version_hash ?? string.Empty;
    response.Headers["X-Config-Version"] = sessionEvent.config_version ?? string.Empty;
    response.Headers["X-Intermediate"] = ServiceAuditWriter.FlattenIntermediates(result.Audit);

    return Results.Json(result.Decision);
});

app.Run(settings.Urls);

internal sealed class ServiceSettings
{
    public string ConfigRoot { get; set; } = string.Empty;
    public string ConfigVersion { get; set; } = "v1";
    public string AuditRoot { get; set; } = string.Empty;
    public string Urls { get; set; } = "http://localhost:5000";

    public static ServiceSettings Load(IConfiguration config)
    {
        return new ServiceSettings
        {
            ConfigRoot = config["ConfigRoot"] ?? config["configRoot"] ?? string.Empty,
            ConfigVersion = config["ConfigVersion"] ?? config["configVersion"] ?? "v1",
            AuditRoot = config["AuditRoot"] ?? config["auditRoot"] ?? string.Empty,
            Urls = config["urls"] ?? config["Urls"] ?? "http://localhost:5000"
        };
    }
}

internal sealed class ComputeNextRequest
{
    public string session_id;
    public string scene_id;
    public float result_z;
    public float time_t;
    public int attempts_a;
    public int seed;
    public string config_version;
    public string profile_id;

    public AdaptationEvent ToEvent()
    {
        return new AdaptationEvent
        {
            session_id = session_id ?? string.Empty,
            scene_id = scene_id ?? string.Empty,
            result_z = result_z,
            time_t = time_t,
            attempts_a = attempts_a,
            seed = seed,
            config_version = config_version ?? string.Empty
        };
    }
}

internal sealed class ServiceAuditWriter
{
    private readonly string _auditPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ServiceAuditWriter(string auditRoot)
    {
        if (string.IsNullOrWhiteSpace(auditRoot))
        {
            _auditPath = string.Empty;
            return;
        }

        Directory.CreateDirectory(auditRoot);
        _auditPath = Path.Combine(auditRoot, "audit.jsonl");
        _jsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null
        };
    }

    public void Append(string sessionId, AdaptationAuditRecord audit)
    {
        if (string.IsNullOrWhiteSpace(_auditPath) || audit == null)
        {
            return;
        }

        var line = new ServiceAuditLine
        {
            session_id = sessionId ?? string.Empty,
            timestamp_utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            inputs = audit.inputs,
            output = audit.output,
            config_version_hash = audit.config_version_hash ?? string.Empty,
            seed = audit.seed,
            intermediate = audit.intermediates
        };

        var json = JsonSerializer.Serialize(line, _jsonOptions);
        File.AppendAllText(_auditPath, json + Environment.NewLine, Encoding.UTF8);
    }

    public static string FlattenIntermediates(AdaptationAuditRecord audit)
    {
        if (audit?.intermediates == null || audit.intermediates.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var first = true;
        foreach (var kvp in audit.intermediates)
        {
            if (!first)
            {
                builder.Append(";");
            }
            first = false;
            builder.Append(kvp.Key);
            builder.Append("=");
            builder.Append(kvp.Value.ToString("0.000", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }
}

internal sealed class ServiceAuditLine
{
    public string session_id;
    public string timestamp_utc;
    public AdaptationEvent inputs;
    public AdaptationDecision output;
    public string config_version_hash;
    public int seed;
    public Dictionary<string, float> intermediate;
}
