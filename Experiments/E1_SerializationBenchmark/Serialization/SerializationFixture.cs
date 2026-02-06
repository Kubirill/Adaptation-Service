using System;
using System.Collections.Generic;
using System.Text.Json;
using E1_SerializationBenchmark.Data;
using E1_SerializationBenchmark.FlatBuffers;
using E1_SerializationBenchmark.Serializers;

namespace E1_SerializationBenchmark.Serialization;

public sealed class SerializationFixture
{
    private static readonly Lazy<SerializationFixture> InstanceLazy = new(() => new SerializationFixture());

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Dictionary<(FormatType, MessageType), byte[]> _payloads;

    public static SerializationFixture Instance => InstanceLazy.Value;

    public EventData EventPayload { get; }
    public DecisionData DecisionPayload { get; }

    private SerializationFixture()
    {
        EventPayload = DataFactory.CreateEventData();
        DecisionPayload = DataFactory.CreateDecisionData();
        _payloads = new Dictionary<(FormatType, MessageType), byte[]>();

        foreach (FormatType format in Enum.GetValues<FormatType>())
        {
            foreach (MessageType message in Enum.GetValues<MessageType>())
            {
                _payloads[(format, message)] = Serialize(format, message, useCached: true);
            }
        }
    }

    private object GetMessageInstance(MessageType message) => message switch
    {
        MessageType.Event => EventPayload,
        MessageType.Decision => DecisionPayload,
        _ => throw new ArgumentOutOfRangeException(nameof(message), message, null)
    };

    public byte[] Serialize(FormatType format, MessageType message)
    {
        return Serialize(format, message, useCached: false);
    }

    private byte[] Serialize(FormatType format, MessageType message, bool useCached)
    {
        if (useCached && _payloads.TryGetValue((format, message), out var value))
        {
            return value;
        }

        return format switch
        {
            FormatType.Json => SerializeJson(message),
            FormatType.Protobuf => SerializeProto(message),
            FormatType.FlatBuffers => SerializeFlatBuffers(message),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    public object Deserialize(FormatType format, MessageType message, byte[]? payload = null)
    {
        payload ??= _payloads[(format, message)];

        return format switch
        {
            FormatType.Json => DeserializeJson(message, payload),
            FormatType.Protobuf => DeserializeProto(message, payload),
            FormatType.FlatBuffers => DeserializeFlatBuffers(message, payload),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    public IReadOnlyDictionary<(FormatType, MessageType), byte[]> SerializedPayloads() => _payloads;

    private byte[] SerializeJson(MessageType message)
    {
        var instance = GetMessageInstance(message);
        return JsonSerializer.SerializeToUtf8Bytes(instance, instance.GetType(), JsonOptions);
    }

    private object DeserializeJson(MessageType message, byte[] payload)
    {
        return message switch
        {
            MessageType.Event => JsonSerializer.Deserialize<EventData>(payload, JsonOptions)!,
            MessageType.Decision => JsonSerializer.Deserialize<DecisionData>(payload, JsonOptions)!,
            _ => throw new ArgumentOutOfRangeException(nameof(message), message, null)
        };
    }

    private byte[] SerializeProto(MessageType message)
    {
        return message switch
        {
            MessageType.Event => ProtobufConverters.Serialize(EventPayload),
            MessageType.Decision => ProtobufConverters.Serialize(DecisionPayload),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message, null)
        };
    }

    private object DeserializeProto(MessageType message, byte[] payload)
    {
        return message switch
        {
            MessageType.Event => ProtobufConverters.DeserializeEvent(payload),
            MessageType.Decision => ProtobufConverters.DeserializeDecision(payload),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message, null)
        };
    }

    private byte[] SerializeFlatBuffers(MessageType message)
    {
        return message switch
        {
            MessageType.Event => FlatBufferConverters.Serialize(EventPayload),
            MessageType.Decision => FlatBufferConverters.Serialize(DecisionPayload),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message, null)
        };
    }

    private object DeserializeFlatBuffers(MessageType message, byte[] payload)
    {
        return message switch
        {
            MessageType.Event => FlatBufferConverters.DeserializeEvent(payload),
            MessageType.Decision => FlatBufferConverters.DeserializeDecision(payload),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message, null)
        };
    }

    public int GetPayloadSize(FormatType format, MessageType message) => Serialize(format, message, useCached: true).Length;

    public byte[] GetPayloadCopy(FormatType format, MessageType message)
    {
        var original = _payloads[(format, message)];
        var copy = new byte[original.Length];
        Buffer.BlockCopy(original, 0, copy, 0, original.Length);
        return copy;
    }
}
