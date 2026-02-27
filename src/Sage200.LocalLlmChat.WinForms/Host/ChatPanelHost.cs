using System.Windows.Forms;
using Sage200.LocalLlmChat.Core.Models;
using Sage200.LocalLlmChat.Core.Services;
using Sage200.LocalLlmChat.WinForms.Controls;
using Sage200.LocalLlmChat.WinForms.State;

namespace Sage200.LocalLlmChat.WinForms.Host;

public sealed class ChatPanelHost : IDisposable
{
    private readonly Form _mainForm;
    private readonly ToolStrip _toolbar;
    private readonly ChatOrchestrator _orchestrator;
    private readonly IChatPanelSessionStateStore _stateStore;
    private readonly ChatSessionContext _sessionContext = new();
    private readonly List<ChatMessage> _conversation = [];
    private readonly SemaphoreSlim _turnLock = new(1, 1);
    private readonly ChatPanelControl _panel;
    private readonly Splitter _splitter;
    private readonly ToolStripButton _toggleButton;
    private readonly Keys _toggleHotkey;
    private string? _lastUserMessage;
    private bool _disposed;

    public ChatPanelHost(
        Form mainForm,
        ToolStrip toolbar,
        ChatOrchestrator orchestrator,
        IChatPanelSessionStateStore stateStore,
        Keys toggleHotkey = Keys.Control | Keys.Shift | Keys.K)
    {
        _mainForm = mainForm;
        _toolbar = toolbar;
        _orchestrator = orchestrator;
        _stateStore = stateStore;
        _toggleHotkey = toggleHotkey;

        _panel = new ChatPanelControl();
        _panel.SendRequested += async (_, message) => await HandleUserMessageAsync(message);
        _panel.ConfirmRequested += async (_, _) => await HandleConfirmAsync();
        _panel.CancelRequested += async (_, _) => await HandleCancelAsync();
        _panel.RegenerateRequested += async (_, _) => await HandleRegenerateAsync();
        _panel.ShowDetailsRequested += (_, details) => ShowTechnicalDetails(details);
        _panel.SizeChanged += async (_, _) => await SaveStateAsync(CancellationToken.None);

        _splitter = new Splitter
        {
            Dock = DockStyle.Right,
            Width = 5
        };

        _toggleButton = new ToolStripButton("AI Chat")
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            CheckOnClick = false,
            Checked = true,
            ToolTipText = "Toggle AI chat panel (Ctrl+Shift+K)"
        };
        _toggleButton.Click += (_, _) => ToggleVisibility();
        _toolbar.Items.Add(_toggleButton);

        _mainForm.KeyPreview = true;
        _mainForm.KeyDown += MainFormOnKeyDown;
        _mainForm.FormClosing += MainFormOnFormClosing;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var loaded = await _stateStore.LoadAsync(cancellationToken);
        _panel.Width = Math.Max(_panel.MinimumSize.Width, loaded.Width);

        foreach (var message in loaded.Conversation)
        {
            _conversation.Add(message);
            _panel.AppendMessage(message.Role, message.Content);
        }

        _panel.Visible = loaded.IsOpen;
        _splitter.Visible = loaded.IsOpen;
        _toggleButton.Checked = loaded.IsOpen;

        _mainForm.Controls.Add(_panel);
        _mainForm.Controls.Add(_splitter);
    }

    private async Task HandleUserMessageAsync(string message)
    {
        await _turnLock.WaitAsync();
        try
        {
            _lastUserMessage = message;
            _panel.HideErrorBanner();
            _panel.AppendMessage("user", message);
            _conversation.Add(new ChatMessage { Role = "user", Content = message, Timestamp = DateTimeOffset.UtcNow });

            _panel.SetInputEnabled(false);
            var result = await _orchestrator.HandleUserInputAsync(
                message,
                _conversation,
                _sessionContext,
                CancellationToken.None);
            ApplyTurnResult(result);
        }
        finally
        {
            _panel.SetInputEnabled(true);
            await SaveStateAsync(CancellationToken.None);
            _turnLock.Release();
        }
    }

    private async Task HandleConfirmAsync()
    {
        await _turnLock.WaitAsync();
        try
        {
            _panel.HideErrorBanner();
            _panel.AppendMessage("user", "Confirm & Create");
            _conversation.Add(new ChatMessage { Role = "user", Content = "Confirm & Create" });

            _panel.SetInputEnabled(false);
            var result = await _orchestrator.ConfirmPendingOrderDirectAsync(_sessionContext, CancellationToken.None);
            ApplyTurnResult(result);
        }
        finally
        {
            _panel.SetInputEnabled(true);
            await SaveStateAsync(CancellationToken.None);
            _turnLock.Release();
        }
    }

    private async Task HandleCancelAsync()
    {
        await _turnLock.WaitAsync();
        try
        {
            _panel.HideErrorBanner();
            _panel.AppendMessage("user", "Cancel");
            _conversation.Add(new ChatMessage { Role = "user", Content = "Cancel" });

            var result = _orchestrator.CancelCurrentAction(_sessionContext);
            ApplyTurnResult(result);
        }
        finally
        {
            await SaveStateAsync(CancellationToken.None);
            _turnLock.Release();
        }
    }

    private async Task HandleRegenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastUserMessage))
        {
            _panel.ShowErrorBanner("Nothing to regenerate yet.", null);
            return;
        }

        await HandleUserMessageAsync(_lastUserMessage);
    }

    private void ApplyTurnResult(ChatTurnResult result)
    {
        _panel.SetPreviewCard(result.PreviewCard);
        _panel.SetConfirmButtonsVisible(result.ShowConfirmButtons);

        if (result.ShowErrorBanner)
        {
            _panel.ShowErrorBanner(result.UserFacingMessage, result.TechnicalDetails);
        }
        else
        {
            _panel.HideErrorBanner();
        }

        _panel.AppendMessage("assistant", result.UserFacingMessage);
        _conversation.Add(new ChatMessage { Role = "assistant", Content = result.UserFacingMessage, Timestamp = DateTimeOffset.UtcNow });
    }

    private void ShowTechnicalDetails(string? details)
    {
        var text = string.IsNullOrWhiteSpace(details) ? "No technical details available." : details;
        MessageBox.Show(_mainForm, text, "Technical details", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task SaveStateAsync(CancellationToken cancellationToken)
    {
        var skip = Math.Max(0, _conversation.Count - 30);
        var state = new ChatPanelSessionState
        {
            IsOpen = _panel.Visible,
            Width = _panel.Width,
            Conversation = _conversation.Skip(skip).ToArray()
        };

        await _stateStore.SaveAsync(state, cancellationToken);
    }

    private void ToggleVisibility()
    {
        var visible = !_panel.Visible;
        _panel.Visible = visible;
        _splitter.Visible = visible;
        _toggleButton.Checked = visible;
        _ = SaveStateAsync(CancellationToken.None);
    }

    private void MainFormOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyData == _toggleHotkey)
        {
            e.Handled = true;
            ToggleVisibility();
        }
    }

    private void MainFormOnFormClosing(object? sender, FormClosingEventArgs e) =>
        SaveStateAsync(CancellationToken.None).GetAwaiter().GetResult();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _mainForm.KeyDown -= MainFormOnKeyDown;
        _mainForm.FormClosing -= MainFormOnFormClosing;
        _panel.Dispose();
        _splitter.Dispose();
        _toggleButton.Dispose();
        _turnLock.Dispose();
    }
}
