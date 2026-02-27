using Sage200.LocalLlmChat.Core.Abstractions;
using Sage200.LocalLlmChat.Core.Models;

namespace Sage200.LocalLlmChat.Core.Services;

public sealed class ClarificationQuestionManager : IClarificationQuestionManager
{
    private static readonly IReadOnlyDictionary<string, string> QuestionTemplates =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["company"] = "Which company is this for?",
            ["contact"] = "Which contact/person should I use?",
            ["assignee"] = "Who should this be assigned to?",
            ["due_date"] = "When should this be due?",
            ["warehouse"] = "What warehouse should we use?",
            ["tax_code"] = "Which tax code should I apply?",
            ["additional_charge_code"] = "Which additional charge code should I use?",
            ["currency"] = "Order currency is missing. Should I use the company default currency?",
            ["order_number"] = "Which order should I add these lines to?",
            ["draft_id"] = "Which draft order should I update?",
            ["supplier"] = "Which supplier should this purchase order be for?",
            ["delivery_address"] = "What delivery address should I use?",
            ["price_list"] = "Which price list should be used?",
            ["payment_terms"] = "What payment terms should be used?"
        };

    public IReadOnlyList<string> BuildQuestions(IReadOnlyList<string> missingFields, LlmActionEnvelope envelope)
    {
        if (missingFields.Count == 0)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>(missingFields.Count);
        foreach (var missingField in missingFields.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (QuestionTemplates.TryGetValue(missingField, out var template))
            {
                result.Add(template);
            }
            else
            {
                result.Add($"Please provide '{missingField}'.");
            }
        }

        if (envelope.Action is "create_sales_order" or "create_purchase_order")
        {
            result.Add("Once the details are complete I will show a summary before you confirm creation.");
        }

        return result;
    }
}
