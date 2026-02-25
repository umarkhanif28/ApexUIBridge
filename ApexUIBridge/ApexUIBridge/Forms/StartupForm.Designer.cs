namespace ApexUIBridge.Forms;

partial class StartupForm {
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing) {
        if (disposing && (components != null)) {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _windowChangeTimer = new System.Windows.Forms.Timer(components);
        _menuStrip = new MenuStrip();
        _menuTools = new ToolStripMenuItem();
        _menuTestWinForms = new ToolStripMenuItem();
        _menuTestEcommerce = new ToolStripMenuItem();
        _menuTestWpf = new ToolStripMenuItem();
        _menuToolStripMenuItem = new ToolStripMenuItem();
        _menuTestCustomWindow = new ToolStripMenuItem();
        _menuToolsSep1 = new ToolStripSeparator();
        _menuListWindows = new ToolStripMenuItem();
        _menuScanWindow = new ToolStripMenuItem();
        _menuToolsSep2 = new ToolStripSeparator();
        _menuAiChat = new ToolStripMenuItem();
        _menuToolsSep3 = new ToolStripSeparator();
        _menuAiSettings = new ToolStripMenuItem();
        _filter = new TextBox();
        _windowedOnly = new CheckBox();
        _grid = new DataGridView();
        _colPid = new DataGridViewTextBoxColumn();
        _colElementId = new DataGridViewTextBoxColumn();
        _colWindowTitle = new DataGridViewTextBoxColumn();
        _processButtons = new FlowLayoutPanel();
        _btnRefresh = new Button();
        _btnPick = new Button();
        _btnInspect = new Button();
        _topPanel = new Panel();
        _tree = new TreeView();
        _details = new ListView();
        _colProperty = new ColumnHeader();
        _colValue = new ColumnHeader();
        _inspectButtons = new FlowLayoutPanel();
        _btnRefreshTree = new Button();
        _btnCopyDetails = new Button();
        _btnCopyJson = new Button();
        _btnCopyState = new Button();
        _btnCapture = new Button();
        _chkHover = new CheckBox();
        _chkHighlight = new CheckBox();
        _chkFocus = new CheckBox();
        _chkXPath = new CheckBox();
        _lblElementId = new Label();
        _elementIdBox = new TextBox();
        _clickButton = new Button();
        _debugLog = new ListBox();
        _inspectSplit = new SplitContainer();
        _contentSplit = new SplitContainer();
        _aiChatPanel = new Panel();
        _aiOutput = new RichTextBox();
        _aiInputPanel = new Panel();
        _aiInput = new TextBox();
        _aiButtonPanel = new FlowLayoutPanel();
        _btnAiSend = new Button();
        _btnAiStream = new Button();
        _btnAiClear = new Button();
        _btnAiStop = new Button();
        _aiStatusStrip = new StatusStrip();
        _aiStatusLabel = new ToolStripStatusLabel();
        _aiIndicatorLabel = new ToolStripStatusLabel();
        _aiStatsLabel = new ToolStripStatusLabel();
        _aiSysStatsLabel = new ToolStripStatusLabel();
        _aiSystemBox = new TextBox();
        _aiSettingsPanel = new FlowLayoutPanel();
        _lblAiProvider = new Label();
        _aiProviderCombo = new ComboBox();
        _lblAiApiKey = new Label();
        _aiApiKeyBox = new TextBox();
        _lblAiModelPath = new Label();
        _aiModelPathBox = new TextBox();
        _btnAiBrowse = new Button();
        _lblAiModel = new Label();
        _aiModelCombo = new ComboBox();
        _chkAiAutoExec = new CheckBox();
        _bottomPanel = new Panel();
        _finderPanel = new FlowLayoutPanel();
        _lblFinderSearch = new Label();
        _finderSearchBox = new TextBox();
        _btnFindWindow = new Button();
        _btnFindElement = new Button();
        _btnFindAll = new Button();
        _lblExcludeNames = new Label();
        _finderExcludeNames = new TextBox();
        _lblIncludeTypes = new Label();
        _finderIncludeTypes = new TextBox();
        _rootSplit = new SplitContainer();
        _menuStrip.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_grid).BeginInit();
        _topPanel.SuspendLayout();
        _inspectButtons.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_inspectSplit).BeginInit();
        _inspectSplit.Panel1.SuspendLayout();
        _inspectSplit.Panel2.SuspendLayout();
        _inspectSplit.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_contentSplit).BeginInit();
        _contentSplit.Panel1.SuspendLayout();
        _contentSplit.Panel2.SuspendLayout();
        _contentSplit.SuspendLayout();
        _aiChatPanel.SuspendLayout();
        _aiInputPanel.SuspendLayout();
        _aiButtonPanel.SuspendLayout();
        _aiStatusStrip.SuspendLayout();
        _aiSettingsPanel.SuspendLayout();
        _bottomPanel.SuspendLayout();
        _finderPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_rootSplit).BeginInit();
        _rootSplit.Panel1.SuspendLayout();
        _rootSplit.Panel2.SuspendLayout();
        _rootSplit.SuspendLayout();
        SuspendLayout();
        // 
        // _windowChangeTimer
        // 
        _windowChangeTimer.Interval = 1000;
        // 
        // _menuStrip
        // 
        _menuStrip.Items.AddRange(new ToolStripItem[] { _menuTools });
        _menuStrip.Location = new Point(0, 0);
        _menuStrip.Name = "_menuStrip";
        _menuStrip.Size = new Size(1300, 24);
        _menuStrip.TabIndex = 1;
        // 
        // _menuTools
        // 
        _menuTools.DropDownItems.AddRange(new ToolStripItem[] { _menuTestWinForms, _menuTestEcommerce, _menuTestWpf, _menuToolStripMenuItem, _menuTestCustomWindow, _menuToolsSep1, _menuListWindows, _menuScanWindow, _menuToolsSep2, _menuAiChat, _menuToolsSep3, _menuAiSettings });
        _menuTools.Name = "_menuTools";
        _menuTools.Size = new Size(47, 20);
        _menuTools.Text = "&Tools";
        // 
        // _menuTestWinForms
        // 
        _menuTestWinForms.Name = "_menuTestWinForms";
        _menuTestWinForms.ShortcutKeys = Keys.F5;
        _menuTestWinForms.Size = new Size(284, 22);
        _menuTestWinForms.Text = "Test WinForms App (Bridge)";
        // 
        // _menuTestEcommerce
        // 
        _menuTestEcommerce.Name = "_menuTestEcommerce";
        _menuTestEcommerce.ShortcutKeys = Keys.F6;
        _menuTestEcommerce.Size = new Size(284, 22);
        _menuTestEcommerce.Text = "Test E-Commerce Web App (Bridge)";
        // 
        // _menuTestWpf
        // 
        _menuTestWpf.Name = "_menuTestWpf";
        _menuTestWpf.ShortcutKeys = Keys.F7;
        _menuTestWpf.Size = new Size(284, 22);
        _menuTestWpf.Text = "Test WPF App (Bridge)";
        // 
        // _menuToolStripMenuItem
        // 
        _menuToolStripMenuItem.Name = "_menuToolStripMenuItem";
        _menuToolStripMenuItem.Size = new Size(284, 22);
        _menuToolStripMenuItem.Text = "Test Menu App (Bridge)";
        // 
        // _menuTestCustomWindow
        // 
        _menuTestCustomWindow.Name = "_menuTestCustomWindow";
        _menuTestCustomWindow.Size = new Size(284, 22);
        _menuTestCustomWindow.Text = "Test Custom Window...";
        // 
        // _menuToolsSep1
        // 
        _menuToolsSep1.Name = "_menuToolsSep1";
        _menuToolsSep1.Size = new Size(281, 6);
        // 
        // _menuListWindows
        // 
        _menuListWindows.Name = "_menuListWindows";
        _menuListWindows.Size = new Size(284, 22);
        _menuListWindows.Text = "List Windows";
        // 
        // _menuScanWindow
        // 
        _menuScanWindow.Name = "_menuScanWindow";
        _menuScanWindow.Size = new Size(284, 22);
        _menuScanWindow.Text = "Scan Window...";
        // 
        // _menuToolsSep2
        // 
        _menuToolsSep2.Name = "_menuToolsSep2";
        _menuToolsSep2.Size = new Size(281, 6);
        // 
        // _menuAiChat
        // 
        _menuAiChat.Name = "_menuAiChat";
        _menuAiChat.ShortcutKeys = Keys.Control | Keys.Shift | Keys.A;
        _menuAiChat.Size = new Size(284, 22);
        _menuAiChat.Text = "Toggle AI Chat Panel";
        // 
        // _menuToolsSep3
        // 
        _menuToolsSep3.Name = "_menuToolsSep3";
        _menuToolsSep3.Size = new Size(281, 6);
        // 
        // _menuAiSettings
        // 
        _menuAiSettings.Name = "_menuAiSettings";
        _menuAiSettings.Size = new Size(284, 22);
        _menuAiSettings.Text = "AI Settings…";
        // 
        // _filter
        // 
        _filter.Dock = DockStyle.Top;
        _filter.Location = new Point(0, 0);
        _filter.Name = "_filter";
        _filter.PlaceholderText = "Filter by title or process id";
        _filter.Size = new Size(1300, 23);
        _filter.TabIndex = 3;
        // 
        // _windowedOnly
        // 
        _windowedOnly.Dock = DockStyle.Top;
        _windowedOnly.Location = new Point(0, 23);
        _windowedOnly.Name = "_windowedOnly";
        _windowedOnly.Size = new Size(1300, 24);
        _windowedOnly.TabIndex = 2;
        _windowedOnly.Text = "Windowed only";
        // 
        // _grid
        // 
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.Columns.AddRange(new DataGridViewColumn[] { _colPid, _colElementId, _colWindowTitle });
        _grid.Dock = DockStyle.Fill;
        _grid.Location = new Point(0, 87);
        _grid.Name = "_grid";
        _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.Size = new Size(1300, 215);
        _grid.TabIndex = 0;
        // 
        // _colPid
        // 
        _colPid.DataPropertyName = "ProcessId";
        _colPid.HeaderText = "PID";
        _colPid.Name = "_colPid";
        _colPid.ReadOnly = true;
        _colPid.Width = 60;
        // 
        // _colElementId
        // 
        _colElementId.DataPropertyName = "ElementId";
        _colElementId.HeaderText = "Element ID";
        _colElementId.Name = "_colElementId";
        _colElementId.ReadOnly = true;
        _colElementId.Width = 80;
        // 
        // _colWindowTitle
        // 
        _colWindowTitle.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _colWindowTitle.DataPropertyName = "WindowTitle";
        _colWindowTitle.HeaderText = "Window";
        _colWindowTitle.MinimumWidth = 300;
        _colWindowTitle.Name = "_colWindowTitle";
        _colWindowTitle.ReadOnly = true;
        // 
        // _processButtons
        // 
        _processButtons.Dock = DockStyle.Top;
        _processButtons.Location = new Point(0, 47);
        _processButtons.Name = "_processButtons";
        _processButtons.Size = new Size(1300, 40);
        _processButtons.TabIndex = 1;
        // 
        // _btnRefresh
        // 
        _btnRefresh.Location = new Point(3, 3);
        _btnRefresh.Name = "_btnRefresh";
        _btnRefresh.Size = new Size(75, 23);
        _btnRefresh.TabIndex = 0;
        _btnRefresh.Text = "Refresh";
        // 
        // _btnPick
        // 
        _btnPick.Location = new Point(84, 3);
        _btnPick.Name = "_btnPick";
        _btnPick.Size = new Size(75, 23);
        _btnPick.TabIndex = 1;
        _btnPick.Text = "Pick Window";
        // 
        // _btnInspect
        // 
        _btnInspect.Location = new Point(165, 3);
        _btnInspect.Name = "_btnInspect";
        _btnInspect.Size = new Size(75, 23);
        _btnInspect.TabIndex = 2;
        _btnInspect.Text = "Inspect Selected";
        // 
        // _topPanel
        // 
        _topPanel.Controls.Add(_grid);
        _topPanel.Controls.Add(_processButtons);
        _topPanel.Controls.Add(_windowedOnly);
        _topPanel.Controls.Add(_filter);
        _topPanel.Dock = DockStyle.Fill;
        _topPanel.Location = new Point(0, 0);
        _topPanel.Name = "_topPanel";
        _topPanel.Size = new Size(1300, 302);
        _topPanel.TabIndex = 0;
        // 
        // _tree
        // 
        _tree.Dock = DockStyle.Fill;
        _tree.Location = new Point(0, 0);
        _tree.Name = "_tree";
        _tree.Size = new Size(226, 269);
        _tree.TabIndex = 0;
        // 
        // _details
        // 
        _details.Columns.AddRange(new ColumnHeader[] { _colProperty, _colValue });
        _details.Dock = DockStyle.Fill;
        _details.FullRowSelect = true;
        _details.Location = new Point(0, 0);
        _details.Name = "_details";
        _details.Size = new Size(388, 269);
        _details.TabIndex = 0;
        _details.UseCompatibleStateImageBehavior = false;
        _details.View = View.Details;
        // 
        // _colProperty
        // 
        _colProperty.Text = "Property";
        _colProperty.Width = 280;
        // 
        // _colValue
        // 
        _colValue.Text = "Value";
        _colValue.Width = 520;
        // 
        // _inspectButtons
        // 
        _inspectButtons.Controls.Add(_btnRefreshTree);
        _inspectButtons.Controls.Add(_btnCopyDetails);
        _inspectButtons.Controls.Add(_btnCopyJson);
        _inspectButtons.Controls.Add(_btnCopyState);
        _inspectButtons.Controls.Add(_btnCapture);
        _inspectButtons.Controls.Add(_chkHover);
        _inspectButtons.Controls.Add(_chkHighlight);
        _inspectButtons.Controls.Add(_chkFocus);
        _inspectButtons.Controls.Add(_chkXPath);
        _inspectButtons.Controls.Add(_lblElementId);
        _inspectButtons.Controls.Add(_elementIdBox);
        _inspectButtons.Controls.Add(_clickButton);
        _inspectButtons.Dock = DockStyle.Top;
        _inspectButtons.Location = new Point(0, 0);
        _inspectButtons.Name = "_inspectButtons";
        _inspectButtons.Size = new Size(1300, 40);
        _inspectButtons.TabIndex = 3;
        // 
        // _btnRefreshTree
        // 
        _btnRefreshTree.Location = new Point(3, 3);
        _btnRefreshTree.Name = "_btnRefreshTree";
        _btnRefreshTree.Size = new Size(75, 23);
        _btnRefreshTree.TabIndex = 0;
        _btnRefreshTree.Text = "Refresh Tree";
        // 
        // _btnCopyDetails
        // 
        _btnCopyDetails.Location = new Point(84, 3);
        _btnCopyDetails.Name = "_btnCopyDetails";
        _btnCopyDetails.Size = new Size(75, 23);
        _btnCopyDetails.TabIndex = 1;
        _btnCopyDetails.Text = "Copy Details";
        // 
        // _btnCopyJson
        // 
        _btnCopyJson.Location = new Point(165, 3);
        _btnCopyJson.Name = "_btnCopyJson";
        _btnCopyJson.Size = new Size(75, 23);
        _btnCopyJson.TabIndex = 2;
        _btnCopyJson.Text = "Copy JSON";
        // 
        // _btnCopyState
        // 
        _btnCopyState.Location = new Point(246, 3);
        _btnCopyState.Name = "_btnCopyState";
        _btnCopyState.Size = new Size(75, 23);
        _btnCopyState.TabIndex = 3;
        _btnCopyState.Text = "Copy State";
        // 
        // _btnCapture
        // 
        _btnCapture.Location = new Point(327, 3);
        _btnCapture.Name = "_btnCapture";
        _btnCapture.Size = new Size(75, 23);
        _btnCapture.TabIndex = 4;
        _btnCapture.Text = "Capture";
        // 
        // _chkHover
        // 
        _chkHover.Location = new Point(408, 3);
        _chkHover.Name = "_chkHover";
        _chkHover.Size = new Size(104, 24);
        _chkHover.TabIndex = 5;
        _chkHover.Text = "Hover";
        // 
        // _chkHighlight
        // 
        _chkHighlight.Location = new Point(518, 3);
        _chkHighlight.Name = "_chkHighlight";
        _chkHighlight.Size = new Size(104, 24);
        _chkHighlight.TabIndex = 6;
        _chkHighlight.Text = "Highlight";
        // 
        // _chkFocus
        // 
        _chkFocus.Location = new Point(628, 3);
        _chkFocus.Name = "_chkFocus";
        _chkFocus.Size = new Size(104, 24);
        _chkFocus.TabIndex = 7;
        _chkFocus.Text = "Focus";
        // 
        // _chkXPath
        // 
        _chkXPath.Location = new Point(738, 3);
        _chkXPath.Name = "_chkXPath";
        _chkXPath.Size = new Size(104, 24);
        _chkXPath.TabIndex = 8;
        _chkXPath.Text = "XPath";
        // 
        // _lblElementId
        // 
        _lblElementId.Anchor = AnchorStyles.Left;
        _lblElementId.AutoSize = true;
        _lblElementId.Location = new Point(855, 10);
        _lblElementId.Margin = new Padding(10, 6, 0, 0);
        _lblElementId.Name = "_lblElementId";
        _lblElementId.Size = new Size(21, 15);
        _lblElementId.TabIndex = 9;
        _lblElementId.Text = "ID:";
        // 
        // _elementIdBox
        // 
        _elementIdBox.Location = new Point(879, 3);
        _elementIdBox.Name = "_elementIdBox";
        _elementIdBox.PlaceholderText = "Element ID";
        _elementIdBox.Size = new Size(120, 23);
        _elementIdBox.TabIndex = 10;
        // 
        // _clickButton
        // 
        _clickButton.Enabled = false;
        _clickButton.Location = new Point(1005, 3);
        _clickButton.Name = "_clickButton";
        _clickButton.Size = new Size(75, 23);
        _clickButton.TabIndex = 11;
        _clickButton.Text = "Click";
        // 
        // _debugLog
        // 
        _debugLog.Dock = DockStyle.Bottom;
        _debugLog.Font = new Font("Consolas", 9F);
        _debugLog.Location = new Point(0, 345);
        _debugLog.Name = "_debugLog";
        _debugLog.Size = new Size(1300, 74);
        _debugLog.TabIndex = 1;
        // 
        // _inspectSplit
        // 
        _inspectSplit.Dock = DockStyle.Fill;
        _inspectSplit.Location = new Point(0, 0);
        _inspectSplit.Name = "_inspectSplit";
        // 
        // _inspectSplit.Panel1
        // 
        _inspectSplit.Panel1.Controls.Add(_tree);
        // 
        // _inspectSplit.Panel2
        // 
        _inspectSplit.Panel2.Controls.Add(_details);
        _inspectSplit.Size = new Size(618, 269);
        _inspectSplit.SplitterDistance = 226;
        _inspectSplit.TabIndex = 0;
        // 
        // _contentSplit
        // 
        _contentSplit.Dock = DockStyle.Fill;
        _contentSplit.Location = new Point(0, 76);
        _contentSplit.Name = "_contentSplit";
        // 
        // _contentSplit.Panel1
        // 
        _contentSplit.Panel1.Controls.Add(_inspectSplit);
        // 
        // _contentSplit.Panel2
        // 
        _contentSplit.Panel2.Controls.Add(_aiChatPanel);
        _contentSplit.Size = new Size(1300, 269);
        _contentSplit.SplitterDistance = 618;
        _contentSplit.TabIndex = 0;
        // 
        // _aiChatPanel
        // 
        _aiChatPanel.Controls.Add(_aiOutput);
        _aiChatPanel.Controls.Add(_aiInputPanel);
        _aiChatPanel.Controls.Add(_aiStatusStrip);
        _aiChatPanel.Controls.Add(_aiSystemBox);
        _aiChatPanel.Controls.Add(_aiSettingsPanel);
        _aiChatPanel.Dock = DockStyle.Fill;
        _aiChatPanel.Location = new Point(0, 0);
        _aiChatPanel.Name = "_aiChatPanel";
        _aiChatPanel.Size = new Size(678, 269);
        _aiChatPanel.TabIndex = 0;
        // 
        // _aiOutput
        // 
        _aiOutput.BackColor = Color.FromArgb(42, 42, 42);
        _aiOutput.Dock = DockStyle.Fill;
        _aiOutput.Font = new Font("Consolas", 9F);
        _aiOutput.ForeColor = Color.FromArgb(220, 220, 220);
        _aiOutput.Location = new Point(0, 52);
        _aiOutput.Name = "_aiOutput";
        _aiOutput.ReadOnly = true;
        _aiOutput.Size = new Size(678, 85);
        _aiOutput.TabIndex = 0;
        _aiOutput.Text = "";
        _aiOutput.WordWrap = false;
        // 
        // _aiInputPanel
        // 
        _aiInputPanel.Controls.Add(_aiInput);
        _aiInputPanel.Controls.Add(_aiButtonPanel);
        _aiInputPanel.Dock = DockStyle.Bottom;
        _aiInputPanel.Location = new Point(0, 137);
        _aiInputPanel.Name = "_aiInputPanel";
        _aiInputPanel.Size = new Size(678, 110);
        _aiInputPanel.TabIndex = 1;
        // 
        // _aiInput
        // 
        _aiInput.Dock = DockStyle.Fill;
        _aiInput.Font = new Font("Consolas", 9F);
        _aiInput.Location = new Point(0, 0);
        _aiInput.Multiline = true;
        _aiInput.Name = "_aiInput";
        _aiInput.PlaceholderText = "Message... (Ctrl+Enter=Send, Ctrl+Shift+Enter=Stream)";
        _aiInput.ScrollBars = ScrollBars.Vertical;
        _aiInput.Size = new Size(608, 110);
        _aiInput.TabIndex = 0;
        // 
        // _aiButtonPanel
        // 
        _aiButtonPanel.Controls.Add(_btnAiSend);
        _aiButtonPanel.Controls.Add(_btnAiStream);
        _aiButtonPanel.Controls.Add(_btnAiClear);
        _aiButtonPanel.Controls.Add(_btnAiStop);
        _aiButtonPanel.Dock = DockStyle.Right;
        _aiButtonPanel.FlowDirection = FlowDirection.TopDown;
        _aiButtonPanel.Location = new Point(608, 0);
        _aiButtonPanel.Name = "_aiButtonPanel";
        _aiButtonPanel.Size = new Size(70, 110);
        _aiButtonPanel.TabIndex = 1;
        // 
        // _btnAiSend
        // 
        _btnAiSend.Location = new Point(3, 3);
        _btnAiSend.Name = "_btnAiSend";
        _btnAiSend.Size = new Size(60, 23);
        _btnAiSend.TabIndex = 0;
        _btnAiSend.Text = "Send";
        // 
        // _btnAiStream
        // 
        _btnAiStream.Location = new Point(3, 32);
        _btnAiStream.Name = "_btnAiStream";
        _btnAiStream.Size = new Size(60, 23);
        _btnAiStream.TabIndex = 1;
        _btnAiStream.Text = "Stream";
        // 
        // _btnAiClear
        // 
        _btnAiClear.Location = new Point(3, 61);
        _btnAiClear.Name = "_btnAiClear";
        _btnAiClear.Size = new Size(60, 23);
        _btnAiClear.TabIndex = 2;
        _btnAiClear.Text = "Clear";
        // 
        // _btnAiStop
        // 
        _btnAiStop.Enabled = false;
        _btnAiStop.ForeColor = Color.DarkRed;
        _btnAiStop.Location = new Point(69, 3);
        _btnAiStop.Name = "_btnAiStop";
        _btnAiStop.Size = new Size(60, 23);
        _btnAiStop.TabIndex = 3;
        _btnAiStop.Text = "Stop";
        // 
        // _aiStatusStrip
        // 
        _aiStatusStrip.Items.AddRange(new ToolStripItem[] { _aiStatusLabel, _aiIndicatorLabel, _aiStatsLabel, _aiSysStatsLabel });
        _aiStatusStrip.Location = new Point(0, 247);
        _aiStatusStrip.Name = "_aiStatusStrip";
        _aiStatusStrip.Size = new Size(678, 22);
        _aiStatusStrip.SizingGrip = false;
        _aiStatusStrip.TabIndex = 2;
        // 
        // _aiStatusLabel
        // 
        _aiStatusLabel.Name = "_aiStatusLabel";
        _aiStatusLabel.Size = new Size(641, 17);
        _aiStatusLabel.Spring = true;
        _aiStatusLabel.Text = "Ready";
        _aiStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _aiIndicatorLabel
        // 
        _aiIndicatorLabel.ForeColor = Color.Gray;
        _aiIndicatorLabel.Name = "_aiIndicatorLabel";
        _aiIndicatorLabel.Size = new Size(14, 17);
        _aiIndicatorLabel.Text = "●";
        _aiIndicatorLabel.ToolTipText = "Generation status";
        // 
        // _aiStatsLabel
        // 
        _aiStatsLabel.BorderSides = ToolStripStatusLabelBorderSides.Left;
        _aiStatsLabel.Name = "_aiStatsLabel";
        _aiStatsLabel.Size = new Size(4, 17);
        _aiStatsLabel.ToolTipText = "Tokens/s  |  Time to first token";
        // 
        // _aiSysStatsLabel
        // 
        _aiSysStatsLabel.BorderSides = ToolStripStatusLabelBorderSides.Left;
        _aiSysStatsLabel.Name = "_aiSysStatsLabel";
        _aiSysStatsLabel.Size = new Size(4, 17);
        _aiSysStatsLabel.ToolTipText = "CPU %  |  RAM MB";
        // 
        // _aiSystemBox
        // 
        _aiSystemBox.Dock = DockStyle.Top;
        _aiSystemBox.Location = new Point(0, 30);
        _aiSystemBox.Multiline = true;
        _aiSystemBox.Name = "_aiSystemBox";
        _aiSystemBox.PlaceholderText = "System prompt (optional)";
        _aiSystemBox.ScrollBars = ScrollBars.Vertical;
        _aiSystemBox.Size = new Size(678, 22);
        _aiSystemBox.TabIndex = 3;
        // 
        // _aiSettingsPanel
        // 
        _aiSettingsPanel.Controls.Add(_lblAiProvider);
        _aiSettingsPanel.Controls.Add(_aiProviderCombo);
        _aiSettingsPanel.Controls.Add(_lblAiApiKey);
        _aiSettingsPanel.Controls.Add(_aiApiKeyBox);
        _aiSettingsPanel.Controls.Add(_lblAiModelPath);
        _aiSettingsPanel.Controls.Add(_aiModelPathBox);
        _aiSettingsPanel.Controls.Add(_btnAiBrowse);
        _aiSettingsPanel.Controls.Add(_lblAiModel);
        _aiSettingsPanel.Controls.Add(_aiModelCombo);
        _aiSettingsPanel.Controls.Add(_chkAiAutoExec);
        _aiSettingsPanel.Dock = DockStyle.Top;
        _aiSettingsPanel.Location = new Point(0, 0);
        _aiSettingsPanel.Name = "_aiSettingsPanel";
        _aiSettingsPanel.Size = new Size(678, 30);
        _aiSettingsPanel.TabIndex = 4;
        // 
        // _lblAiProvider
        // 
        _lblAiProvider.Anchor = AnchorStyles.Left;
        _lblAiProvider.AutoSize = true;
        _lblAiProvider.Location = new Point(4, 10);
        _lblAiProvider.Margin = new Padding(4, 9, 0, 0);
        _lblAiProvider.Name = "_lblAiProvider";
        _lblAiProvider.Size = new Size(55, 15);
        _lblAiProvider.TabIndex = 0;
        _lblAiProvider.Text = "Backend:";
        // 
        // _aiProviderCombo
        // 
        _aiProviderCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _aiProviderCombo.Items.AddRange(new object[] { "Anthropic", "LlamaSharp (Local)", "LlamaSharp Instruct" });
        _aiProviderCombo.Location = new Point(61, 4);
        _aiProviderCombo.Margin = new Padding(2, 4, 2, 0);
        _aiProviderCombo.Name = "_aiProviderCombo";
        _aiProviderCombo.Size = new Size(140, 23);
        _aiProviderCombo.TabIndex = 1;
        // 
        // _lblAiApiKey
        // 
        _lblAiApiKey.Anchor = AnchorStyles.Left;
        _lblAiApiKey.AutoSize = true;
        _lblAiApiKey.Location = new Point(209, 10);
        _lblAiApiKey.Margin = new Padding(6, 9, 0, 0);
        _lblAiApiKey.Name = "_lblAiApiKey";
        _lblAiApiKey.Size = new Size(29, 15);
        _lblAiApiKey.TabIndex = 2;
        _lblAiApiKey.Text = "Key:";
        _lblAiApiKey.Visible = false;
        // 
        // _aiApiKeyBox
        // 
        _aiApiKeyBox.Location = new Point(240, 4);
        _aiApiKeyBox.Margin = new Padding(2, 4, 0, 0);
        _aiApiKeyBox.Name = "_aiApiKeyBox";
        _aiApiKeyBox.PlaceholderText = "sk-ant-...";
        _aiApiKeyBox.Size = new Size(160, 23);
        _aiApiKeyBox.TabIndex = 3;
        _aiApiKeyBox.UseSystemPasswordChar = true;
        _aiApiKeyBox.Visible = false;
        // 
        // _lblAiModelPath
        // 
        _lblAiModelPath.Anchor = AnchorStyles.Left;
        _lblAiModelPath.AutoSize = true;
        _lblAiModelPath.Location = new Point(406, 10);
        _lblAiModelPath.Margin = new Padding(6, 9, 0, 0);
        _lblAiModelPath.Name = "_lblAiModelPath";
        _lblAiModelPath.Size = new Size(34, 15);
        _lblAiModelPath.TabIndex = 4;
        _lblAiModelPath.Text = "Path:";
        _lblAiModelPath.Visible = false;
        // 
        // _aiModelPathBox
        // 
        _aiModelPathBox.Location = new Point(442, 4);
        _aiModelPathBox.Margin = new Padding(2, 4, 0, 0);
        _aiModelPathBox.Name = "_aiModelPathBox";
        _aiModelPathBox.PlaceholderText = "C:\\path\\to\\model.gguf";
        _aiModelPathBox.Size = new Size(200, 23);
        _aiModelPathBox.TabIndex = 5;
        _aiModelPathBox.Visible = false;
        // 
        // _btnAiBrowse
        // 
        _btnAiBrowse.Location = new Point(2, 31);
        _btnAiBrowse.Margin = new Padding(2, 4, 2, 0);
        _btnAiBrowse.Name = "_btnAiBrowse";
        _btnAiBrowse.Size = new Size(60, 23);
        _btnAiBrowse.TabIndex = 6;
        _btnAiBrowse.Text = "Browse…";
        _btnAiBrowse.Visible = false;
        // 
        // _lblAiModel
        // 
        _lblAiModel.Anchor = AnchorStyles.Left;
        _lblAiModel.AutoSize = true;
        _lblAiModel.Location = new Point(70, 37);
        _lblAiModel.Margin = new Padding(6, 9, 0, 0);
        _lblAiModel.Name = "_lblAiModel";
        _lblAiModel.Size = new Size(44, 15);
        _lblAiModel.TabIndex = 7;
        _lblAiModel.Text = "Model:";
        // 
        // _aiModelCombo
        // 
        _aiModelCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _aiModelCombo.Items.AddRange(new object[] { "claude-haiku-4-5-20251001", "claude-sonnet-4-5-20250929", "claude-opus-4-1-20250805" });
        _aiModelCombo.Location = new Point(116, 31);
        _aiModelCombo.Margin = new Padding(2, 4, 2, 0);
        _aiModelCombo.Name = "_aiModelCombo";
        _aiModelCombo.Size = new Size(120, 23);
        _aiModelCombo.TabIndex = 8;
        // 
        // _chkAiAutoExec
        // 
        _chkAiAutoExec.Location = new Point(244, 32);
        _chkAiAutoExec.Margin = new Padding(6, 5, 0, 0);
        _chkAiAutoExec.Name = "_chkAiAutoExec";
        _chkAiAutoExec.Size = new Size(80, 20);
        _chkAiAutoExec.TabIndex = 9;
        _chkAiAutoExec.Text = "Auto-exec";
        // 
        // _bottomPanel
        // 
        _bottomPanel.Controls.Add(_contentSplit);
        _bottomPanel.Controls.Add(_debugLog);
        _bottomPanel.Controls.Add(_finderPanel);
        _bottomPanel.Controls.Add(_inspectButtons);
        _bottomPanel.Dock = DockStyle.Fill;
        _bottomPanel.Location = new Point(0, 0);
        _bottomPanel.Name = "_bottomPanel";
        _bottomPanel.Size = new Size(1300, 419);
        _bottomPanel.TabIndex = 0;
        // 
        // _finderPanel
        // 
        _finderPanel.Controls.Add(_lblFinderSearch);
        _finderPanel.Controls.Add(_finderSearchBox);
        _finderPanel.Controls.Add(_btnFindWindow);
        _finderPanel.Controls.Add(_btnFindElement);
        _finderPanel.Controls.Add(_btnFindAll);
        _finderPanel.Controls.Add(_lblExcludeNames);
        _finderPanel.Controls.Add(_finderExcludeNames);
        _finderPanel.Controls.Add(_lblIncludeTypes);
        _finderPanel.Controls.Add(_finderIncludeTypes);
        _finderPanel.Dock = DockStyle.Top;
        _finderPanel.Location = new Point(0, 40);
        _finderPanel.Name = "_finderPanel";
        _finderPanel.Size = new Size(1300, 36);
        _finderPanel.TabIndex = 2;
        // 
        // _lblFinderSearch
        // 
        _lblFinderSearch.Anchor = AnchorStyles.Left;
        _lblFinderSearch.AutoSize = true;
        _lblFinderSearch.Location = new Point(4, 11);
        _lblFinderSearch.Margin = new Padding(4, 8, 0, 0);
        _lblFinderSearch.Name = "_lblFinderSearch";
        _lblFinderSearch.Size = new Size(33, 15);
        _lblFinderSearch.TabIndex = 0;
        _lblFinderSearch.Text = "Find:";
        // 
        // _finderSearchBox
        // 
        _finderSearchBox.Location = new Point(39, 6);
        _finderSearchBox.Margin = new Padding(2, 6, 2, 0);
        _finderSearchBox.Name = "_finderSearchBox";
        _finderSearchBox.PlaceholderText = "Name or ID";
        _finderSearchBox.Size = new Size(140, 23);
        _finderSearchBox.TabIndex = 1;
        // 
        // _btnFindWindow
        // 
        _btnFindWindow.Location = new Point(184, 3);
        _btnFindWindow.Name = "_btnFindWindow";
        _btnFindWindow.Size = new Size(75, 23);
        _btnFindWindow.TabIndex = 2;
        _btnFindWindow.Text = "Find Window";
        // 
        // _btnFindElement
        // 
        _btnFindElement.Location = new Point(265, 3);
        _btnFindElement.Name = "_btnFindElement";
        _btnFindElement.Size = new Size(75, 23);
        _btnFindElement.TabIndex = 3;
        _btnFindElement.Text = "Find Element";
        // 
        // _btnFindAll
        // 
        _btnFindAll.Location = new Point(346, 3);
        _btnFindAll.Name = "_btnFindAll";
        _btnFindAll.Size = new Size(75, 23);
        _btnFindAll.TabIndex = 4;
        _btnFindAll.Text = "Find All";
        // 
        // _lblExcludeNames
        // 
        _lblExcludeNames.Anchor = AnchorStyles.Left;
        _lblExcludeNames.AutoSize = true;
        _lblExcludeNames.Location = new Point(432, 11);
        _lblExcludeNames.Margin = new Padding(8, 8, 0, 0);
        _lblExcludeNames.Name = "_lblExcludeNames";
        _lblExcludeNames.Size = new Size(50, 15);
        _lblExcludeNames.TabIndex = 5;
        _lblExcludeNames.Text = "Exclude:";
        // 
        // _finderExcludeNames
        // 
        _finderExcludeNames.Location = new Point(484, 6);
        _finderExcludeNames.Margin = new Padding(2, 6, 2, 0);
        _finderExcludeNames.Name = "_finderExcludeNames";
        _finderExcludeNames.PlaceholderText = "name1,name2";
        _finderExcludeNames.Size = new Size(120, 23);
        _finderExcludeNames.TabIndex = 6;
        // 
        // _lblIncludeTypes
        // 
        _lblIncludeTypes.Anchor = AnchorStyles.Left;
        _lblIncludeTypes.AutoSize = true;
        _lblIncludeTypes.Location = new Point(614, 11);
        _lblIncludeTypes.Margin = new Padding(8, 8, 0, 0);
        _lblIncludeTypes.Name = "_lblIncludeTypes";
        _lblIncludeTypes.Size = new Size(40, 15);
        _lblIncludeTypes.TabIndex = 7;
        _lblIncludeTypes.Text = "Types:";
        // 
        // _finderIncludeTypes
        // 
        _finderIncludeTypes.Location = new Point(656, 6);
        _finderIncludeTypes.Margin = new Padding(2, 6, 2, 0);
        _finderIncludeTypes.Name = "_finderIncludeTypes";
        _finderIncludeTypes.PlaceholderText = "Button,Edit,Menu";
        _finderIncludeTypes.Size = new Size(150, 23);
        _finderIncludeTypes.TabIndex = 8;
        // 
        // _rootSplit
        // 
        _rootSplit.Dock = DockStyle.Fill;
        _rootSplit.Location = new Point(0, 24);
        _rootSplit.Name = "_rootSplit";
        _rootSplit.Orientation = Orientation.Horizontal;
        // 
        // _rootSplit.Panel1
        // 
        _rootSplit.Panel1.Controls.Add(_topPanel);
        // 
        // _rootSplit.Panel2
        // 
        _rootSplit.Panel2.Controls.Add(_bottomPanel);
        _rootSplit.Size = new Size(1300, 725);
        _rootSplit.SplitterDistance = 302;
        _rootSplit.TabIndex = 0;
        // 
        // StartupForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1300, 749);
        Controls.Add(_rootSplit);
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;
        Name = "StartupForm";
        Text = "ApexUIBridge";
        _menuStrip.ResumeLayout(false);
        _menuStrip.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_grid).EndInit();
        _topPanel.ResumeLayout(false);
        _topPanel.PerformLayout();
        _inspectButtons.ResumeLayout(false);
        _inspectButtons.PerformLayout();
        _inspectSplit.Panel1.ResumeLayout(false);
        _inspectSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_inspectSplit).EndInit();
        _inspectSplit.ResumeLayout(false);
        _contentSplit.Panel1.ResumeLayout(false);
        _contentSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_contentSplit).EndInit();
        _contentSplit.ResumeLayout(false);
        _aiChatPanel.ResumeLayout(false);
        _aiChatPanel.PerformLayout();
        _aiInputPanel.ResumeLayout(false);
        _aiInputPanel.PerformLayout();
        _aiButtonPanel.ResumeLayout(false);
        _aiStatusStrip.ResumeLayout(false);
        _aiStatusStrip.PerformLayout();
        _aiSettingsPanel.ResumeLayout(false);
        _aiSettingsPanel.PerformLayout();
        _bottomPanel.ResumeLayout(false);
        _finderPanel.ResumeLayout(false);
        _finderPanel.PerformLayout();
        _rootSplit.Panel1.ResumeLayout(false);
        _rootSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_rootSplit).EndInit();
        _rootSplit.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    // Top panel - Process selection
    private TextBox _filter;
    private CheckBox _windowedOnly;
    private DataGridView _grid;
    private DataGridViewTextBoxColumn _colPid;
    private DataGridViewTextBoxColumn _colElementId;
    private DataGridViewTextBoxColumn _colWindowTitle;
    private FlowLayoutPanel _processButtons;
    private Button _btnRefresh;
    private Button _btnPick;
    private Button _btnInspect;
    private Panel _topPanel;

    // Bottom panel - Element inspection
    private TreeView _tree;
    private ListView _details;
    private ColumnHeader _colProperty;
    private ColumnHeader _colValue;
    private FlowLayoutPanel _inspectButtons;
    private Button _btnRefreshTree;
    private Button _btnCopyDetails;
    private Button _btnCopyJson;
    private Button _btnCopyState;
    private Button _btnCapture;
    private CheckBox _chkHover;
    private CheckBox _chkHighlight;
    private CheckBox _chkFocus;
    private CheckBox _chkXPath;
    private Label _lblElementId;
    private TextBox _elementIdBox;
    private Button _clickButton;
    private ListBox _debugLog;
    private SplitContainer _inspectSplit;
    private SplitContainer _contentSplit;
    private Panel _bottomPanel;

    // Finder panel
    private FlowLayoutPanel _finderPanel;
    private Label _lblFinderSearch;
    private TextBox _finderSearchBox;
    private Button _btnFindWindow;
    private Button _btnFindElement;
    private Button _btnFindAll;
    private Label _lblExcludeNames;
    private TextBox _finderExcludeNames;
    private Label _lblIncludeTypes;
    private TextBox _finderIncludeTypes;

    // AI Chat panel
    private Panel _aiChatPanel;
    private FlowLayoutPanel _aiSettingsPanel;
    private Label _lblAiProvider;
    private ComboBox _aiProviderCombo;
    private Label _lblAiApiKey;
    private TextBox _aiApiKeyBox;
    private Label _lblAiModelPath;
    private TextBox _aiModelPathBox;
    private Button _btnAiBrowse;
    private Label _lblAiModel;
    private ComboBox _aiModelCombo;
    private CheckBox _chkAiAutoExec;
    private TextBox _aiSystemBox;
    private RichTextBox _aiOutput;
    private Panel _aiInputPanel;
    private TextBox _aiInput;
    private FlowLayoutPanel _aiButtonPanel;
    private Button _btnAiSend;
    private Button _btnAiStream;
    private Button _btnAiClear;
    private Button _btnAiStop;
    private StatusStrip _aiStatusStrip;
    private ToolStripStatusLabel _aiStatusLabel;
    private ToolStripStatusLabel _aiIndicatorLabel;
    private ToolStripStatusLabel _aiStatsLabel;
    private ToolStripStatusLabel _aiSysStatsLabel;

    // Root layout
    private SplitContainer _rootSplit;
    private System.Windows.Forms.Timer _windowChangeTimer;

    // Menu
    private MenuStrip _menuStrip;
    private ToolStripMenuItem _menuTools;
    private ToolStripMenuItem _menuTestWinForms;
    private ToolStripMenuItem _menuTestEcommerce;
    private ToolStripMenuItem _menuTestWpf;
    private ToolStripMenuItem _menuTestCustomWindow;
    private ToolStripSeparator _menuToolsSep1;
    private ToolStripMenuItem _menuListWindows;
    private ToolStripMenuItem _menuScanWindow;
    private ToolStripSeparator _menuToolsSep2;
    private ToolStripMenuItem _menuAiChat;
    private ToolStripSeparator _menuToolsSep3;
    private ToolStripMenuItem _menuAiSettings;
    private ToolStripMenuItem _menuToolStripMenuItem;
}
