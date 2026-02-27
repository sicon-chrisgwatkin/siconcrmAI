using System.Text.Json.Nodes;
using Sage200.LocalLlmChat.Core.Abstractions;
using Sage200.LocalLlmChat.Core.Models;

namespace Sage200.LocalLlmChat.Infrastructure.Services;

public interface ICrmMetadataProvider
{
    Task<IReadOnlyList<string>> GetMandatoryFieldsAsync(
        string action,
        CancellationToken cancellationToken);
}

public interface ICrmValidationProbe
{
    Task<IReadOnlyList<string>> ValidateDryRunAsync(
        string action,
        JsonObject proposedFields,
        CancellationToken cancellationToken);
}

public interface ISiconCrmGateway
{
    Task<IReadOnlyList<CrmLookupRecord>> SearchCompaniesAsync(string query, CancellationToken cancellationToken);

    Task<IReadOnlyList<CrmLookupRecord>> SearchPeopleAsync(string query, CancellationToken cancellationToken);

    Task<CreateRecordResult> CreateAsync(string action, JsonObject fields, CancellationToken cancellationToken);
}

public sealed class CrmLookupRecord
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? PostCode { get; init; }
}

public sealed class SiconCrmService : ICrmService
{
    private readonly ICrmMetadataProvider _metadataProvider;
    private readonly ICrmValidationProbe _validationProbe;
    private readonly ISiconCrmGateway _gateway;
    private readonly IChatLogger? _logger;

    public SiconCrmService(
        ICrmMetadataProvider metadataProvider,
        ICrmValidationProbe validationProbe,
        ISiconCrmGateway gateway,
        IChatLogger? logger = null)
    {
        _metadataProvider = metadataProvider;
        _validationProbe = validationProbe;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> ResolveMandatoryFieldsAsync(
        string action,
        JsonObject proposedFields,
        CancellationToken cancellationToken)
    {
        _logger?.Info("Resolving CRM mandatory fields.", new { action, proposedFields });
        // Mandatory fields are resolved dynamically using both metadata and dry-run validation errors.
        var fromMetadata = await _metadataProvider.GetMandatoryFieldsAsync(action, cancellationToken);
        var fromDryRun = await _validationProbe.ValidateDryRunAsync(action, proposedFields, cancellationToken);
        var missing = fromMetadata
            .Concat(fromDryRun)
            .Where(field => !proposedFields.ContainsKey(field))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _logger?.Info("Resolved CRM mandatory fields.", new { action, missing });
        return missing;
    }

    public async Task<DuplicateDetectionResult> DetectLikelyCompanyDuplicatesAsync(
        JsonObject proposedFields,
        CancellationToken cancellationToken)
    {
        var name = proposedFields["name"]?.GetValue<string?>() ?? proposedFields["company"]?.GetValue<string?>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return new DuplicateDetectionResult();
        }

        var matches = await _gateway.SearchCompaniesAsync(name!, cancellationToken);
        return BuildDuplicateResult(matches, proposedFields);
    }

    public async Task<DuplicateDetectionResult> DetectLikelyPersonDuplicatesAsync(
        JsonObject proposedFields,
        CancellationToken cancellationToken)
    {
        var name = proposedFields["name"]?.GetValue<string?>() ?? proposedFields["person"]?.GetValue<string?>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return new DuplicateDetectionResult();
        }

        var matches = await _gateway.SearchPeopleAsync(name!, cancellationToken);
        return BuildDuplicateResult(matches, proposedFields);
    }

    public Task<CreateRecordResult> CreateAsync(
        string action,
        JsonObject fields,
        CancellationToken cancellationToken)
    {
        _logger?.Info("Creating CRM record.", new { action, fields });
        return _gateway.CreateAsync(action, fields, cancellationToken);
    }

    private static DuplicateDetectionResult BuildDuplicateResult(
        IReadOnlyList<CrmLookupRecord> candidates,
        JsonObject proposedFields)
    {
        var normalizedEmail = (proposedFields["email"]?.GetValue<string?>() ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedPhone = NormalizeDigits(proposedFields["phone"]?.GetValue<string?>());
        var normalizedPostCode = NormalizeText(proposedFields["postcode"]?.GetValue<string?>() ?? proposedFields["post_code"]?.GetValue<string?>());

        var likely = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score =
                    (normalizedEmail.Length > 0 && NormalizeText(candidate.Email) == normalizedEmail ? 3 : 0) +
                    (normalizedPhone.Length > 0 && NormalizeDigits(candidate.Phone) == normalizedPhone ? 2 : 0) +
                    (normalizedPostCode.Length > 0 && NormalizeText(candidate.PostCode) == normalizedPostCode ? 1 : 0)
            })
            .Where(row => row.Score > 0)
            .OrderByDescending(row => row.Score)
            .Take(5)
            .Select(row => new LookupCandidate
            {
                Id = row.Candidate.Id,
                DisplayName = row.Candidate.Name
            })
            .ToArray();

        return new DuplicateDetectionResult
        {
            HasLikelyDuplicates = likely.Length > 0,
            Candidates = likely
        };
    }

    private static string NormalizeDigits(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsDigit).ToArray());

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant();
    }
}
