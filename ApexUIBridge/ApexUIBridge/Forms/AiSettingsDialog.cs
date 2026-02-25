using ApexUIBridge.Models;

namespace ApexUIBridge.Forms;

/// <summary>
/// Settings dialog for all AI configuration. Built entirely in code — no Designer.cs.
/// </summary>
public sealed class AiSettingsDialog : Form
{
    private readonly AiSettings _settings;

    // Backend group controls
    private ComboBox _providerCombo = new();
    private Label _lblModelPath = new();
    private Panel _modelPathPanel = new();
    private TextBox _modelPathBox = new();
    private Button _browseModelBtn = new();
    private Label _lblApiKey = new();
    private TextBox _apiKeyBox = new();
    private Label _lblAnthropicModel = new();
    private ComboBox _anthropicModelCombo = new();

    // Inference group
    private NumericUpDown _tempSpinner = new();
    private ComboBox _reasoningCombo = new();
    private NumericUpDown _maxTokensSpinner = new();

    // Model loading group
    private NumericUpDown _threadsSpinner = new();
    private NumericUpDown _contextSizeSpinner = new();
    private NumericUpDown _gpuLayersSpinner = new();

    // Anti-prompts group
    private TextBox _antiPromptsChatBox = new();
    private TextBox _antiPromptsInstructBox = new();

    // Display/behaviour group
    private CheckBox _showThinkingChk = new();
    private CheckBox _autoExecChk = new();

    // System prompt
    private TextBox _systemPromptBox = new();

    // All section group boxes (filled in BuildUI)
    private GroupBox[] _sections = [];

    // Dialog buttons
    private Button _okBtn = new();
    private Button _cancelBtn = new();
    private Button _applyBtn = new();

    public AiSettingsDialog(AiSettings settings)
    {
        _settings = settings;

        Text = "AI Settings";
        Size = new Size(540, 660);
        MinimumSize = new Size(420, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = false;

        BuildUI();
        LoadFromSettings();
    }

    private void BuildUI()
    {
        // ── bottom button bar ────────────────────────────────────────────────
        var dialogBtnPanel = new Panel { Dock = DockStyle.Bottom, Height = 38 };
        _okBtn = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(75, 26) };
        _cancelBtn = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(75, 26) };
        _applyBtn = new Button { Text = "Apply", Size = new Size(75, 26) };
        _okBtn.Click += (_, _) => { SaveToSettings(); DialogResult = DialogResult.OK; Close(); };
        _cancelBtn.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _applyBtn.Click += (_, _) => SaveToSettings();

        var btnFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 5, 8, 0)
        };
        btnFlow.Controls.Add(_applyBtn);
        btnFlow.Controls.Add(_okBtn);
        btnFlow.Controls.Add(_cancelBtn);
        dialogBtnPanel.Controls.Add(btnFlow);
        AcceptButton = _okBtn;
        CancelButton = _cancelBtn;

        // ── scrollable content ───────────────────────────────────────────────
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        // Single-column TableLayoutPanel with Dock=Top fills the scroll panel width
        // automatically — no manual width sync needed. Each section gets Dock=Fill
        // so it expands to the full column width on every resize.
        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(8, 8, 8, 8)
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _sections = new[]
        {
            BuildBackendGroup(),
            BuildInferenceGroup(),
            BuildLoadingGroup(),
            BuildAntiPromptsGroup(),
            BuildDisplayGroup(),
            BuildSystemPromptGroup()
        };

        outer.RowCount = _sections.Length;
        foreach (var gb in _sections)
        {
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            gb.Dock = DockStyle.Fill;
            outer.Controls.Add(gb);
        }

        scroll.Controls.Add(outer);
        Controls.Add(scroll);
        Controls.Add(dialogBtnPanel);
    }

    // ── group builders ───────────────────────────────────────────────────────

    private GroupBox BuildBackendGroup()
    {
        var gb = MakeGroupBox("Backend");
        var t = MakeTable(4);

        _providerCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _providerCombo.Items.AddRange(new object[] { "Anthropic", "LlamaSharp (Local)", "LlamaSharp Instruct" });
        _providerCombo.SelectedIndexChanged += (_, _) => UpdateBackendVisibility();
        AddRow(t, MakeLabel("Provider:"), _providerCombo);

        // Model path row: textbox + browse button side by side
        _lblModelPath = MakeLabel("Model Path:");
        _modelPathPanel = new Panel { Dock = DockStyle.Fill, Height = 27, Margin = new Padding(0, 4, 2, 2) };
        _modelPathBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Top = 1 };
        _browseModelBtn = new Button { Text = "…", Width = 26, Anchor = AnchorStyles.Right | AnchorStyles.Top, Top = 0 };
        _browseModelBtn.Click += OnBrowseModel;
        _modelPathPanel.Controls.Add(_modelPathBox);
        _modelPathPanel.Controls.Add(_browseModelBtn);
        _modelPathPanel.Resize += (_, _) =>
        {
            _browseModelBtn.Left = _modelPathPanel.Width - _browseModelBtn.Width;
            _browseModelBtn.Height = _modelPathBox.Height;
            _modelPathBox.Width = _modelPathPanel.Width - _browseModelBtn.Width - 2;
        };
        AddRow(t, _lblModelPath, _modelPathPanel);

        _lblApiKey = MakeLabel("API Key:");
        _apiKeyBox = new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
        AddRow(t, _lblApiKey, _apiKeyBox);

        _lblAnthropicModel = MakeLabel("Claude Model:");
        _anthropicModelCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _anthropicModelCombo.Items.AddRange(new object[]
        {
            "claude-haiku-4-5-20251001", "claude-sonnet-4-5-20250929", "claude-opus-4-1-20250805"
        });
        AddRow(t, _lblAnthropicModel, _anthropicModelCombo);

        gb.Controls.Add(t);
        return gb;
    }

    private GroupBox BuildInferenceGroup()
    {
        var gb = MakeGroupBox("Inference");
        var t = MakeTable(3);

        _tempSpinner = new NumericUpDown
        {
            DecimalPlaces = 2, Increment = 0.05m, Minimum = 0m, Maximum = 2m, Width = 80
        };
        AddRow(t, MakeLabel("Temperature:"), _tempSpinner);

        _reasoningCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        _reasoningCombo.Items.AddRange(new object[] { "", "low", "medium", "high" });
        AddRow(t, MakeLabel("Reasoning:"), _reasoningCombo);

        _maxTokensSpinner = new NumericUpDown
        {
            Minimum = 64, Maximum = 131072, Increment = 256, Width = 110
        };
        AddRow(t, MakeLabel("Max Tokens:"), _maxTokensSpinner);

        gb.Controls.Add(t);
        return gb;
    }

    private GroupBox BuildLoadingGroup()
    {
        var gb = MakeGroupBox("Model Loading");
        var t = MakeTable(3);

        _threadsSpinner = new NumericUpDown { Minimum = 1, Maximum = 256, Width = 80 };
        AddRow(t, MakeLabel("Threads:"), _threadsSpinner);

        _contextSizeSpinner = new NumericUpDown { Minimum = 512, Maximum = 1048576, Increment = 512, Width = 110 };
        AddRow(t, MakeLabel("Context Size:"), _contextSizeSpinner);

        _gpuLayersSpinner = new NumericUpDown { Minimum = 0, Maximum = 999, Width = 80 };
        AddRow(t, MakeLabel("GPU Layers:"), _gpuLayersSpinner);

        gb.Controls.Add(t);
        return gb;
    }

    private GroupBox BuildAntiPromptsGroup()
    {
        var gb = MakeGroupBox("Anti-Prompts");
        var t = MakeTable(2);

        _antiPromptsChatBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "comma-separated, e.g. User:,Human:"
        };
        AddRow(t, MakeLabel("Chat:"), _antiPromptsChatBox);

        _antiPromptsInstructBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "comma-separated, e.g. [INST]"
        };
        AddRow(t, MakeLabel("Instruct:"), _antiPromptsInstructBox);

        gb.Controls.Add(t);
        return gb;
    }

    private GroupBox BuildDisplayGroup()
    {
        var gb = MakeGroupBox("Display & Behaviour");

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(2, 2, 2, 2)
        };

        _showThinkingChk = new CheckBox
        {
            Text = "Show thinking in chat",
            AutoSize = true,
            Margin = new Padding(4, 4, 16, 4)
        };
        _autoExecChk = new CheckBox
        {
            Text = "Auto-execute commands",
            AutoSize = true,
            Margin = new Padding(4)
        };

        flow.Controls.Add(_showThinkingChk);
        flow.Controls.Add(_autoExecChk);
        gb.Controls.Add(flow);

        return gb;
    }

    private GroupBox BuildSystemPromptGroup()
    {
        var gb = new GroupBox
        {
            Text = "System Prompt",
            Height = 130,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(6, 18, 6, 6)
        };

        _systemPromptBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 8.5f)
        };
        gb.Controls.Add(_systemPromptBox);
        return gb;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static GroupBox MakeGroupBox(string title) => new()
    {
        Text = title,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowOnly,
        Margin = new Padding(0, 0, 0, 6),
        Padding = new Padding(6, 18, 6, 6)
    };

    private static TableLayoutPanel MakeTable(int rows)
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(2, 2, 2, 2)
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    private static Label MakeLabel(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };

    private static void AddRow(TableLayoutPanel t, Control label, Control input)
    {
        label.Margin = new Padding(2, 7, 4, 2);
        input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        input.Margin = new Padding(0, 4, 4, 2);
        t.Controls.Add(label);
        t.Controls.Add(input);
    }

    // ── backend visibility ───────────────────────────────────────────────────

    private void UpdateBackendVisibility()
    {
        var provider = _providerCombo.SelectedItem?.ToString() ?? "";
        bool isLlama = provider is "LlamaSharp (Local)" or "LlamaSharp Instruct";
        bool isAnthropic = provider == "Anthropic";

        _lblModelPath.Visible = isLlama;
        _modelPathPanel.Visible = isLlama;
        _lblApiKey.Visible = isAnthropic;
        _apiKeyBox.Visible = isAnthropic;
        _lblAnthropicModel.Visible = isAnthropic;
        _anthropicModelCombo.Visible = isAnthropic;
    }

    private void OnBrowseModel(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select GGUF Model File",
            Filter = "GGUF Models (*.gguf)|*.gguf|All Files (*.*)|*.*",
            CheckFileExists = true
        };
        if (!string.IsNullOrEmpty(_modelPathBox.Text))
            dlg.InitialDirectory = Path.GetDirectoryName(_modelPathBox.Text) ?? "";

        if (dlg.ShowDialog(this) == DialogResult.OK)
            _modelPathBox.Text = dlg.FileName;
    }

    // ── load / save ──────────────────────────────────────────────────────────

    private void LoadFromSettings()
    {
        int provIdx = _providerCombo.Items.IndexOf(_settings.Provider);
        _providerCombo.SelectedIndex = provIdx >= 0 ? provIdx : 0;

        _modelPathBox.Text = _settings.ModelPath;
        _apiKeyBox.Text = _settings.AnthropicApiKey;

        int modelIdx = _anthropicModelCombo.Items.IndexOf(_settings.AnthropicModel);
        _anthropicModelCombo.SelectedIndex = modelIdx >= 0 ? modelIdx : 0;

        _tempSpinner.Value = (decimal)Math.Clamp(_settings.Temperature, 0f, 2f);

        int reasoningIdx = _reasoningCombo.Items.IndexOf(_settings.ReasoningEffort);
        _reasoningCombo.SelectedIndex = reasoningIdx >= 0 ? reasoningIdx : 0;

        _maxTokensSpinner.Value = Math.Clamp(_settings.MaxTokens, 64, 131072);
        _threadsSpinner.Value = Math.Clamp(_settings.Threads, 1, 256);
        _contextSizeSpinner.Value = Math.Clamp(_settings.ContextSize, 512, 1048576);
        _gpuLayersSpinner.Value = Math.Clamp(_settings.GpuLayers, 0, 999);

        _antiPromptsChatBox.Text = string.Join(", ", _settings.AntiPromptsChat);
        _antiPromptsInstructBox.Text = string.Join(", ", _settings.AntiPromptsInstruct);

        _showThinkingChk.Checked = _settings.ShowThinking;
        _autoExecChk.Checked = _settings.AutoExec;
        _systemPromptBox.Text = _settings.SystemPrompt;

        UpdateBackendVisibility();
    }

    public void SaveToSettings()
    {
        _settings.Provider = _providerCombo.SelectedItem?.ToString() ?? "LlamaSharp Instruct";
        _settings.ModelPath = _modelPathBox.Text.Trim();
        _settings.AnthropicApiKey = _apiKeyBox.Text.Trim();
        _settings.AnthropicModel = _anthropicModelCombo.SelectedItem?.ToString() ?? "claude-haiku-4-5-20251001";
        _settings.Temperature = (float)_tempSpinner.Value;
        _settings.ReasoningEffort = _reasoningCombo.SelectedItem?.ToString() ?? "";
        _settings.MaxTokens = (int)_maxTokensSpinner.Value;
        _settings.Threads = (int)_threadsSpinner.Value;
        _settings.ContextSize = (int)_contextSizeSpinner.Value;
        _settings.GpuLayers = (int)_gpuLayersSpinner.Value;

        _settings.AntiPromptsChat = _antiPromptsChatBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        _settings.AntiPromptsInstruct = _antiPromptsInstructBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        _settings.ShowThinking = _showThinkingChk.Checked;
        _settings.AutoExec = _autoExecChk.Checked;
        _settings.SystemPrompt = _systemPromptBox.Text;
    }
}
