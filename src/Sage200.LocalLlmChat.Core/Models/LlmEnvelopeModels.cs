using System.Text.Json.Nodes;

namespace Sage200.LocalLlmChat.Core.Models;

public sealed class LlmActionEnvelope
{
    public string Action { get; init; } = string.Empty;

    public string Entity { get; init; } = "none";

    public JsonObject Fields { get; init; } = new();

    public IReadOnlyList<LlmOrderItem> Items { get; init; } = Array.Empty<LlmOrderItem>();

    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    public LlmMeta Meta { get; init; } = new();
}

public sealed class LlmOrderItem
{
    public string Type { get; init; } = string.Empty;

    public string? StockCode { get; init; }

    public string? Description { get; init; }

    public decimal? Qty { get; init; }

    public decimal? UnitPrice { get; init; }

    public string? Uom { get; init; }

    public string? Warehouse { get; init; }

    public string? AdditionalChargeCode { get; init; }

    public string? TaxCode { get; init; }

    public decimal? DiscountPercent { get; init; }
}

public sealed class LlmMeta
{
    public bool UserConfirmationRequired { get; init; }

    public string? Notes { get; init; }
}

public sealed class EnvelopeParseResult
{
    public static EnvelopeParseResult Success(LlmActionEnvelope envelope) =>
        new() { IsSuccess = true, Envelope = envelope };

    public static EnvelopeParseResult Failure(string message, string? details = null) =>
        new() { IsSuccess = false, ErrorMessage = message, ErrorDetails = details };

    public bool IsSuccess { get; init; }

    public LlmActionEnvelope? Envelope { get; init; }

    public string? ErrorMessage { get; init; }

    public string? ErrorDetails { get; init; }
}

public sealed class EnvelopeValidationResult
{
    public bool IsValid { get; init; }

    public bool CanExecute { get; init; }

    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class ChatMessage
{
    public string Role { get; init; } = "user";

    public string Content { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class DraftOrderContext
{
    public string DraftId { get; init; } = Guid.NewGuid().ToString("N");

    public string Action { get; init; } = string.Empty;

    public JsonObject HeaderFields { get; init; } = new();

    public IReadOnlyList<LlmOrderItem> Items { get; init; } = Array.Empty<LlmOrderItem>();

    public string Summary { get; init; } = string.Empty;
}

public sealed class ChatSessionContext
{
    public DraftOrderContext? PendingOrder { get; set; }
}

public sealed class ChatTurnResult
{
    public bool Success { get; init; }

    public string UserFacingMessage { get; init; } = string.Empty;

    public string? PreviewCard { get; init; }

    public bool ShowConfirmButtons { get; init; }

    public bool ShowErrorBanner { get; init; }

    public string? TechnicalDetails { get; init; }

    public string? CreatedEntity { get; init; }

    public string? CreatedRecordId { get; init; }

    public IReadOnlyList<string> ClarificationQuestions { get; init; } = Array.Empty<string>();
}

public sealed class LlmInferenceResult
{
    public string RawResponse { get; init; } = string.Empty;

    public string? ModelName { get; init; }

    public TimeSpan Duration { get; init; }
}

public sealed class LookupCandidate
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
}

public sealed class DuplicateDetectionResult
{
    public bool HasLikelyDuplicates { get; init; }

    public IReadOnlyList<LookupCandidate> Candidates { get; init; } = Array.Empty<LookupCandidate>();
}

public sealed class CreateRecordResult
{
    public bool Success { get; init; }

    public string Entity { get; init; } = string.Empty;

    public string RecordId { get; init; } = string.Empty;

    public string? DisplayReference { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed class OrderDraftBuildResult
{
    public bool Success { get; init; }

    public DraftOrderContext? Draft { get; init; }

    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    public string? ErrorMessage { get; init; }
}
