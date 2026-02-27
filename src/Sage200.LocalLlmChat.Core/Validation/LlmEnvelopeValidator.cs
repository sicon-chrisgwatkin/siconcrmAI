using Sage200.LocalLlmChat.Core.Models;

namespace Sage200.LocalLlmChat.Core.Validation;

public sealed class LlmEnvelopeValidator
{
    public EnvelopeValidationResult Validate(LlmActionEnvelope envelope)
    {
        var errors = new List<string>();
        var missing = envelope.MissingFields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (missing.Length > 0)
        {
            return new EnvelopeValidationResult
            {
                IsValid = true,
                CanExecute = false,
                MissingFields = missing
            };
        }

        if (envelope.Action is "create_sales_order" or "create_purchase_order")
        {
            if (envelope.Items.Count == 0)
            {
                errors.Add("Orders must include at least one line item.");
            }

            if (!envelope.Meta.UserConfirmationRequired)
            {
                errors.Add("Order actions must request explicit user confirmation.");
            }
        }

        if (envelope.Action == "add_order_lines" &&
            !envelope.Fields.ContainsKey("order_number") &&
            !envelope.Fields.ContainsKey("draft_id"))
        {
            errors.Add("add_order_lines requires order_number or draft_id.");
        }

        for (var i = 0; i < envelope.Items.Count; i++)
        {
            var item = envelope.Items[i];

            switch (item.Type)
            {
                case "stock":
                    if (string.IsNullOrWhiteSpace(item.StockCode))
                    {
                        errors.Add($"items[{i}] stock_code is required for stock lines.");
                    }

                    if (!item.Qty.HasValue || item.Qty.Value <= 0)
                    {
                        errors.Add($"items[{i}] qty must be greater than zero for stock lines.");
                    }

                    break;

                case "free_text":
                    if (string.IsNullOrWhiteSpace(item.Description))
                    {
                        errors.Add($"items[{i}] description is required for free_text lines.");
                    }

                    if (item.Qty.HasValue && item.Qty.Value <= 0)
                    {
                        errors.Add($"items[{i}] qty must be greater than zero if supplied for free_text lines.");
                    }

                    break;

                case "additional_charge":
                    if (string.IsNullOrWhiteSpace(item.AdditionalChargeCode))
                    {
                        errors.Add($"items[{i}] additional_charge_code is required for additional_charge lines.");
                    }

                    break;

                case "comment":
                    if (string.IsNullOrWhiteSpace(item.Description))
                    {
                        errors.Add($"items[{i}] description is required for comment lines.");
                    }

                    break;
            }
        }

        return new EnvelopeValidationResult
        {
            IsValid = errors.Count == 0,
            CanExecute = errors.Count == 0,
            Errors = errors
        };
    }
}
