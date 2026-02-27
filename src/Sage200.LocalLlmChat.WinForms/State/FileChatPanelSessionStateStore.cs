using System.Text.Json;
using Sage200.LocalLlmChat.Core.Models;

namespace Sage200.LocalLlmChat.WinForms.State;

public sealed class FileChatPanelSessionStateStore : IChatPanelSessionStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileChatPanelSessionStateStore(string? customDirectory = null)
    {
        var root = customDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            root = Path.Combine(appData, "Sage200", "LocalLlmChat");
        }

        Directory.CreateDirectory(root);
        var user = Environment.UserName;
        _filePath = Path.Combine(root, $"chat-panel-{user}.json");
    }

    public async Task<ChatPanelSessionState> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new ChatPanelSessionState();
        }

        using (var stream = File.OpenRead(_filePath))
        {
            var state = await JsonSerializer.DeserializeAsync<ChatPanelSessionState>(stream, cancellationToken: cancellationToken);
            return state ?? new ChatPanelSessionState();
        }
    }

    public async Task SaveAsync(ChatPanelSessionState state, CancellationToken cancellationToken)
    {
        using (var stream = File.Create(_filePath))
        {
            await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken);
        }
    }
}
