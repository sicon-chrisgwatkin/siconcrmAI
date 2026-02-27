using System.Text.Json.Nodes;
using Sage200.LocalLlmChat.Core.Models;
using Sage200.LocalLlmChat.Core.Validation;

namespace Sage200.LocalLlmChat.Tests;

public sealed class LlmEnvelopeValidatorTests
{
    private readonly LlmEnvelopeValidator _validator = new();

    [Fact]
    public void Validate_OrderWithoutConfirmation_IsInvalid()
    {
        var envelope = new LlmActionEnvelope
        {
            Action = "create_sales_order",
            Entity = "sales_order",
            Fields = new JsonObject { ["company"] = "Acme Ltd" },
            Items =
            [
                new LlmOrderItem { Type = "stock", StockCode = "ABC-123", Qty = 2 }
            ],
            MissingFields = [],
            Meta = new LlmMeta { UserConfirmationRequired = false }
        };

        var result = _validator.Validate(envelope);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("confirmation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WithMissingFields_BlocksExecutionWithoutErrors()
    {
        var envelope = new LlmActionEnvelope
        {
            Action = "create_task",
            Entity = "task",
            Fields = new JsonObject(),
            MissingFields = ["company", "due_date"],
            Meta = new LlmMeta { UserConfirmationRequired = false }
        };

        var result = _validator.Validate(envelope);

        Assert.True(result.IsValid);
        Assert.False(result.CanExecute);
        Assert.Equal(2, result.MissingFields.Count);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_AddOrderLinesWithoutTarget_IsInvalid()
    {
        var envelope = new LlmActionEnvelope
        {
            Action = "add_order_lines",
            Entity = "order_lines",
            Fields = new JsonObject(),
            Items =
            [
                new LlmOrderItem { Type = "comment", Description = "Handle with care" }
            ],
            MissingFields = [],
            Meta = new LlmMeta { UserConfirmationRequired = false }
        };

        var result = _validator.Validate(envelope);

        Assert.False(result.IsValid);
        Assert.Contains("order_number", string.Join(',', result.Errors));
    }
}
