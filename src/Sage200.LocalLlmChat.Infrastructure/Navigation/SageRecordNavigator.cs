using Sage200.LocalLlmChat.Core.Abstractions;

namespace Sage200.LocalLlmChat.Infrastructure.Navigation;

public interface ISageFormLauncher
{
    void OpenTask(string id);

    void OpenOpportunity(string id);

    void OpenCompany(string id);

    void OpenPerson(string id);

    void OpenSalesOrder(string id);

    void OpenPurchaseOrder(string id);
}

public sealed class SageRecordNavigator : IRecordNavigator
{
    private readonly ISageFormLauncher _formLauncher;

    public SageRecordNavigator(ISageFormLauncher formLauncher)
    {
        _formLauncher = formLauncher;
    }

    public void OpenRecord(string entity, string recordId)
    {
        switch (entity.ToLowerInvariant())
        {
            case "task":
                _formLauncher.OpenTask(recordId);
                break;
            case "opportunity":
                _formLauncher.OpenOpportunity(recordId);
                break;
            case "company":
                _formLauncher.OpenCompany(recordId);
                break;
            case "person":
                _formLauncher.OpenPerson(recordId);
                break;
            case "sales_order":
                _formLauncher.OpenSalesOrder(recordId);
                break;
            case "purchase_order":
                _formLauncher.OpenPurchaseOrder(recordId);
                break;
            case "order_lines":
                _formLauncher.OpenSalesOrder(recordId);
                break;
            default:
                break;
        }
    }
}
