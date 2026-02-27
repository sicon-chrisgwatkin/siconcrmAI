using Sage200.LocalLlmChat.Core.Abstractions;
using Sage200.LocalLlmChat.Core.Models;

namespace Sage200.LocalLlmChat.Infrastructure.Llm;

public sealed class LocalLlmUnavailableException : Exception
{
    public LocalLlmUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class ResilientLocalLlmClient : ILocalLlmClient
{
    private readonly ILocalLlmClient _inner;
    private readonly IChatLogger _logger;
    private readonly LocalLlmConnectorOptions _options;
    private readonly object _sync = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _circuitOpenUntil;

    public ResilientLocalLlmClient(
        ILocalLlmClient inner,
        LocalLlmConnectorOptions options,
        IChatLogger logger)
    {
        _inner = inner;
        _options = options;
        _logger = logger;
    }

    public async Task<LlmInferenceResult> GenerateAsync(
        IReadOnlyList<ChatMessage> conversation,
        string systemPrompt,
        CancellationToken cancellationToken)
    {
        if (IsCircuitOpen())
        {
            throw new LocalLlmUnavailableException("Local LLM is temporarily unavailable (circuit open).");
        }

        Exception? lastError = null;
        for (var attempt = 0; attempt <= _options.RetryCount; attempt++)
        {
            try
            {
                var result = await _inner.GenerateAsync(conversation, systemPrompt, cancellationToken);
                ResetFailures();
                return result;
            }
            catch (Exception ex) when (attempt < _options.RetryCount)
            {
                lastError = ex;
                _logger.Warning("Local LLM request failed, retrying.", new { attempt, error = ex.Message });
                var delayMs = (int)Math.Pow(2, attempt) * 250;
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        RegisterFailure();
        throw new LocalLlmUnavailableException(
            "Local LLM request failed after retries. Please verify local runtime availability.",
            lastError);
    }

    private bool IsCircuitOpen()
    {
        lock (_sync)
        {
            if (_circuitOpenUntil is null)
            {
                return false;
            }

            if (_circuitOpenUntil <= DateTimeOffset.UtcNow)
            {
                _circuitOpenUntil = null;
                return false;
            }

            return true;
        }
    }

    private void RegisterFailure()
    {
        lock (_sync)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures < _options.CircuitBreakerThreshold)
            {
                return;
            }

            _circuitOpenUntil = DateTimeOffset.UtcNow.Add(_options.CircuitBreakerResetAfter);
            _logger.Warning("Local LLM circuit opened.", new
            {
                _consecutiveFailures,
                _circuitOpenUntil
            });
        }
    }

    private void ResetFailures()
    {
        lock (_sync)
        {
            _consecutiveFailures = 0;
            _circuitOpenUntil = null;
        }
    }
}
