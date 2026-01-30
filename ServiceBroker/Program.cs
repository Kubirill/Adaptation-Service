using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using AdaptationCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var settings = BrokerWorkerSettings.LoadFromEnv();
Directory.CreateDirectory(settings.LogDirectory);

Console.WriteLine("ServiceBroker starting.");
Console.WriteLine($"Broker: {settings.BrokerHost}:{settings.BrokerPort}");
Console.WriteLine($"Config root: {settings.ConfigRoot}");
Console.WriteLine($"Config version: {settings.ConfigVersion}");
Console.WriteLine($"Log dir: {settings.LogDirectory}");

var config = ConfigLoader.LoadByVersion(settings.ConfigRoot, settings.ConfigVersion);

using var logWriter = new WorkerLogWriter(Path.Combine(settings.LogDirectory, "worker_events.csv"));

var factory = new ConnectionFactory
{
    HostName = settings.BrokerHost,
    Port = settings.BrokerPort,
    UserName = settings.BrokerUser,
    Password = settings.BrokerPass,
    DispatchConsumersAsync = false
};

using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.QueueDeclare(queue: "adaptation.req", durable: true, exclusive: false, autoDelete: false, arguments: null);
channel.BasicQos(0, 1, false);

var consumer = new EventingBasicConsumer(channel);
consumer.Received += (_, ea) =>
{
    var receiveTime = DateTime.UtcNow;
    var correlationId = ea.BasicProperties?.CorrelationId ?? string.Empty;
    var replyTo = ea.BasicProperties?.ReplyTo ?? string.Empty;
    var status = "OK";

    try
    {
        if (string.IsNullOrWhiteSpace(replyTo))
        {
            throw new InvalidOperationException("ReplyTo is missing on request.");
        }

        var body = Encoding.UTF8.GetString(ea.Body.ToArray());
        var sessionEvent = DeserializeEvent(body);
        if (string.IsNullOrWhiteSpace(sessionEvent.config_version))
        {
            sessionEvent.config_version = config.version;
        }

        var computeTimer = Stopwatch.StartNew();
        var result = AdaptationEngine.ComputeNext(sessionEvent, config);
        computeTimer.Stop();

        var responseJson = SerializeDecision(result.Decision);
        var responseBytes = Encoding.UTF8.GetBytes(responseJson);

        var props = channel.CreateBasicProperties();
        props.CorrelationId = correlationId;
        props.Headers = new Dictionary<string, object>
        {
            { "x-server-compute-ms", computeTimer.Elapsed.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture) },
            { "x-correlation-id", correlationId }
        };

        channel.BasicPublish(exchange: string.Empty, routingKey: replyTo, basicProperties: props, body: responseBytes);
        channel.BasicAck(ea.DeliveryTag, false);

        logWriter.WriteEvent(receiveTime, DateTime.UtcNow, computeTimer.Elapsed.TotalMilliseconds, correlationId, status);
    }
    catch (Exception ex)
    {
        status = "ERROR";
        Console.Error.WriteLine($"Error handling request: {ex.GetType().Name} - {ex.Message}");
        logWriter.WriteEvent(receiveTime, DateTime.UtcNow, 0.0, correlationId, status);
        channel.BasicNack(ea.DeliveryTag, false, false);
    }
};

channel.BasicConsume(queue: "adaptation.req", autoAck: false, consumer: consumer);

var quit = new System.Threading.ManualResetEvent(false);
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    quit.Set();
};

quit.WaitOne();

static AdaptationEvent DeserializeEvent(string json)
{
    var options = new JsonSerializerOptions
    {
        IncludeFields = true,
        PropertyNamingPolicy = null,
        DictionaryKeyPolicy = null
    };
    return JsonSerializer.Deserialize<AdaptationEvent>(json, options) ?? new AdaptationEvent();
}

static string SerializeDecision(AdaptationDecision decision)
{
    var options = new JsonSerializerOptions
    {
        IncludeFields = true,
        PropertyNamingPolicy = null,
        DictionaryKeyPolicy = null
    };
    return JsonSerializer.Serialize(decision ?? new AdaptationDecision(), options);
}

internal sealed class BrokerWorkerSettings
{
    public string BrokerHost { get; set; } = "127.0.0.1";
    public int BrokerPort { get; set; } = 5672;
    public string BrokerUser { get; set; } = "adaptation";
    public string BrokerPass { get; set; } = "adaptation";
    public string ConfigRoot { get; set; } = string.Empty;
    public string ConfigVersion { get; set; } = "v1";
    public string LogDirectory { get; set; } = string.Empty;

    public static BrokerWorkerSettings LoadFromEnv()
    {
        var settings = new BrokerWorkerSettings();
        settings.BrokerHost = GetEnv("BROKER_HOST", settings.BrokerHost);
        settings.BrokerPort = GetIntEnv("BROKER_PORT", settings.BrokerPort);
        settings.BrokerUser = GetEnv("BROKER_USER", settings.BrokerUser);
        settings.BrokerPass = GetEnv("BROKER_PASS", settings.BrokerPass);
        settings.ConfigRoot = GetEnv("CONFIG_ROOT", settings.ConfigRoot);
        settings.ConfigVersion = GetEnv("CONFIG_VERSION", settings.ConfigVersion);
        settings.LogDirectory = GetEnv("WORKER_LOG_DIR", settings.LogDirectory);

        if (string.IsNullOrWhiteSpace(settings.ConfigRoot))
        {
            var repoRoot = ResolveRepoRoot();
            settings.ConfigRoot = Path.Combine(repoRoot, "Configs");
        }
        if (string.IsNullOrWhiteSpace(settings.LogDirectory))
        {
            var repoRoot = ResolveRepoRoot();
            settings.LogDirectory = Path.Combine(repoRoot, "Experiments", "server_logs", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        }

        return settings;
    }

    private static string GetEnv(string key, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int GetIntEnv(string key, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (int.TryParse(value, out var parsed))
        {
            return parsed;
        }
        return fallback;
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && dir != null; i++)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Configs")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}

internal sealed class WorkerLogWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public WorkerLogWriter(string path)
    {
        _writer = new StreamWriter(path, false, Encoding.UTF8) { AutoFlush = true };
        _writer.WriteLine("ts_receive,ts_publish,server_compute_ms,correlation_id,status");
    }

    public void WriteEvent(DateTime receiveUtc, DateTime publishUtc, double serverComputeMs, string correlationId, string status)
    {
        _writer.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2:0.000},\"{3}\",{4}",
            receiveUtc.ToString("o", CultureInfo.InvariantCulture),
            publishUtc.ToString("o", CultureInfo.InvariantCulture),
            serverComputeMs,
            Escape(correlationId),
            status
        ));
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return value.Replace("\"", "\"\"");
    }
}
