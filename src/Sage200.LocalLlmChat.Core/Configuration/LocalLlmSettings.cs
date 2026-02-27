namespace Sage200.LocalLlmChat.Core.Configuration;

public sealed class LocalLlmSettings
{
    public string ModelName { get; init; } = "local-default";

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxRetries { get; init; } = 2;

    public int CircuitBreakerFailuresBeforeOpen { get; init; } = 3;

    public TimeSpan CircuitBreakerDuration { get; init; } = TimeSpan.FromSeconds(30);
}
