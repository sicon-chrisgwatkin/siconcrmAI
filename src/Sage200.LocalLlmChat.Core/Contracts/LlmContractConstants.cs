namespace Sage200.LocalLlmChat.Core.Contracts;

public static class LlmContractConstants
{
    public static readonly ISet<string> AllowedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "create_task",
        "create_opportunity",
        "create_company",
        "create_person",
        "create_sales_order",
        "create_purchase_order",
        "add_order_lines",
        "confirm_order",
        "cancel"
    };

    public static readonly ISet<string> AllowedEntities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "task",
        "opportunity",
        "company",
        "person",
        "sales_order",
        "purchase_order",
        "order_lines",
        "none"
    };

    public static readonly ISet<string> AllowedItemTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "stock",
        "free_text",
        "additional_charge",
        "comment"
    };
}
