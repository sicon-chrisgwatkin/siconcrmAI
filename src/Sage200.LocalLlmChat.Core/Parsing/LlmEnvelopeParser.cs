using System.Text.Json;
using System.Text.Json.Nodes;
using Sage200.LocalLlmChat.Core.Contracts;
using Sage200.LocalLlmChat.Core.Models;

namespace Sage200.LocalLlmChat.Core.Parsing;

public sealed class LlmEnvelopeParser
{
    private static readonly ISet<string> RootKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "action",
        "entity",
        "fields",
        "items",
        "missing_fields",
        "meta"
    };

    private static readonly ISet<string> ItemKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "type",
        "stock_code",
        "description",
        "qty",
        "unit_price",
        "uom",
        "warehouse",
        "additional_charge_code",
        "tax_code",
        "discount_percent"
    };

    private static readonly ISet<string> MetaKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "user_confirmation_required",
        "notes"
    };

    public EnvelopeParseResult Parse(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return EnvelopeParseResult.Failure("LLM response was empty.");
        }

        JsonNode? parsedNode;
        try
        {
            parsedNode = JsonNode.Parse(rawJson, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        }
        catch (Exception ex)
        {
            return EnvelopeParseResult.Failure("LLM response was not valid JSON.", ex.Message);
        }

        if (parsedNode is not JsonObject root)
        {
            return EnvelopeParseResult.Failure("LLM response must be a single JSON object.");
        }

        var unknownRootKeys = root.Select(kvp => kvp.Key).Where(key => !RootKeys.Contains(key)).ToArray();
        if (unknownRootKeys.Length > 0)
        {
            return EnvelopeParseResult.Failure(
                "LLM JSON contained unsupported top-level properties.",
                $"Unsupported properties: {string.Join(", ", unknownRootKeys)}");
        }

        foreach (var requiredKey in RootKeys)
        {
            if (!root.ContainsKey(requiredKey))
            {
                return EnvelopeParseResult.Failure(
                    "LLM JSON did not include all required top-level properties.",
                    $"Missing property: {requiredKey}");
            }
        }

        if (!TryGetString(root["action"], out var action, out var actionError))
        {
            return EnvelopeParseResult.Failure("Invalid action value.", actionError);
        }

        if (!LlmContractConstants.AllowedActions.Contains(action))
        {
            return EnvelopeParseResult.Failure("Unsupported action in LLM JSON.", action);
        }

        if (!TryGetString(root["entity"], out var entity, out var entityError))
        {
            return EnvelopeParseResult.Failure("Invalid entity value.", entityError);
        }

        if (!LlmContractConstants.AllowedEntities.Contains(entity))
        {
            return EnvelopeParseResult.Failure("Unsupported entity in LLM JSON.", entity);
        }

        if (root["fields"] is not JsonObject fields)
        {
            return EnvelopeParseResult.Failure("fields must be a JSON object.");
        }

        if (root["missing_fields"] is not JsonArray missingFieldsNode)
        {
            return EnvelopeParseResult.Failure("missing_fields must be a string array.");
        }

        var missingFields = new List<string>(missingFieldsNode.Count);
        foreach (var node in missingFieldsNode)
        {
            if (!TryGetString(node, out var fieldName, out var fieldError))
            {
                return EnvelopeParseResult.Failure("missing_fields contained a non-string value.", fieldError);
            }

            missingFields.Add(fieldName);
        }

        if (root["items"] is not JsonArray itemsNode)
        {
            return EnvelopeParseResult.Failure("items must be an array.");
        }

        var items = new List<LlmOrderItem>(itemsNode.Count);
        for (var i = 0; i < itemsNode.Count; i++)
        {
            if (itemsNode[i] is not JsonObject itemObject)
            {
                return EnvelopeParseResult.Failure($"Item at index {i} must be an object.");
            }

            var unknownItemKeys = itemObject.Select(kvp => kvp.Key).Where(key => !ItemKeys.Contains(key)).ToArray();
            if (unknownItemKeys.Length > 0)
            {
                return EnvelopeParseResult.Failure(
                    $"Item at index {i} has unsupported properties.",
                    string.Join(", ", unknownItemKeys));
            }

            if (!TryGetString(itemObject["type"], out var type, out var typeError))
            {
                return EnvelopeParseResult.Failure($"Item at index {i} has invalid type.", typeError);
            }

            if (!LlmContractConstants.AllowedItemTypes.Contains(type))
            {
                return EnvelopeParseResult.Failure($"Item at index {i} has unsupported type.", type);
            }

            items.Add(new LlmOrderItem
            {
                Type = type,
                StockCode = GetOptionalString(itemObject["stock_code"]),
                Description = GetOptionalString(itemObject["description"]),
                Qty = GetOptionalDecimal(itemObject["qty"]),
                UnitPrice = GetOptionalDecimal(itemObject["unit_price"]),
                Uom = GetOptionalString(itemObject["uom"]),
                Warehouse = GetOptionalString(itemObject["warehouse"]),
                AdditionalChargeCode = GetOptionalString(itemObject["additional_charge_code"]),
                TaxCode = GetOptionalString(itemObject["tax_code"]),
                DiscountPercent = GetOptionalDecimal(itemObject["discount_percent"])
            });
        }

        if (root["meta"] is not JsonObject metaObject)
        {
            return EnvelopeParseResult.Failure("meta must be an object.");
        }

        var unknownMetaKeys = metaObject.Select(kvp => kvp.Key).Where(key => !MetaKeys.Contains(key)).ToArray();
        if (unknownMetaKeys.Length > 0)
        {
            return EnvelopeParseResult.Failure(
                "meta contained unsupported properties.",
                string.Join(", ", unknownMetaKeys));
        }

        if (!metaObject.ContainsKey("user_confirmation_required"))
        {
            return EnvelopeParseResult.Failure("meta.user_confirmation_required is required.");
        }

        if (!TryGetBoolean(metaObject["user_confirmation_required"], out var confirmationRequired, out var confirmationError))
        {
            return EnvelopeParseResult.Failure("meta.user_confirmation_required must be true/false.", confirmationError);
        }

        var envelope = new LlmActionEnvelope
        {
            Action = action,
            Entity = entity,
            Fields = fields,
            Items = items,
            MissingFields = missingFields,
            Meta = new LlmMeta
            {
                UserConfirmationRequired = confirmationRequired,
                Notes = GetOptionalString(metaObject["notes"])
            }
        };

        return EnvelopeParseResult.Success(envelope);
    }

    private static bool TryGetString(JsonNode? node, out string value, out string? error)
    {
        value = string.Empty;
        error = null;

        if (node is null)
        {
            error = "Property was null.";
            return false;
        }

        if (node is not JsonValue jsonValue || !jsonValue.TryGetValue<string>(out var parsed) || string.IsNullOrWhiteSpace(parsed))
        {
            error = "Property must be a non-empty string.";
            return false;
        }

        value = parsed;
        return true;
    }

    private static string? GetOptionalString(JsonNode? node)
    {
        if (node is not JsonValue jsonValue)
        {
            return null;
        }

        return jsonValue.TryGetValue<string>(out var value) ? value : null;
    }

    private static decimal? GetOptionalDecimal(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        return node switch
        {
            JsonValue value when value.TryGetValue<decimal>(out var decimalValue) => decimalValue,
            JsonValue value when value.TryGetValue<double>(out var doubleValue) => Convert.ToDecimal(doubleValue),
            JsonValue value when value.TryGetValue<int>(out var intValue) => intValue,
            JsonValue value when value.TryGetValue<long>(out var longValue) => longValue,
            JsonValue value when value.TryGetValue<string>(out var stringValue) && decimal.TryParse(stringValue, out var parsedDecimal) => parsedDecimal,
            _ => null
        };
    }

    private static bool TryGetBoolean(JsonNode? node, out bool value, out string? error)
    {
        value = false;
        error = null;

        if (node is null)
        {
            error = "Property was null.";
            return false;
        }

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<bool>(out var boolValue))
            {
                value = boolValue;
                return true;
            }

            if (jsonValue.TryGetValue<string>(out var boolText) && bool.TryParse(boolText, out var parsed))
            {
                value = parsed;
                return true;
            }
        }

        error = "Property was not boolean.";
        return false;
    }
}
