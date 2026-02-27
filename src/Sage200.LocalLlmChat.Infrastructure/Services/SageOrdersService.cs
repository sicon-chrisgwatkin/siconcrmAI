using System.Text.Json.Nodes;
using Sage200.LocalLlmChat.Core.Abstractions;
using Sage200.LocalLlmChat.Core.Models;

namespace Sage200.LocalLlmChat.Infrastructure.Services;

public interface IOrdersMetadataProvider
{
    Task<IReadOnlyList<string>> GetMandatoryHeaderFieldsAsync(
        string action,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetMandatoryLineFieldsAsync(
        string action,
        CancellationToken cancellationToken);
}

public interface IOrdersValidationProbe
{
    Task<IReadOnlyList<string>> ValidateHeaderDryRunAsync(
        string action,
        JsonObject headerFields,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ValidateLinesDryRunAsync(
        string action,
        IReadOnlyList<LlmOrderItem> lines,
        CancellationToken cancellationToken);
}

public interface IOrdersLookupValidator
{
    Task<LookupValidationResult> ValidateAsync(
        string action,
        JsonObject headerFields,
        IReadOnlyList<LlmOrderItem> lines,
        CancellationToken cancellationToken);
}

public interface ISageOrdersGateway
{
    Task<CreateRecordResult> CreateOrderFromDraftAsync(
        DraftOrderContext draftContext,
        bool postOrder,
        CancellationToken cancellationToken);

    Task<CreateRecordResult> AddLinesAsync(
        JsonObject fields,
        IReadOnlyList<LlmOrderItem> lines,
        CancellationToken cancellationToken);
}

public sealed class LookupValidationResult
{
    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class SageOrdersService : IOrdersService
{
    private readonly IOrdersMetadataProvider _metadataProvider;
    private readonly IOrdersValidationProbe _validationProbe;
    private readonly IOrdersLookupValidator _lookupValidator;
    private readonly ISageOrdersGateway _gateway;
    private readonly IChatLogger? _logger;

    public SageOrdersService(
        IOrdersMetadataProvider metadataProvider,
        IOrdersValidationProbe validationProbe,
        IOrdersLookupValidator lookupValidator,
        ISageOrdersGateway gateway,
        IChatLogger? logger = null)
    {
        _metadataProvider = metadataProvider;
        _validationProbe = validationProbe;
        _lookupValidator = lookupValidator;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> ResolveMandatoryHeaderFieldsAsync(
        string action,
        JsonObject headerFields,
        CancellationToken cancellationToken)
    {
        _logger?.Info("Resolving mandatory order header fields.", new { action, headerFields });
        var metadataFields = await _metadataProvider.GetMandatoryHeaderFieldsAsync(action, cancellationToken);
        var dryRunFields = await _validationProbe.ValidateHeaderDryRunAsync(action, headerFields, cancellationToken);
        var missing = metadataFields
            .Concat(dryRunFields)
            .Where(field => !headerFields.ContainsKey(field))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _logger?.Info("Resolved mandatory order header fields.", new { action, missing });
        return missing;
    }

    public async Task<IReadOnlyList<string>> ResolveMandatoryLineFieldsAsync(
        string action,
        IReadOnlyList<LlmOrderItem> lines,
        CancellationToken cancellationToken)
    {
        _logger?.Info("Resolving mandatory order line fields.", new { action, lineCount = lines.Count });
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var metadataFields = await _metadataProvider.GetMandatoryLineFieldsAsync(action, cancellationToken);
        var dryRunFields = await _validationProbe.ValidateLinesDryRunAsync(action, lines, cancellationToken);
        fields.UnionWith(metadataFields);
        fields.UnionWith(dryRunFields);

        foreach (var line in lines)
        {
            if (line.Type == "stock" && string.IsNullOrWhiteSpace(line.StockCode))
            {
                fields.Add("stock_code");
            }

            if (line.Type == "stock" && (!line.Qty.HasValue || line.Qty.Value <= 0))
            {
                fields.Add("qty");
            }

            if (line.Type == "free_text" && string.IsNullOrWhiteSpace(line.Description))
            {
                fields.Add("description");
            }

            if (line.Type == "additional_charge" && string.IsNullOrWhiteSpace(line.AdditionalChargeCode))
            {
                fields.Add("additional_charge_code");
            }

            if (line.Type == "comment" && string.IsNullOrWhiteSpace(line.Description))
            {
                fields.Add("description");
            }
        }

        var missing = fields.ToArray();
        _logger?.Info("Resolved mandatory order line fields.", new { action, missing });
        return missing;
    }

    public async Task<OrderDraftBuildResult> BuildDraftAsync(
        string action,
        JsonObject headerFields,
        IReadOnlyList<LlmOrderItem> lines,
        CancellationToken cancellationToken)
    {
        _logger?.Info("Building order draft.", new { action, headerFields, lineCount = lines.Count });
        var lookup = await _lookupValidator.ValidateAsync(action, headerFields, lines, cancellationToken);
        if (lookup.Errors.Count > 0)
        {
            return new OrderDraftBuildResult
            {
                Success = false,
                ErrorMessage = string.Join("; ", lookup.Errors)
            };
        }

        if (lookup.MissingFields.Count > 0)
        {
            return new OrderDraftBuildResult
            {
                Success = false,
                MissingFields = lookup.MissingFields,
                ErrorMessage = "Lookup validation found missing fields."
            };
        }

        var summary = BuildSummary(action, headerFields, lines);
        _logger?.Info("Order draft built.", new { action, summary });
        return new OrderDraftBuildResult
        {
            Success = true,
            Draft = new DraftOrderContext
            {
                Action = action,
                HeaderFields = headerFields,
                Items = lines,
                Summary = summary
            }
        };
    }

    public Task<CreateRecordResult> ConfirmDraftAsync(
        DraftOrderContext draftContext,
        bool postOrder,
        CancellationToken cancellationToken)
    {
        _logger?.Info("Confirming order draft.", new { draftContext.DraftId, draftContext.Action, postOrder });
        return _gateway.CreateOrderFromDraftAsync(draftContext, postOrder, cancellationToken);
    }

    public async Task<CreateRecordResult> AddLinesAsync(
        JsonObject fields,
        IReadOnlyList<LlmOrderItem> lines,
        CancellationToken cancellationToken)
    {
        _logger?.Info("Adding order lines.", new { fields, lineCount = lines.Count });
        var lookup = await _lookupValidator.ValidateAsync("add_order_lines", fields, lines, cancellationToken);
        if (lookup.Errors.Count > 0)
        {
            return new CreateRecordResult
            {
                Success = false,
                ErrorMessage = string.Join("; ", lookup.Errors)
            };
        }

        if (lookup.MissingFields.Count > 0)
        {
            return new CreateRecordResult
            {
                Success = false,
                ErrorMessage = $"Missing lookup fields: {string.Join(", ", lookup.MissingFields)}"
            };
        }

        return await _gateway.AddLinesAsync(fields, lines, cancellationToken);
    }

    private static string BuildSummary(string action, JsonObject headerFields, IReadOnlyList<LlmOrderItem> lines)
    {
        var counterparty = headerFields["company"]?.GetValue<string?>() ??
                           headerFields["supplier"]?.GetValue<string?>() ??
                           "Unknown";

        var lineCount = lines.Count;
        var estimatedTotal = lines
            .Where(line => line.Qty.HasValue && line.UnitPrice.HasValue)
            .Sum(line => line.Qty!.Value * line.UnitPrice!.Value);

        return $"{action}: {counterparty} | {lineCount} line(s) | estimated total {estimatedTotal:0.00}";
    }
}
