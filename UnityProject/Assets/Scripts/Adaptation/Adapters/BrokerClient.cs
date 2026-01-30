using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using AdaptationCore;
using AdaptationUnity.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UnityEngine;

namespace AdaptationUnity.Adapters
{
    public sealed class BrokerClient
    {
        private readonly RunConfig _config;
        private SessionLogWriter _logWriter;
        private string _sessionId = string.Empty;
        private int _sessionIndex;
        private bool _warmup;

        private IConnection _connection;
        private IModel _channel;
        private string _replyQueueName;
        private EventingBasicConsumer _consumer;
        private readonly ConcurrentDictionary<string, PendingResponse> _pending = new ConcurrentDictionary<string, PendingResponse>();

        public BrokerClient(RunConfig config)
        {
            _config = config;
            Connect();
        }

        public void SetLogger(SessionLogWriter logWriter)
        {
            _logWriter = logWriter;
        }

        public void SetSessionContext(string sessionId, int sessionIndex, bool warmup)
        {
            _sessionId = sessionId ?? string.Empty;
            _sessionIndex = sessionIndex;
            _warmup = warmup;
        }

        public AdaptationDecision ComputeNext(AdaptationEvent sessionEvent, out AdaptationAuditRecord auditRecord)
        {
            auditRecord = null;

            var serializeTimer = Stopwatch.StartNew();
            var payload = JsonUtility.ToJson(sessionEvent);
            serializeTimer.Stop();

            var totalTimer = Stopwatch.StartNew();
            var brokerMs = 0.0;
            var deserializeMs = 0.0;
            var serverMs = 0.0;
            var retriesCount = 0;
            var timeoutFlag = false;
            var status = "OK";

            Exception lastError = null;
            var maxAttempts = 2;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var correlationId = Guid.NewGuid().ToString("N");
                var pending = new PendingResponse();
                _pending[correlationId] = pending;

                var props = _channel.CreateBasicProperties();
                props.CorrelationId = correlationId;
                props.ReplyTo = _replyQueueName;

                var timer = Stopwatch.StartNew();
                try
                {
                    var body = Encoding.UTF8.GetBytes(payload);
                    _channel.BasicPublish(exchange: string.Empty, routingKey: "adaptation.req", basicProperties: props, body: body);

                    var timeoutMs = Math.Max(100, _config.BrokerTimeoutMs);
                    if (!pending.WaitHandle.Wait(TimeSpan.FromMilliseconds(timeoutMs)))
                    {
                        timeoutFlag = true;
                        status = "Timeout";
                        retriesCount = attempt - 1;
                        throw new TimeoutException("Broker RPC timeout.");
                    }

                    timer.Stop();
                    brokerMs = timer.Elapsed.TotalMilliseconds;

                    var responseBody = pending.ResponseBody;
                    serverMs = ParseHeaderMs(pending.Headers, "x-server-compute-ms");

                    var deserializeTimer = Stopwatch.StartNew();
                    var decision = JsonUtility.FromJson<AdaptationDecision>(responseBody);
                    deserializeTimer.Stop();
                    deserializeMs = deserializeTimer.Elapsed.TotalMilliseconds;

                    retriesCount = attempt - 1;
                    totalTimer.Stop();
                    status = "OK";
                    _logWriter?.LogR4BrokerBreakdown(
                        serializeTimer.Elapsed.TotalMilliseconds,
                        brokerMs,
                        serverMs,
                        deserializeMs,
                        totalTimer.Elapsed.TotalMilliseconds,
                        retriesCount,
                        timeoutFlag,
                        status
                    );
                    return decision;
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    lastError = ex;
                    var shouldRetry = false;
                    if (ex is TimeoutException)
                    {
                        timeoutFlag = true;
                        status = "Timeout";
                        _logWriter?.LogServiceError(_sessionId, _sessionIndex, _warmup, attempt, ex.Message, timer.Elapsed.TotalMilliseconds);
                        if (attempt < maxAttempts)
                        {
                            Thread.Sleep(Math.Max(0, _config.ServiceRetryDelayMs));
                            shouldRetry = true;
                        }
                    }
                    else
                    {
                        status = ex.GetType().Name;
                        _logWriter?.LogServiceError(_sessionId, _sessionIndex, _warmup, attempt, ex.Message, timer.Elapsed.TotalMilliseconds);
                    }
                    retriesCount = attempt - 1;
                    if (shouldRetry)
                    {
                        continue;
                    }
                    break;
                }
                finally
                {
                    _pending.TryRemove(correlationId, out _);
                    pending.Dispose();
                }
            }

            totalTimer.Stop();
            _logWriter?.LogR4BrokerBreakdown(
                serializeTimer.Elapsed.TotalMilliseconds,
                brokerMs,
                serverMs,
                deserializeMs,
                totalTimer.Elapsed.TotalMilliseconds,
                Math.Max(0, retriesCount),
                timeoutFlag,
                status
            );

            throw lastError ?? new Exception("Broker RPC failed.");
        }

        private void Connect()
        {
            var factory = new ConnectionFactory
            {
                HostName = _config.BrokerHost,
                Port = _config.BrokerPort,
                UserName = _config.BrokerUser,
                Password = _config.BrokerPass,
                DispatchConsumersAsync = false
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: "adaptation.req", durable: true, exclusive: false, autoDelete: false, arguments: null);

            var replyQueue = _channel.QueueDeclare(queue: string.Empty, durable: false, exclusive: true, autoDelete: true, arguments: null);
            _replyQueueName = replyQueue.QueueName;

            _consumer = new EventingBasicConsumer(_channel);
            _consumer.Received += (_, ea) =>
            {
                var correlationId = ea.BasicProperties?.CorrelationId ?? string.Empty;
                if (_pending.TryGetValue(correlationId, out var pending))
                {
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    pending.SetResponse(body, ea.BasicProperties?.Headers);
                }
            };

            _channel.BasicConsume(queue: _replyQueueName, autoAck: true, consumer: _consumer);
        }

        private static double ParseHeaderMs(IDictionary<string, object> headers, string key)
        {
            var value = ReadHeader(headers, key);
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
            return 0.0;
        }

        private static string ReadHeader(IDictionary<string, object> headers, string key)
        {
            if (headers == null)
            {
                return string.Empty;
            }

            foreach (var kvp in headers)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return HeaderValueToString(kvp.Value);
                }
            }

            return string.Empty;
        }

        private static string HeaderValueToString(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value is byte[] bytes)
            {
                return Encoding.UTF8.GetString(bytes);
            }

            return value.ToString();
        }

        private sealed class PendingResponse : IDisposable
        {
            public ManualResetEventSlim WaitHandle { get; } = new ManualResetEventSlim(false);
            public string ResponseBody { get; private set; } = string.Empty;
            public IDictionary<string, object> Headers { get; private set; }

            public void SetResponse(string body, IDictionary<string, object> headers)
            {
                ResponseBody = body ?? string.Empty;
                Headers = headers;
                WaitHandle.Set();
            }

            public void Dispose()
            {
                WaitHandle?.Dispose();
            }
        }
    }
}
