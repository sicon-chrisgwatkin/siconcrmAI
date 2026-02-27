using System.Text.Json.Nodes;
using Sage200.LocalLlmChat.Core.Models;
using Sage200.LocalLlmChat.Infrastructure.Navigation;
using Sage200.LocalLlmChat.Infrastructure.Security;

namespace Sage200.LocalLlmChat.Infrastructure.Services;

public sealed class InMemoryCrmMetadataProvider : ICrmMetadataProvider
{
    private static readonly IReadOnlyDictionary<string, string[]> MandatoryByAction =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["create_task"] = ["subject", "assignee", "due_date"],
            ["create_opportunity"] = ["company", "name", "value"],
            ["create_company"] = ["name"],
            ["create_person"] = ["company", "name", "email"]
        };

    public Task<IReadOnlyList<string>> GetMandatoryFieldsAsync(string action, CancellationToken cancellationToken)
    {
        MandatoryByAction.TryGetValue(action, out var fields);
        return Task.FromResult<IReadOnlyList<string>>(fields ?? Array.Empty<string>());
    }
}

public sealed class InMemoryCrmValidationProbe : ICrmValidationProbe
{
    public Task<IReadOnlyList<string>> ValidateDryRunAsync(
        string action,
        JsonObject proposedFields,
        CancellationToken cancellationToken)
    {
        var dynamicFields = new List<string>();
        if (action.Equals("create_task", StringComparison.OrdinalIgnoreCase) &&
            !proposedFields.ContainsKey("company"))
        {
            dynamicFields.Add("company");
        }

        return Task.FromResult<IReadOnlyList<string>>(dynamicFields);
    }
}

public sealed class InMemorySiconCrmGateway : ISiconCrmGateway
{
    private readonly List<CrmLookupRecord> _companies =
    [
        new() { Id = "CMP-001", Name = "Acme Ltd", Email = "hello@acme.test", Phone = "01234 567890", PostCode = "AB12 3CD" },
        new() { Id = "CMP-002", Name = "ACME London", Email = "london@acme.test", Phone = "0207 111 2222", PostCode = "EC1A 1AA" }
    ];

    private readonly List<CrmLookupRecord> _people =
    [
        new() { Id = "PER-001", Name = "John Smith", Email = "john@acme.test", Phone = "01234 567891", PostCode = "AB12 3CD" }
    ];

    public Task<IReadOnlyList<CrmLookupRecord>> SearchCompaniesAsync(string query, CancellationToken cancellationToken)
    {
        var matches = _companies
            .Where(company => company.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult<IReadOnlyList<CrmLookupRecord>>(matches);
    }

    public Task<IReadOnlyList<CrmLookupRecord>> SearchPeopleAsync(string query, CancellationToken cancellationToken)
    {
        var matches = _people
            .Where(person => person.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult<IReadOnlyList<CrmLookupRecord>>(matches);
    }

    public Task<CreateRecordResult> CreateAsync(string action, JsonObject fields, CancellationToken cancellationToken)
    {
        var entity = action switch
        {
            "create_task" => "task",
            "create_opportunity" => "opportunity",
            "create_company" => "company",
            "create_person" => "person",
            _ => "none"
        };

        var id = $"{entity[..Math.Min(entity.Length, 3)].ToUpperInvariant()}-{Guid.NewGuid():N}"[..12];
        return Task.FromResult(new CreateRecordResult
        {
            Success = true,
            Entity = entity,
            RecordId = id,
            DisplayReference = id
        });
    }
}

public sealed class InMemoryOrdersMetadataProvider : IOrdersMetadataProvider
{
    public Task<IReadOnlyList<string>> GetMandatoryHeaderFieldsAsync(
        string action,
        CancellationToken cancellationToken)
    {
        var fields = action switch
        {
            "create_sales_order" => new[] { "company", "contact", "currency" },
            "create_purchase_order" => new[] { "supplier", "required_by_date" },
            _ => Array.Empty<string>()
        };

        return Task.FromResult<IReadOnlyList<string>>(fields);
    }

    public Task<IReadOnlyList<string>> GetMandatoryLineFieldsAsync(
        string action,
        CancellationToken cancellationToken)
    {
        var fields = action switch
        {
            "create_sales_order" or "create_purchase_order" => new[] { "warehouse", "tax_code" },
            "add_order_lines" => new[] { "warehouse" },
            _ => Array.Empty<string>()
        };

        return Task.FromResult<IReadOnlyList<string>>(fields);
    }
}

public sealed class InMemoryOrdersValidationProbe : IOrdersValidationProbe
{
    public Task<IReadOnlyList<string>> ValidateHeaderDryRunAsync(
        string action,
        JsonObject headerFields,
        CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        if (action == "create_sales_order" && !headerFields.ContainsKey("price_list"))
        {
            missing.Add("price_list");
        }

        return Task.FromResult<IReadOnlyList<string>>(missing);
    }

    public Task<IReadOnlyList<string>> ValidateLinesDryRunAsync(
        string action,
        IReadOnlyList<LlmOrderItem> lines,
        CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        if (lines.Any(line => line.Type == "stock" && string.IsNullOrWhiteSpace(line.Warehouse)))
        {
            missing.Add("warehouse");
        }

        if (lines.Any(line => string.IsNullOrWhiteSpace(line.TaxCode)))
        {
            missing.Add("tax_code");
        }

        return Task.FromResult<IReadOnlyList<string>>(missing);
    }
}

public sealed class InMemoryOrdersLookupValidator : IOrdersLookupValidator
{
    public Task<LookupValidationResult> ValidateAsync(
        string action,
        JsonObject headerFields,
        IReadOnlyList<LlmOrderItem> lines,
        CancellationToken cancellationToken)
    {
        var missing = new List<string>();

        if ((action == "create_sales_order" || action == "create_purchase_order") && lines.Count == 0)
        {
            missing.Add("items");
        }

        if (action == "add_order_lines" &&
            !headerFields.ContainsKey("order_number") &&
            !headerFields.ContainsKey("draft_id"))
        {
            missing.Add("order_number");
        }

        return Task.FromResult(new LookupValidationResult
        {
            MissingFields = missing
        });
    }
}

public sealed class InMemorySageOrdersGateway : ISageOrdersGateway
{
    public Task<CreateRecordResult> CreateOrderFromDraftAsync(
        DraftOrderContext draftContext,
        bool postOrder,
        CancellationToken cancellationToken)
    {
        var entity = draftContext.Action == "create_purchase_order" ? "purchase_order" : "sales_order";
        var idPrefix = entity == "purchase_order" ? "PO" : "SO";
        var id = $"{idPrefix}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        return Task.FromResult(new CreateRecordResult
        {
            Success = true,
            Entity = entity,
            RecordId = id,
            DisplayReference = id
        });
    }

    public Task<CreateRecordResult> AddLinesAsync(
        JsonObject fields,
        IReadOnlyList<LlmOrderItem> lines,
        CancellationToken cancellationToken)
    {
        var order = fields["order_number"]?.GetValue<string?>() ?? fields["draft_id"]?.GetValue<string?>() ?? "UNKNOWN";
        return Task.FromResult(new CreateRecordResult
        {
            Success = true,
            Entity = "sales_order",
            RecordId = order,
            DisplayReference = order
        });
    }
}

public sealed class InMemoryFormLauncher : ISageFormLauncher
{
    public string? LastOpenedEntityId { get; private set; }

    public void OpenTask(string id) => LastOpenedEntityId = $"task:{id}";

    public void OpenOpportunity(string id) => LastOpenedEntityId = $"opportunity:{id}";

    public void OpenCompany(string id) => LastOpenedEntityId = $"company:{id}";

    public void OpenPerson(string id) => LastOpenedEntityId = $"person:{id}";

    public void OpenSalesOrder(string id) => LastOpenedEntityId = $"sales_order:{id}";

    public void OpenPurchaseOrder(string id) => LastOpenedEntityId = $"purchase_order:{id}";
}

public sealed class AllowAllPermissionGateway : ISagePermissionGateway
{
    public Task<bool> HasPermissionAsync(string action, CancellationToken cancellationToken) =>
        Task.FromResult(true);
}
