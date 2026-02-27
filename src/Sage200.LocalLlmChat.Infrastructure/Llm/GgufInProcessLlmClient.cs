using Sage200.LocalLlmChat.Core.Abstractions;
using Sage200.LocalLlmChat.Core.Models;

namespace Sage200.LocalLlmChat.Infrastructure.Llm;

public interface IGgufRuntime
{
    Task<string> InferAsync(
        string systemPrompt,
        IReadOnlyList<ChatMessage> conversation,
        CancellationToken cancellationToken);
}

public sealed class GgufInProcessLlmClient : ILocalLlmClient
{
    private readonly IGgufRuntime _runtime;
    private readonly string _modelName;

    public GgufInProcessLlmClient(IGgufRuntime runtime, string modelName = "gguf-runtime")
    {
        _runtime = runtime;
        _modelName = modelName;
    }

    public async Task<LlmInferenceResult> GenerateAsync(
        IReadOnlyList<ChatMessage> conversation,
        string systemPrompt,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var raw = await _runtime.InferAsync(systemPrompt, conversation, cancellationToken);
        return new LlmInferenceResult
        {
            RawResponse = raw,
            ModelName = _modelName,
            Duration = DateTimeOffset.UtcNow - started
        };
    }
}
