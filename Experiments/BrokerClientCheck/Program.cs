using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var host = GetEnv("BROKER_HOST", "127.0.0.1");
var port = GetEnvInt("BROKER_PORT", 5672);
var user = GetEnv("BROKER_USER", "adaptation");
var pass = GetEnv("BROKER_PASS", "adaptation");
var timeoutMs = GetEnvInt("BROKER_TIMEOUT_MS", 2000);

var factory = new ConnectionFactory
{
    HostName = host,
    Port = port,
    UserName = user,
    Password = pass,
    DispatchConsumersAsync = false
};

using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();
channel.QueueDeclare(queue: "adaptation.req", durable: true, exclusive: false, autoDelete: false, arguments: null);

var replyQueue = channel.QueueDeclare(queue: string.Empty, durable: false, exclusive: true, autoDelete: true, arguments: null);
var replyQueueName = replyQueue.QueueName;

var correlationId = Guid.NewGuid().ToString("N");
var tcs = new ManualResetEventSlim(false);
string responseBody = null;

var consumer = new EventingBasicConsumer(channel);
consumer.Received += (_, ea) =>
{
    if (ea.BasicProperties?.CorrelationId == correlationId)
    {
        responseBody = Encoding.UTF8.GetString(ea.Body.ToArray());
        tcs.Set();
    }
};
channel.BasicConsume(queue: replyQueueName, autoAck: true, consumer: consumer);

var request = new AdaptationEvent
{
    session_id = "check_session",
    scene_id = "check_scene",
    result_z = 0.5f,
    time_t = 1.0f,
    attempts_a = 1,
    seed = 123,
    config_version = "v1"
};

var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
{
    IncludeFields = true,
    PropertyNamingPolicy = null,
    DictionaryKeyPolicy = null
});

var props = channel.CreateBasicProperties();
props.CorrelationId = correlationId;
props.ReplyTo = replyQueueName;

channel.BasicPublish(exchange: string.Empty, routingKey: "adaptation.req", basicProperties: props, body: Encoding.UTF8.GetBytes(json));

if (!tcs.Wait(TimeSpan.FromMilliseconds(Math.Max(100, timeoutMs))))
{
    Console.Error.WriteLine("Timeout waiting for broker reply.");
    return 1;
}

Console.WriteLine($"OK: correlation_id={correlationId}");
Console.WriteLine($"Reply: {responseBody}");
return 0;

static string GetEnv(string key, string fallback)
{
    var value = Environment.GetEnvironmentVariable(key);
    return string.IsNullOrWhiteSpace(value) ? fallback : value;
}

static int GetEnvInt(string key, int fallback)
{
    var value = Environment.GetEnvironmentVariable(key);
    if (int.TryParse(value, out var parsed))
    {
        return parsed;
    }
    return fallback;
}

internal sealed class AdaptationEvent
{
    public string session_id;
    public string scene_id;
    public float result_z;
    public float time_t;
    public int attempts_a;
    public int seed;
    public string config_version;
}
