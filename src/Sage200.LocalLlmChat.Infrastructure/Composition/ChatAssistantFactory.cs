using Sage200.LocalLlmChat.Core.Abstractions;
using Sage200.LocalLlmChat.Core.Services;
using Sage200.LocalLlmChat.Infrastructure.Llm;
using Sage200.LocalLlmChat.Infrastructure.Logging;
using Sage200.LocalLlmChat.Infrastructure.Navigation;
using Sage200.LocalLlmChat.Infrastructure.Security;
using Sage200.LocalLlmChat.Infrastructure.Services;

namespace Sage200.LocalLlmChat.Infrastructure.Composition;

public enum LocalLlmProviderType
{
    Ollama,
    LmStudio,
    GgufInProcess
}

public static class ChatAssistantFactory
{
    public static ChatOrchestrator CreateDemoOrchestrator(
        LocalLlmProviderType providerType,
        LocalLlmConnectorOptions connectorOptions,
        ISageFormLauncher formLauncher,
        IGgufRuntime? ggufRuntime = null)
    {
        var logger = new StructuredChatLogger();
        var llmClient = CreateLlmClient(providerType, connectorOptions, logger, ggufRuntime);

        var crmService = new SiconCrmService(
            new InMemoryCrmMetadataProvider(),
            new InMemoryCrmValidationProbe(),
            new InMemorySiconCrmGateway(),
            logger);

        var ordersService = new SageOrdersService(
            new InMemoryOrdersMetadataProvider(),
            new InMemoryOrdersValidationProbe(),
            new InMemoryOrdersLookupValidator(),
            new InMemorySageOrdersGateway(),
            logger);

        return new ChatOrchestrator(
            llmClient,
            crmService,
            ordersService,
            new ClarificationQuestionManager(),
            new SageRecordNavigator(formLauncher),
            new SageUserPermissionService(new AllowAllPermissionGateway()),
            logger);
    }

    private static ILocalLlmClient CreateLlmClient(
        LocalLlmProviderType providerType,
        LocalLlmConnectorOptions connectorOptions,
        IChatLogger logger,
        IGgufRuntime? ggufRuntime)
    {
        ILocalLlmClient rawClient = providerType switch
        {
            LocalLlmProviderType.Ollama => new OllamaLocalLlmClient(new HttpClient(), connectorOptions),
            LocalLlmProviderType.LmStudio => new LmStudioLocalLlmClient(new HttpClient(), connectorOptions),
            LocalLlmProviderType.GgufInProcess when ggufRuntime is not null => new GgufInProcessLlmClient(ggufRuntime),
            LocalLlmProviderType.GgufInProcess => throw new InvalidOperationException("GGUF provider requires IGgufRuntime."),
            _ => throw new ArgumentOutOfRangeException(nameof(providerType), providerType, null)
        };

        return new ResilientLocalLlmClient(rawClient, connectorOptions, logger);
    }
}
