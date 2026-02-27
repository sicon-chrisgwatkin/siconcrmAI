using Sage200.LocalLlmChat.Core.Parsing;

namespace Sage200.LocalLlmChat.Tests;

public sealed class LlmEnvelopeParserTests
{
    private readonly LlmEnvelopeParser _parser = new();

    [Fact]
    public void Parse_ValidSalesOrderEnvelope_Succeeds()
    {
        const string json =
            """
            {
              "action": "create_sales_order",
              "entity": "sales_order",
              "fields": { "company": "Acme Ltd" },
              "items": [
                { "type": "stock", "stock_code": "ABC-123", "qty": 10 },
                { "type": "additional_charge", "additional_charge_code": "DELIVERY", "unit_price": 25.0 }
              ],
              "missing_fields": [],
              "meta": { "user_confirmation_required": true, "notes": "draft only" }
            }
            """;

        var result = _parser.Parse(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Envelope);
        Assert.Equal("create_sales_order", result.Envelope!.Action);
        Assert.Equal(2, result.Envelope.Items.Count);
    }

    [Fact]
    public void Parse_WithUnknownTopLevelProperty_Fails()
    {
        const string json =
            """
            {
              "action": "create_task",
              "entity": "task",
              "fields": {},
              "items": [],
              "missing_fields": [],
              "meta": { "user_confirmation_required": false },
              "unsupported": true
            }
            """;

        var result = _parser.Parse(json);

        Assert.False(result.IsSuccess);
        Assert.Contains("unsupported", result.ErrorDetails);
    }

    [Fact]
    public void Parse_WhenMetaFlagMissing_Fails()
    {
        const string json =
            """
            {
              "action": "create_task",
              "entity": "task",
              "fields": {},
              "items": [],
              "missing_fields": [],
              "meta": { "notes": "missing confirmation flag" }
            }
            """;

        var result = _parser.Parse(json);

        Assert.False(result.IsSuccess);
        Assert.Contains("user_confirmation_required", result.ErrorMessage);
    }
}
