using System.Drawing;
using System.Windows.Forms;

namespace Sage200.LocalLlmChat.WinForms.Controls;

public sealed class ChatPanelControl : UserControl
{
    private readonly ListBox _messages;
    private readonly TextBox _input;
    private readonly Button _sendButton;
    private readonly Panel _errorBanner;
    private readonly Label _errorLabel;
    private readonly LinkLabel _regenerateLink;
    private readonly LinkLabel _showDetailsLink;
    private readonly LinkLabel _copyDetailsLink;
    private readonly Panel _previewPanel;
    private readonly Label _previewLabel;
    private readonly FlowLayoutPanel _confirmPanel;
    private readonly Button _confirmButton;
    private readonly Button _cancelButton;

    private string? _technicalDetails;

    public event EventHandler<string>? SendRequested;

    public event EventHandler? ConfirmRequested;

    public event EventHandler? CancelRequested;

    public event EventHandler? RegenerateRequested;

    public event EventHandler<string?>? ShowDetailsRequested;

    public ChatPanelControl()
    {
        Dock = DockStyle.Right;
        MinimumSize = new Size(280, 250);
        Width = 420;
        BackColor = Color.WhiteSmoke;

        _errorBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 66,
            BackColor = Color.MistyRose,
            Visible = false
        };

        _errorLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 6, 8, 0)
        };

        var errorLinks = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };

        _regenerateLink = new LinkLabel
        {
            Text = "Regenerate",
            Margin = new Padding(8, 2, 8, 2),
            AutoSize = true
        };

        _showDetailsLink = new LinkLabel
        {
            Text = "Show details",
            Margin = new Padding(0, 2, 8, 2),
            AutoSize = true
        };

        _copyDetailsLink = new LinkLabel
        {
            Text = "Copy technical details",
            Margin = new Padding(0, 2, 8, 2),
            AutoSize = true
        };

        _regenerateLink.LinkClicked += (_, _) => RegenerateRequested?.Invoke(this, EventArgs.Empty);
        _showDetailsLink.LinkClicked += (_, _) => ShowDetailsRequested?.Invoke(this, _technicalDetails);
        _copyDetailsLink.LinkClicked += (_, _) =>
        {
            var text = string.IsNullOrWhiteSpace(_technicalDetails) ? "No technical details available." : _technicalDetails;
            Clipboard.SetText(text);
        };
        errorLinks.Controls.Add(_regenerateLink);
        errorLinks.Controls.Add(_showDetailsLink);
        errorLinks.Controls.Add(_copyDetailsLink);
        _errorBanner.Controls.Add(errorLinks);
        _errorBanner.Controls.Add(_errorLabel);

        _previewPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Color.AliceBlue,
            Visible = false
        };
        _previewLabel = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _previewPanel.Controls.Add(_previewLabel);

        _messages = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            HorizontalScrollbar = true
        };

        _confirmPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(8, 8, 8, 8),
            FlowDirection = FlowDirection.RightToLeft,
            Visible = false
        };
        _confirmButton = new Button
        {
            Text = "Confirm",
            AutoSize = true
        };
        _cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true
        };
        _confirmButton.Click += (_, _) => ConfirmRequested?.Invoke(this, EventArgs.Empty);
        _cancelButton.Click += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        _confirmPanel.Controls.Add(_confirmButton);
        _confirmPanel.Controls.Add(_cancelButton);

        var inputPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 88,
            Padding = new Padding(8)
        };

        _sendButton = new Button
        {
            Text = "Send",
            Width = 72,
            Dock = DockStyle.Right
        };
        _sendButton.Click += (_, _) => SendCurrentInput();

        _input = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical
        };
        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.Control)
            {
                e.SuppressKeyPress = true;
                SendCurrentInput();
            }
        };

        inputPanel.Controls.Add(_input);
        inputPanel.Controls.Add(_sendButton);

        Controls.Add(_messages);
        Controls.Add(inputPanel);
        Controls.Add(_confirmPanel);
        Controls.Add(_previewPanel);
        Controls.Add(_errorBanner);
    }

    public void AppendMessage(string role, string text)
    {
        _messages.Items.Add($"[{role}] {text}");
        _messages.TopIndex = _messages.Items.Count - 1;
    }

    public void SetPreviewCard(string? previewText)
    {
        var hasContent = !string.IsNullOrWhiteSpace(previewText);
        _previewPanel.Visible = hasContent;
        _previewLabel.Text = previewText ?? string.Empty;
    }

    public void SetConfirmButtonsVisible(bool visible)
    {
        _confirmPanel.Visible = visible;
    }

    public void ShowErrorBanner(string message, string? technicalDetails)
    {
        _technicalDetails = technicalDetails;
        _errorLabel.Text = message;
        _errorBanner.Visible = true;
    }

    public void HideErrorBanner()
    {
        _technicalDetails = null;
        _errorBanner.Visible = false;
    }

    public void SetInputEnabled(bool enabled)
    {
        _input.Enabled = enabled;
        _sendButton.Enabled = enabled;
    }

    private void SendCurrentInput()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        _input.Clear();
        SendRequested?.Invoke(this, text);
    }
}
