using System.Text.Json.Nodes;
using Sage200.LocalLlmChat.Core.Models;

namespace Sage200.LocalLlmChat.Core.Abstractions;

public interface ILocalLlmClient
{
    Task<LlmInferenceResult> GenerateAsync(
        IReadOnlyList<ChatMessage> conversation,
        string systemPrompt,
        CancellationToken cancellationToken);
}

public interface ICrmService
{
    Task<IReadOnlyList<string>> ResolveMandatoryFieldsAsync(
        string action,
        JsonObject proposedFields,
        CancellationToken cancellationToken);

    Task<DuplicateDetectionResult> DetectLikelyCompanyDuplicatesAsync(
        JsonObject proposedFields,
        CancellationToken cancellationToken);

    Task<DuplicateDetectionResult> DetectLikelyPersonDuplicatesAsync(
        JsonObject proposedFields,
        CancellationToken cancellationToken);

    Task<CreateRecordResult> CreateAsync(
        string action,
        JsonObject fields,
        CancellationToken cancellationToken);
}

public interface IOrdersService
{
    Task<IReadOnlyList<string>> ResolveMandatoryHeaderFieldsAsync(
        string action,
        JsonObject headerFields,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ResolveMandatoryLineFieldsAsync(
        string action,
        IReadOnlyList<LlmOrderItem> lines,
        CancellationToken cancellationToken);

    Task<OrderDraftBuildResult> BuildDraftAsync(
        string action,
        JsonObject headerFields,
        IReadOnlyList<LlmOrderItem> lines,
        CancellationToken cancellationToken);

    Task<CreateRecordResult> ConfirmDraftAsync(
        DraftOrderContext draftContext,
        bool postOrder,
        CancellationToken cancellationToken);

    Task<CreateRecordResult> AddLinesAsync(
        JsonObject fields,
        IReadOnlyList<LlmOrderItem> lines,
        CancellationToken cancellationToken);
}

public interface IClarificationQuestionManager
{
    IReadOnlyList<string> BuildQuestions(
        IReadOnlyList<string> missingFields,
        LlmActionEnvelope envelope);
}

public interface IRecordNavigator
{
    void OpenRecord(string entity, string recordId);
}

public interface IUserPermissionService
{
    Task<bool> HasPermissionAsync(string action, CancellationToken cancellationToken);
}

public interface IChatLogger
{
    void Info(string message, object? data = null);

    void Warning(string message, object? data = null);

    void Error(string message, Exception? exception = null, object? data = null);
}
