using Sage200.LocalLlmChat.Core.Models;

namespace Sage200.LocalLlmChat.WinForms.State;

public sealed class ChatPanelSessionState
{
    public bool IsOpen { get; init; } = true;

    public int Width { get; init; } = 420;

    public IReadOnlyList<ChatMessage> Conversation { get; init; } = Array.Empty<ChatMessage>();
}

public interface IChatPanelSessionStateStore
{
    Task<ChatPanelSessionState> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(ChatPanelSessionState state, CancellationToken cancellationToken);
}
