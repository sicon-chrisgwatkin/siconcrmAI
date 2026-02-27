using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Sage200.LocalLlmChat.Core.Abstractions;
using Sage200.LocalLlmChat.Core.Models;

namespace Sage200.LocalLlmChat.Infrastructure.Llm;

public sealed class OllamaLocalLlmClient : ILocalLlmClient
{
    private readonly HttpClient _httpClient;
    private readonly LocalLlmConnectorOptions _options;

    public OllamaLocalLlmClient(HttpClient httpClient, LocalLlmConnectorOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<LlmInferenceResult> GenerateAsync(
        IReadOnlyList<ChatMessage> conversation,
        string systemPrompt,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.Timeout);

        var requestBody = new
        {
            model = _options.Model,
            stream = false,
            messages = BuildMessages(systemPrompt, conversation)
        };

        var endpoint = new Uri(_options.Endpoint, "/api/chat");
        using var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: timeoutCts.Token);
        var content = json?["message"]?["content"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Ollama did not return message.content.");
        }

        return new LlmInferenceResult
        {
            RawResponse = content,
            ModelName = _options.Model,
            Duration = DateTimeOffset.UtcNow - started
        };
    }

    private static IReadOnlyList<object> BuildMessages(string systemPrompt, IReadOnlyList<ChatMessage> conversation)
    {
        var messages = new List<object>(conversation.Count + 1)
        {
            new { role = "system", content = systemPrompt }
        };
        messages.AddRange(conversation.Select(message => new { role = message.Role, content = message.Content }));
        return messages;
    }
}
