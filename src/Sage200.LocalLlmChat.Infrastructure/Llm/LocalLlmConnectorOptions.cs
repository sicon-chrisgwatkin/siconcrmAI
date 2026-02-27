namespace Sage200.LocalLlmChat.Infrastructure.Llm;

public sealed class LocalLlmConnectorOptions
{
    public Uri Endpoint { get; init; } = new("http://127.0.0.1:11434");

    public string Model { get; init; } = "llama3";

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public int RetryCount { get; init; } = 2;

    public int CircuitBreakerThreshold { get; init; } = 3;

    public TimeSpan CircuitBreakerResetAfter { get; init; } = TimeSpan.FromSeconds(30);
}
