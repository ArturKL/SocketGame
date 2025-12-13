using System.Text.Json;

namespace GameEngine.Messaging;

public class MessageEnvelope
{
    public string Version { get; set; } = "1.0";
    public MessageType MessageType { get; set; }
    public string? CorrelationId { get; set; }
    public string? SessionId { get; set; }
    public string Payload { get; set; } = "";
}

public interface IMessageSerializer
{
    byte[] Serialize(MessageEnvelope envelope);
    MessageEnvelope? Deserialize(ReadOnlySpan<byte> bytes);

    string SerializePayload<T>(T payload);
    T? DeserializePayload<T>(string payload);
}

public class JsonMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public byte[] Serialize(MessageEnvelope envelope)
    {
        return JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
    }

    public MessageEnvelope? Deserialize(ReadOnlySpan<byte> bytes)
    {
        return JsonSerializer.Deserialize<MessageEnvelope>(bytes, Options);
    }

    public string SerializePayload<T>(T payload)
    {
        return JsonSerializer.Serialize(payload, Options);
    }

    public T? DeserializePayload<T>(string payload)
    {
        return JsonSerializer.Deserialize<T>(payload, Options);
    }
}