using Sage200.LocalLlmChat.Core.Abstractions;
using Sage200.LocalLlmChat.Core.Models;
using Sage200.LocalLlmChat.Core.Parsing;
using Sage200.LocalLlmChat.Core.Validation;

namespace Sage200.LocalLlmChat.Core.Services;

public sealed class ChatOrchestrator
{
    private readonly ILocalLlmClient _llmClient;
    private readonly ICrmService _crmService;
    private readonly IOrdersService _ordersService;
    private readonly IClarificationQuestionManager _clarificationQuestionManager;
    private readonly IRecordNavigator _recordNavigator;
    private readonly IUserPermissionService _permissionService;
    private readonly IChatLogger _logger;
    private readonly LlmEnvelopeParser _parser;
    private readonly LlmEnvelopeValidator _validator;

    public ChatOrchestrator(
        ILocalLlmClient llmClient,
        ICrmService crmService,
        IOrdersService ordersService,
        IClarificationQuestionManager clarificationQuestionManager,
        IRecordNavigator recordNavigator,
        IUserPermissionService permissionService,
        IChatLogger logger,
        LlmEnvelopeParser? parser = null,
        LlmEnvelopeValidator? validator = null)
    {
        _llmClient = llmClient;
        _crmService = crmService;
        _ordersService = ordersService;
        _clarificationQuestionManager = clarificationQuestionManager;
        _recordNavigator = recordNavigator;
        _permissionService = permissionService;
        _logger = logger;
        _parser = parser ?? new LlmEnvelopeParser();
        _validator = validator ?? new LlmEnvelopeValidator();
    }

    public async Task<ChatTurnResult> HandleUserInputAsync(
        string userMessage,
        IReadOnlyList<ChatMessage> conversation,
        ChatSessionContext sessionContext,
        CancellationToken cancellationToken)
    {
        var fullConversation = new List<ChatMessage>(conversation)
        {
            new()
            {
                Role = "user",
                Content = userMessage,
                Timestamp = DateTimeOffset.UtcNow
            }
        };
        var prompt = SystemPromptFactory.BuildPrivacyFirstPrompt();
        _logger.Info("Sending prompt to local LLM.", new
        {
            MessageCount = fullConversation.Count,
            LastUserMessage = userMessage
        });

        LlmInferenceResult inference;
        try
        {
            inference = await _llmClient.GenerateAsync(
                fullConversation,
                prompt,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error("Local LLM request failed.", ex);
            return new ChatTurnResult
            {
                Success = false,
                ShowErrorBanner = true,
                UserFacingMessage = "The local LLM is unavailable right now. Check Ollama/LM Studio runtime status and try again.",
                TechnicalDetails = ex.Message
            };
        }

        _logger.Info("LLM raw response generated.", new { inference.ModelName, inference.Duration });
        _logger.Info("LLM JSON candidate received.", new { inference.RawResponse });

        var parseResult = _parser.Parse(inference.RawResponse);
        if (!parseResult.IsSuccess || parseResult.Envelope is null)
        {
            _logger.Warning("LLM response could not be parsed.", new { parseResult.ErrorMessage, parseResult.ErrorDetails });
            return new ChatTurnResult
            {
                Success = false,
                ShowErrorBanner = true,
                UserFacingMessage = "I couldn't read the model response as valid action JSON. You can retry or regenerate.",
                TechnicalDetails = $"{parseResult.ErrorMessage} {parseResult.ErrorDetails}".Trim()
            };
        }

        var envelope = parseResult.Envelope;
        _logger.Info("Parsed LLM envelope.", new { envelope.Action, envelope.Entity, Missing = envelope.MissingFields.Count });

        var validationResult = _validator.Validate(envelope);
        if (!validationResult.IsValid)
        {
            return new ChatTurnResult
            {
                Success = false,
                ShowErrorBanner = true,
                UserFacingMessage = "The model response failed safety validation. No action was executed.",
                TechnicalDetails = string.Join("; ", validationResult.Errors)
            };
        }

        if (!validationResult.CanExecute)
        {
            var questions = _clarificationQuestionManager.BuildQuestions(validationResult.MissingFields, envelope);
            return new ChatTurnResult
            {
                Success = true,
                ShowConfirmButtons = false,
                UserFacingMessage = string.Join(Environment.NewLine, questions),
                ClarificationQuestions = questions
            };
        }

        switch (envelope.Action)
        {
            case "cancel":
                return CancelCurrentAction(sessionContext);

            case "confirm_order":
                return await ConfirmPendingOrderAsync(sessionContext, postOrder: false, cancellationToken);

            case "create_task":
            case "create_opportunity":
            case "create_company":
            case "create_person":
                return await ExecuteCrmCreateAsync(envelope, cancellationToken);

            case "create_sales_order":
            case "create_purchase_order":
                return await BuildOrderDraftAsync(envelope, sessionContext, cancellationToken);

            case "add_order_lines":
                return await AddOrderLinesAsync(envelope, cancellationToken);

            default:
                return new ChatTurnResult
                {
                    Success = false,
                    ShowErrorBanner = true,
                    UserFacingMessage = "The requested action is not currently supported.",
                    TechnicalDetails = envelope.Action
                };
        }
    }

    public Task<ChatTurnResult> ConfirmPendingOrderDirectAsync(
        ChatSessionContext sessionContext,
        CancellationToken cancellationToken,
        bool postOrder = false) =>
        ConfirmPendingOrderAsync(sessionContext, postOrder, cancellationToken);

    public ChatTurnResult CancelCurrentAction(ChatSessionContext sessionContext)
    {
        sessionContext.PendingOrder = null;
        return new ChatTurnResult
        {
            Success = true,
            UserFacingMessage = "Okay, I have cancelled the current action."
        };
    }

    private async Task<ChatTurnResult> ExecuteCrmCreateAsync(
        LlmActionEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (!await _permissionService.HasPermissionAsync(envelope.Action, cancellationToken))
        {
            return AccessDenied(envelope.Action);
        }

        var platformMandatory = await _crmService.ResolveMandatoryFieldsAsync(
            envelope.Action,
            envelope.Fields,
            cancellationToken);

        if (platformMandatory.Count > 0)
        {
            var questions = _clarificationQuestionManager.BuildQuestions(platformMandatory, envelope);
            return new ChatTurnResult
            {
                Success = true,
                UserFacingMessage = string.Join(Environment.NewLine, questions),
                ClarificationQuestions = questions
            };
        }

        if (envelope.Action == "create_company")
        {
            var duplicates = await _crmService.DetectLikelyCompanyDuplicatesAsync(envelope.Fields, cancellationToken);
            if (duplicates.HasLikelyDuplicates)
            {
                var options = string.Join(", ", duplicates.Candidates.Select(c => c.DisplayName));
                return new ChatTurnResult
                {
                    Success = true,
                    UserFacingMessage = $"I found possible duplicate companies: {options}. Link to an existing record or say 'create anyway'."
                };
            }
        }

        if (envelope.Action == "create_person")
        {
            var duplicates = await _crmService.DetectLikelyPersonDuplicatesAsync(envelope.Fields, cancellationToken);
            if (duplicates.HasLikelyDuplicates)
            {
                var options = string.Join(", ", duplicates.Candidates.Select(c => c.DisplayName));
                return new ChatTurnResult
                {
                    Success = true,
                    UserFacingMessage = $"I found possible duplicate people: {options}. Link to an existing record or say 'create anyway'."
                };
            }
        }

        var createResult = await _crmService.CreateAsync(envelope.Action, envelope.Fields, cancellationToken);
        if (!createResult.Success)
        {
            return new ChatTurnResult
            {
                Success = false,
                ShowErrorBanner = true,
                UserFacingMessage = "The CRM action failed.",
                TechnicalDetails = createResult.ErrorMessage
            };
        }

        _recordNavigator.OpenRecord(createResult.Entity, createResult.RecordId);

        return new ChatTurnResult
        {
            Success = true,
            UserFacingMessage = $"Done. Created {createResult.Entity} {createResult.DisplayReference ?? createResult.RecordId}.",
            CreatedEntity = createResult.Entity,
            CreatedRecordId = createResult.RecordId
        };
    }

    private async Task<ChatTurnResult> BuildOrderDraftAsync(
        LlmActionEnvelope envelope,
        ChatSessionContext sessionContext,
        CancellationToken cancellationToken)
    {
        if (!await _permissionService.HasPermissionAsync(envelope.Action, cancellationToken))
        {
            return AccessDenied(envelope.Action);
        }

        var headerMissing = await _ordersService.ResolveMandatoryHeaderFieldsAsync(
            envelope.Action,
            envelope.Fields,
            cancellationToken);

        var lineMissing = await _ordersService.ResolveMandatoryLineFieldsAsync(
            envelope.Action,
            envelope.Items,
            cancellationToken);

        var combinedMissing = headerMissing.Concat(lineMissing)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (combinedMissing.Length > 0)
        {
            var questions = _clarificationQuestionManager.BuildQuestions(combinedMissing, envelope);
            return new ChatTurnResult
            {
                Success = true,
                UserFacingMessage = string.Join(Environment.NewLine, questions),
                ClarificationQuestions = questions
            };
        }

        var draftResult = await _ordersService.BuildDraftAsync(
            envelope.Action,
            envelope.Fields,
            envelope.Items,
            cancellationToken);

        if (!draftResult.Success || draftResult.Draft is null)
        {
            return new ChatTurnResult
            {
                Success = false,
                ShowErrorBanner = true,
                UserFacingMessage = "I couldn't build the order draft.",
                TechnicalDetails = draftResult.ErrorMessage
            };
        }

        sessionContext.PendingOrder = draftResult.Draft;

        return new ChatTurnResult
        {
            Success = true,
            ShowConfirmButtons = true,
            UserFacingMessage = "I have prepared a draft order. Please review and click Confirm to create it.",
            PreviewCard = draftResult.Draft.Summary
        };
    }

    private async Task<ChatTurnResult> AddOrderLinesAsync(
        LlmActionEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (!await _permissionService.HasPermissionAsync(envelope.Action, cancellationToken))
        {
            return AccessDenied(envelope.Action);
        }

        var missing = await _ordersService.ResolveMandatoryLineFieldsAsync(
            envelope.Action,
            envelope.Items,
            cancellationToken);

        if (missing.Count > 0)
        {
            var questions = _clarificationQuestionManager.BuildQuestions(missing, envelope);
            return new ChatTurnResult
            {
                Success = true,
                UserFacingMessage = string.Join(Environment.NewLine, questions),
                ClarificationQuestions = questions
            };
        }

        var result = await _ordersService.AddLinesAsync(envelope.Fields, envelope.Items, cancellationToken);
        if (!result.Success)
        {
            return new ChatTurnResult
            {
                Success = false,
                ShowErrorBanner = true,
                UserFacingMessage = "I couldn't add the lines to the order.",
                TechnicalDetails = result.ErrorMessage
            };
        }

        _recordNavigator.OpenRecord(result.Entity, result.RecordId);

        return new ChatTurnResult
        {
            Success = true,
            UserFacingMessage = $"Order updated successfully ({result.DisplayReference ?? result.RecordId}).",
            CreatedEntity = result.Entity,
            CreatedRecordId = result.RecordId
        };
    }

    private async Task<ChatTurnResult> ConfirmPendingOrderAsync(
        ChatSessionContext sessionContext,
        bool postOrder,
        CancellationToken cancellationToken)
    {
        if (sessionContext.PendingOrder is null)
        {
            return new ChatTurnResult
            {
                Success = true,
                UserFacingMessage = "There is no pending draft order to confirm."
            };
        }

        if (!await _permissionService.HasPermissionAsync(sessionContext.PendingOrder.Action, cancellationToken))
        {
            return AccessDenied(sessionContext.PendingOrder.Action);
        }

        var createResult = await _ordersService.ConfirmDraftAsync(
            sessionContext.PendingOrder,
            postOrder,
            cancellationToken);

        if (!createResult.Success)
        {
            return new ChatTurnResult
            {
                Success = false,
                ShowErrorBanner = true,
                UserFacingMessage = "I couldn't create the order from the draft.",
                TechnicalDetails = createResult.ErrorMessage
            };
        }

        sessionContext.PendingOrder = null;
        _recordNavigator.OpenRecord(createResult.Entity, createResult.RecordId);

        return new ChatTurnResult
        {
            Success = true,
            UserFacingMessage = $"Order created: {createResult.DisplayReference ?? createResult.RecordId}.",
            CreatedEntity = createResult.Entity,
            CreatedRecordId = createResult.RecordId
        };
    }

    private static ChatTurnResult AccessDenied(string action) =>
        new()
        {
            Success = false,
            ShowErrorBanner = true,
            UserFacingMessage = $"You do not have permission to run '{action}'.",
            TechnicalDetails = "Permission denied by IUserPermissionService."
        };
}
