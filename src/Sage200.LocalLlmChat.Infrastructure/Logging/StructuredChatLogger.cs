using System.Text.Json;
using Sage200.LocalLlmChat.Core.Abstractions;

namespace Sage200.LocalLlmChat.Infrastructure.Logging;

public sealed class StructuredChatLogger : IChatLogger
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    private readonly Action<string> _sink;

    public StructuredChatLogger(Action<string>? sink = null)
    {
        _sink = sink ?? Console.WriteLine;
    }

    public void Info(string message, object? data = null) =>
        Write("INFO", message, data);

    public void Warning(string message, object? data = null) =>
        Write("WARN", message, data);

    public void Error(string message, Exception? exception = null, object? data = null)
    {
        var payload = new
        {
            Data = Redact(data),
            Exception = exception?.Message
        };
        Write("ERROR", message, payload);
    }

    private void Write(string level, string message, object? data)
    {
        var payload = new
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Message = message,
            Data = Redact(data)
        };

        _sink(JsonSerializer.Serialize(payload, SerializerOptions));
    }

    private static object? Redact(object? data)
    {
        if (data is null)
        {
            return null;
        }

        var json = JsonSerializer.SerializeToNode(data);
        if (json is null)
        {
            return null;
        }

        RedactNode(json);
        return json;
    }

    private static void RedactNode(System.Text.Json.Nodes.JsonNode node)
    {
        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            var keys = obj.Select(kvp => kvp.Key).ToArray();
            foreach (var key in keys)
            {
                if (IsSensitiveKey(key))
                {
                    obj[key] = "[REDACTED]";
                    continue;
                }

                var child = obj[key];
                if (child is not null)
                {
                    RedactNode(child);
                }
            }
        }
        else if (node is System.Text.Json.Nodes.JsonArray arr)
        {
            foreach (var child in arr.Where(child => child is not null))
            {
                RedactNode(child!);
            }
        }
    }

    private static bool IsSensitiveKey(string key) =>
        key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("apikey", StringComparison.OrdinalIgnoreCase);
}
