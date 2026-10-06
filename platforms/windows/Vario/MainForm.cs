namespace Vario;

internal sealed class MainForm : Form
{
    private const int MaximumVoiceCount = 200;

    private readonly IVoiceRegistry registry;
    private readonly SettingsStore settingsStore;
    private VarioSettings preferences;
    private readonly MenuStrip settingsMenu = new();
    private readonly ToolStripMenuItem settingsMenuItem = new();
    private readonly ToolStripMenuItem themeMenu = new();
    private readonly ToolStripMenuItem lightThemeItem = new();
    private readonly ToolStripMenuItem darkThemeItem = new();
    private readonly ToolStripMenuItem languageMenu = new();
    private readonly ToolStripMenuItem systemLanguageItem = new();
    private readonly ToolStripMenuItem englishLanguageItem = new();
    private readonly ToolStripMenuItem polishLanguageItem = new();
    private readonly ToolStripMenuItem showHintsItem = new();
    private readonly List<Action> localizedText = new();
    private readonly Label availableHelp = CreateHelpLabel();
    private readonly Label installedHelp = CreateHelpLabel();
    private readonly TriStateTreeView availableTree = new()
    {
        FullRowSelect = true,
        HideSelection = false,
        ShowNodeToolTips = true
    };
    private readonly TriStateTreeView installedTree = new()
    {
        FullRowSelect = true,
        HideSelection = false,
        ShowNodeToolTips = true
    };
    private readonly NumericUpDown inflectionValue = new()
    {
        Minimum = 0,
        Maximum = 100,
        Increment = 5,
        TextAlign = HorizontalAlignment.Right,
        Width = 90
    };
    private readonly Label inflectionVoiceLabel = new() { AutoSize = true };
    private readonly CheckBox sonicCheck = new() { AutoSize = true };
    private readonly RadioButton sonicModeNvda = new() { AutoSize = true };
    private readonly RadioButton sonicModeLegacy = new() { AutoSize = true };
    private readonly RadioButton sonicModeSmooth = new() { AutoSize = true };
    private readonly AnnouncingLabel statusLabel = new() { AutoSize = false, AutoEllipsis = true };

    private IReadOnlyList<string> catalogVoices = Array.Empty<string>();
    private IReadOnlyList<string> catalogVariants = Array.Empty<string>();
    private readonly List<string> pendingOrder = new();
    private readonly Dictionary<string, int> pendingInflections = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> baselineInflections = new(StringComparer.OrdinalIgnoreCase);
    private bool baselineSonic;
    private SonicMode baselineSonicMode;
    private bool loading;
    private bool updatingInflection;
    private bool dirty;
    private bool allowClose;

    internal MainForm(IVoiceRegistry? registry = null, SettingsStore? settingsStore = null)
    {
        this.registry = registry ?? new RegistryService();
        this.settingsStore = settingsStore ?? new SettingsStore(AppContext.BaseDirectory, this.registry.ReadLegacySpeedSettings);
        LoadedSettings loadedSettings = this.settingsStore.Load();
        preferences = loadedSettings.Value;
        UiText.SetLanguage(preferences.Language);
        if (loadedSettings.NeedsInitialization)
            this.settingsStore.Save(preferences);
        Text = UiText.Title;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(840, 680);
        ClientSize = new Size(960, 740);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);
        BuildInterface();
        FormClosing += OnFormClosing;
        FormClosed += (_, _) => this.registry.Dispose();
        Shown += (_, _) => Appearance.Apply(this, settingsMenu, preferences.Theme);
        LoadData();
        if (loadedSettings.MigratedLegacy)
            SetStatus(UiText.SettingsMigrated);
    }

    private void BuildInterface()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 7
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label architecture = new()
        {
            Text = UiText.Architecture(registry.ArchitectureName),
            AutoSize = true,
            MaximumSize = new Size(920, 0),
            Margin = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(architecture, 0, 0);

        TableLayoutPanel treesLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        treesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        treesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        GroupBox availableGroup = new()
        {
            Text = UiText.AvailableGroup,
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 6, 0)
        };
        TableLayoutPanel availableLayout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        availableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        availableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        availableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        availableHelp.Text = UiText.AvailableTreeHelp;
        availableTree.Dock = DockStyle.Fill;
        availableTree.TabIndex = 0;
        availableTree.AccessibleName = UiText.AvailableGroup;
        availableTree.CheckStateChanged += OnTreeCheckStateChanged;
        Button addButton = new() { Text = UiText.AddSelected, AutoSize = true, Anchor = AnchorStyles.Right, TabIndex = 1 };
        addButton.Click += (_, _) => AddCheckedVoices();
        availableLayout.Controls.Add(availableHelp, 0, 0);
        availableLayout.Controls.Add(availableTree, 0, 1);
        availableLayout.Controls.Add(addButton, 0, 2);
        availableGroup.Controls.Add(availableLayout);

        GroupBox installedGroup = new()
        {
            Text = UiText.InstalledGroup,
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            Margin = new Padding(6, 0, 0, 0)
        };
        TableLayoutPanel installedLayout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        installedLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        installedLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        installedLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        installedHelp.Text = UiText.InstalledTreeHelp;
        installedTree.Dock = DockStyle.Fill;
        installedTree.TabIndex = 2;
        installedTree.AccessibleName = UiText.InstalledGroup;
        installedTree.CheckStateChanged += OnTreeCheckStateChanged;
        installedTree.AfterSelect += (_, _) => UpdateInflectionEditor();
        installedTree.KeyDown += OnInstalledTreeKeyDown;
        Button removeButton = new() { Text = UiText.RemoveSelected, AutoSize = true, Anchor = AnchorStyles.Right, TabIndex = 3 };
        removeButton.Click += (_, _) => RemoveCheckedVoices();
        installedLayout.Controls.Add(installedHelp, 0, 0);
        installedLayout.Controls.Add(installedTree, 0, 1);
        installedLayout.Controls.Add(removeButton, 0, 2);
        installedGroup.Controls.Add(installedLayout);

        treesLayout.Controls.Add(availableGroup, 0, 0);
        treesLayout.Controls.Add(installedGroup, 1, 0);
        root.Controls.Add(treesLayout, 0, 1);

        GroupBox inflectionGroup = new()
        {
            Text = UiText.InflectionGroup,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            Margin = new Padding(0, 10, 0, 0)
        };
        TableLayoutPanel inflectionLayout = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2
        };
        inflectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inflectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        inflectionVoiceLabel.Text = UiText.SelectVoiceForInflection;
        inflectionVoiceLabel.MaximumSize = new Size(760, 0);
        inflectionLayout.Controls.Add(inflectionVoiceLabel, 0, 0);
        inflectionLayout.SetColumnSpan(inflectionVoiceLabel, 2);
        Label inflectionLabel = new() { Text = UiText.Inflection, AutoSize = true, Anchor = AnchorStyles.Left };
        inflectionLabel.Click += (_, _) => inflectionValue.Focus();
        inflectionValue.Enabled = false;
        inflectionValue.TabIndex = 4;
        inflectionValue.AccessibleName = UiText.Inflection;
        inflectionValue.AccessibleDescription = UiText.InflectionHelp;
        inflectionValue.ValueChanged += (_, _) => ChangeInflection();
        inflectionLayout.Controls.Add(inflectionLabel, 0, 1);
        inflectionLayout.Controls.Add(inflectionValue, 1, 1);
        inflectionGroup.Controls.Add(inflectionLayout);
        root.Controls.Add(inflectionGroup, 0, 2);

        sonicCheck.Text = UiText.Sonic;
        sonicCheck.TabIndex = 5;
        sonicCheck.Margin = new Padding(0, 10, 0, 0);
        sonicCheck.CheckedChanged += (_, _) => { if (!loading) UpdateDirty(); };
        root.Controls.Add(sonicCheck, 0, 3);

        GroupBox sonicModeGroup = new()
        {
            Text = UiText.SonicModeGroup,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            Margin = new Padding(0, 4, 0, 8),
            TabStop = false
        };
        FlowLayoutPanel sonicModes = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        sonicModeNvda.Text = UiText.SonicModeNvda;
        sonicModeNvda.TabIndex = 7;
        sonicModeNvda.CheckedChanged += (_, _) => { if (!loading) UpdateDirty(); };
        sonicModeLegacy.Text = UiText.SonicModeLegacy;
        sonicModeLegacy.TabIndex = 8;
        sonicModeLegacy.CheckedChanged += (_, _) => { if (!loading) UpdateDirty(); };
        sonicModeSmooth.Text = UiText.SonicModeSmooth;
        sonicModeSmooth.TabIndex = 6;
        sonicModeSmooth.Checked = true;
        sonicModeSmooth.CheckedChanged += (_, _) => { if (!loading) UpdateDirty(); };
        sonicModes.Controls.Add(sonicModeSmooth);
        sonicModes.Controls.Add(sonicModeNvda);
        sonicModes.Controls.Add(sonicModeLegacy);
        sonicModeGroup.Controls.Add(sonicModes);
        root.Controls.Add(sonicModeGroup, 0, 4);

        FlowLayoutPanel buttons = new()
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false
        };
        Button closeButton = new() { Text = UiText.Close, AutoSize = true, TabIndex = 11 };
        Button reloadButton = new() { Text = UiText.Reload, AutoSize = true, TabIndex = 10 };
        Button applyButton = new() { Text = UiText.Apply, AutoSize = true, TabIndex = 9 };
        closeButton.Click += (_, _) => { allowClose = !dirty || Confirm(UiText.ConfirmClose); if (allowClose) Close(); };
        reloadButton.Click += (_, _) => { if (!dirty || Confirm(UiText.ConfirmReload)) LoadData(); };
        applyButton.Click += (_, _) => ApplyChanges();
        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(reloadButton);
        buttons.Controls.Add(applyButton);
        root.Controls.Add(buttons, 0, 5);

        statusLabel.Text = UiText.Ready;
        statusLabel.Height = 34;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.AccessibleRole = AccessibleRole.StaticText;
        statusLabel.TabIndex = 12;
        root.Controls.Add(statusLabel, 0, 6);

        AcceptButton = applyButton;
        CancelButton = closeButton;
        Controls.Add(root);
        BuildSettingsMenu();
        localizedText.Add(() =>
        {
            architecture.Text = UiText.Architecture(registry.ArchitectureName);
            availableGroup.Text = UiText.AvailableGroup;
            installedGroup.Text = UiText.InstalledGroup;
            availableHelp.Text = UiText.AvailableTreeHelp;
            installedHelp.Text = UiText.InstalledTreeHelp;
            addButton.Text = UiText.AddSelected;
            removeButton.Text = UiText.RemoveSelected;
            inflectionGroup.Text = UiText.InflectionGroup;
            inflectionLabel.Text = UiText.Inflection;
            sonicCheck.Text = UiText.Sonic;
            sonicModeGroup.Text = UiText.SonicModeGroup;
            sonicModeSmooth.Text = UiText.SonicModeSmooth;
            sonicModeNvda.Text = UiText.SonicModeNvda;
            sonicModeLegacy.Text = UiText.SonicModeLegacy;
            closeButton.Text = UiText.Close;
            reloadButton.Text = UiText.Reload;
            applyButton.Text = UiText.Apply;
        });
        RefreshInterfacePreferences();
    }

    private static Label CreateHelpLabel() => new()
    {
        AutoSize = true,
        MaximumSize = new Size(420, 0),
        AccessibleRole = AccessibleRole.StaticText,
        TabStop = false,
        Margin = new Padding(0, 0, 0, 6)
    };

    private SonicMode SelectedSonicMode => sonicModeSmooth.Checked ? SonicMode.Smooth
        : sonicModeNvda.Checked ? SonicMode.Nvda : SonicMode.Legacy;

    private void BuildSettingsMenu()
    {
        lightThemeItem.Click += (_, _) => SaveInterfacePreferences(preferences with { Theme = AppTheme.Light });
        darkThemeItem.Click += (_, _) => SaveInterfacePreferences(preferences with { Theme = AppTheme.Dark });
        systemLanguageItem.Click += (_, _) => SaveInterfacePreferences(preferences with { Language = AppLanguage.System });
        englishLanguageItem.Click += (_, _) => SaveInterfacePreferences(preferences with { Language = AppLanguage.English });
        polishLanguageItem.Click += (_, _) => SaveInterfacePreferences(preferences with { Language = AppLanguage.Polish });
        showHintsItem.Click += (_, _) => SaveInterfacePreferences(preferences with { ShowHints = !preferences.ShowHints });
        themeMenu.DropDownItems.AddRange(new ToolStripItem[] { lightThemeItem, darkThemeItem });
        languageMenu.DropDownItems.AddRange(new ToolStripItem[] { systemLanguageItem, englishLanguageItem, polishLanguageItem });
        settingsMenuItem.DropDownItems.AddRange(new ToolStripItem[] { themeMenu, languageMenu, showHintsItem });
        settingsMenu.Items.Add(settingsMenuItem);
        settingsMenu.TabStop = false;
        MainMenuStrip = settingsMenu;
        Controls.Add(settingsMenu);
    }

    private void SaveInterfacePreferences(VarioSettings updatedPreferences)
    {
        try
        {
            settingsStore.Save(updatedPreferences);
            preferences = updatedPreferences;
            RefreshInterfacePreferences();
            SetStatus(UiText.PreferencesSaved);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, UiText.PreferencesSaveFailed(settingsStore.FilePath) +
                Environment.NewLine + exception.Message, UiText.Warning, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshInterfacePreferences()
    {
        SuspendLayout();
        try
        {
            UiText.SetLanguage(preferences.Language);
            Text = UiText.Title;
            foreach (Action updateText in localizedText)
                updateText();
            settingsMenu.AccessibleName = UiText.SettingsMenuName;
            settingsMenuItem.Text = UiText.SettingsMenu;
            themeMenu.Text = UiText.ThemeMenu;
            lightThemeItem.Text = UiText.LightTheme;
            darkThemeItem.Text = UiText.DarkTheme;
            languageMenu.Text = UiText.LanguageMenu;
            systemLanguageItem.Text = UiText.SystemLanguage;
            englishLanguageItem.Text = UiText.EnglishLanguage;
            polishLanguageItem.Text = UiText.PolishLanguage;
            showHintsItem.Text = UiText.ShowHints;
            lightThemeItem.Checked = preferences.Theme == AppTheme.Light;
            darkThemeItem.Checked = preferences.Theme == AppTheme.Dark;
            systemLanguageItem.Checked = preferences.Language == AppLanguage.System;
            englishLanguageItem.Checked = preferences.Language == AppLanguage.English;
            polishLanguageItem.Checked = preferences.Language == AppLanguage.Polish;
            showHintsItem.Checked = preferences.ShowHints;
            availableTree.AccessibleName = UiText.AvailableGroup;
            installedTree.AccessibleName = UiText.InstalledGroup;
            availableHelp.Visible = preferences.ShowHints;
            installedHelp.Visible = preferences.ShowHints;
            availableTree.ShowNodeToolTips = preferences.ShowHints;
            installedTree.ShowNodeToolTips = preferences.ShowHints;
            inflectionValue.AccessibleDescription = preferences.ShowHints ? UiText.InflectionHelp : string.Empty;
            foreach (TreeNode languageNode in installedTree.Nodes)
            {
                languageNode.ToolTipText = preferences.ShowHints ? languageNode.Name : string.Empty;
                foreach (TreeNode voiceNode in languageNode.Nodes)
                {
                    if (voiceNode.Tag is not VoiceNodeData voice || !pendingInflections.TryGetValue(voice.Value, out int value))
                        continue;
                    voiceNode.Text = UiText.VoiceWithInflection(voice.Value, value);
                    voiceNode.ToolTipText = preferences.ShowHints ? UiText.InflectionFor(voice.Value, value) : string.Empty;
                }
            }
            foreach (TreeNode languageNode in availableTree.Nodes)
            {
                languageNode.ToolTipText = preferences.ShowHints ? languageNode.Name : string.Empty;
                foreach (TreeNode voiceNode in languageNode.Nodes)
                    voiceNode.ToolTipText = preferences.ShowHints ? voiceNode.Name : string.Empty;
            }
            UpdateInflectionEditor();
            Appearance.Apply(this, settingsMenu, preferences.Theme);
        }
        finally
        {
            ResumeLayout(performLayout: true);
        }
    }

    private void LoadData()
    {
        loading = true;
        try
        {
            var available = registry.DiscoverAvailableVoices();
            catalogVoices = available.Voices;
            catalogVariants = available.Variants;

            pendingOrder.Clear();
            pendingInflections.Clear();
            foreach (VoiceConfiguration voice in registry.ReadInstalledVoices())
            {
                if (pendingInflections.ContainsKey(voice.Name))
                    continue;
                pendingOrder.Add(voice.Name);
                pendingInflections.Add(voice.Name, voice.Inflection);
            }

            baselineInflections = new Dictionary<string, int>(pendingInflections, StringComparer.OrdinalIgnoreCase);
            preferences = settingsStore.Load().Value;
            RefreshInterfacePreferences();
            baselineSonic = preferences.SonicBoost;
            baselineSonicMode = preferences.SonicMode;
            sonicCheck.Checked = baselineSonic;
            sonicModeSmooth.Checked = baselineSonicMode == SonicMode.Smooth;
            sonicModeNvda.Checked = baselineSonicMode == SonicMode.Nvda;
            sonicModeLegacy.Checked = baselineSonicMode == SonicMode.Legacy;
            RebuildTrees();
            dirty = false;
            SetStatus(UiText.Ready);
        }
        catch (Exception ex)
        {
            SetStatus(UiText.LoadFailed + " " + ex.Message);
            MessageBox.Show(this, UiText.LoadFailed + Environment.NewLine + ex.Message, UiText.Warning,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            loading = false;
        }
    }

    private void RebuildTrees(IEnumerable<string>? additionallyExpandedInstalled = null)
    {
        HashSet<string> expandedAvailable = ExpandedGroups(availableTree);
        HashSet<string> expandedInstalled = ExpandedGroups(installedTree);
        if (additionallyExpandedInstalled is not null)
            expandedInstalled.UnionWith(additionallyExpandedInstalled);

        PopulateAvailableTree(expandedAvailable);
        PopulateInstalledTree(expandedInstalled);
        UpdateInflectionEditor();
    }

    private void PopulateAvailableTree(HashSet<string> expandedGroups)
    {
        availableTree.BeginUpdate();
        try
        {
            availableTree.Nodes.Clear();
            foreach (string voice in catalogVoices.OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase))
            {
                List<string> choices = new() { voice };
                choices.AddRange(catalogVariants.Select(variant => voice + "+" + variant));
                choices.RemoveAll(value => pendingInflections.ContainsKey(value));
                if (choices.Count == 0)
                    continue;

                TreeNode languageNode = new(voice) { Name = voice, ToolTipText = preferences.ShowHints ? voice : string.Empty, StateImageIndex = 0 };
                foreach (string choice in choices)
                    languageNode.Nodes.Add(CreateVoiceNode(choice, installed: false));
                availableTree.Nodes.Add(languageNode);
                if (expandedGroups.Contains(voice))
                    languageNode.Expand();
            }
        }
        finally
        {
            availableTree.EndUpdate();
        }
    }

    private void PopulateInstalledTree(HashSet<string> expandedGroups)
    {
        installedTree.BeginUpdate();
        try
        {
            installedTree.Nodes.Clear();
            foreach (IGrouping<string, string> group in pendingOrder
                .Where(pendingInflections.ContainsKey)
                .GroupBy(BaseVoice, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                TreeNode languageNode = new(group.Key) { Name = group.Key, ToolTipText = preferences.ShowHints ? group.Key : string.Empty, StateImageIndex = 0 };
                foreach (string voice in group.OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase))
                    languageNode.Nodes.Add(CreateVoiceNode(voice, installed: true));
                installedTree.Nodes.Add(languageNode);
                if (expandedGroups.Contains(group.Key))
                    languageNode.Expand();
            }
        }
        finally
        {
            installedTree.EndUpdate();
        }
    }

    private TreeNode CreateVoiceNode(string voice, bool installed)
    {
        string text = installed
            ? UiText.VoiceWithInflection(voice, pendingInflections[voice])
            : voice;
        return new TreeNode(text)
        {
            Name = voice,
            Tag = new VoiceNodeData(voice),
            ToolTipText = !preferences.ShowHints ? string.Empty
                : installed ? UiText.InflectionFor(voice, pendingInflections[voice]) : voice,
            StateImageIndex = 0
        };
    }

    private void AddCheckedVoices()
    {
        string[] selected = CheckedVoices(availableTree).ToArray();
        if (selected.Length == 0)
        {
            SetStatus(UiText.NothingSelected);
            return;
        }
        if (pendingInflections.Count + selected.Length > MaximumVoiceCount)
        {
            SetStatus(UiText.TooManyVoices(MaximumVoiceCount));
            MessageBox.Show(this, UiText.TooManyVoices(MaximumVoiceCount), UiText.Warning,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        foreach (string voice in selected)
        {
            if (pendingInflections.TryAdd(voice, 50))
                pendingOrder.Add(voice);
        }
        RebuildTrees(selected.Select(BaseVoice));
        SelectInstalledVoice(selected[0]);
        UpdateDirty();
        SetStatus(UiText.Added(selected.Length));
    }

    private void RemoveCheckedVoices()
    {
        string[] selected = CheckedVoices(installedTree).ToArray();
        if (selected.Length == 0)
        {
            SetStatus(UiText.NothingSelected);
            return;
        }
        if (!Confirm(UiText.ConfirmRemove(selected.Length)))
            return;
        RemoveVoices(selected);
    }

    private void OnInstalledTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Delete)
            return;
        e.Handled = true;
        e.SuppressKeyPress = true;

        TreeNode? selectedNode = installedTree.SelectedNode;
        if (selectedNode is null)
        {
            SetStatus(UiText.NothingSelected);
            return;
        }
        string[] voices = VoicesFromNode(selectedNode).ToArray();
        if (voices.Length == 0)
        {
            SetStatus(UiText.NothingSelected);
            return;
        }
        if (!e.Shift && !Confirm(UiText.ConfirmRemove(voices.Length)))
            return;
        RemoveVoices(voices);
    }

    private void RemoveVoices(IEnumerable<string> voices)
    {
        HashSet<string> removed = voices.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string voice in removed)
            pendingInflections.Remove(voice);
        pendingOrder.RemoveAll(voice => removed.Contains(voice));
        RebuildTrees();
        UpdateDirty();
        SetStatus(UiText.Removed(removed.Count));
    }

    private void UpdateInflectionEditor()
    {
        updatingInflection = true;
        try
        {
            if (installedTree.SelectedNode?.Tag is VoiceNodeData voice &&
                pendingInflections.TryGetValue(voice.Value, out int value))
            {
                inflectionVoiceLabel.Text = UiText.InflectionFor(voice.Value, value);
                inflectionValue.AccessibleName = UiText.InflectionFor(voice.Value, value);
                inflectionValue.Value = value;
                inflectionValue.Enabled = true;
            }
            else
            {
                inflectionVoiceLabel.Text = UiText.SelectVoiceForInflection;
                inflectionValue.AccessibleName = UiText.Inflection;
                inflectionValue.Value = 50;
                inflectionValue.Enabled = false;
            }
        }
        finally
        {
            updatingInflection = false;
        }
    }

    private void ChangeInflection()
    {
        if (updatingInflection || installedTree.SelectedNode?.Tag is not VoiceNodeData voice)
            return;
        int value = decimal.ToInt32(inflectionValue.Value);
        if (!pendingInflections.ContainsKey(voice.Value))
            return;
        pendingInflections[voice.Value] = value;
        installedTree.SelectedNode.Text = UiText.VoiceWithInflection(voice.Value, value);
        installedTree.SelectedNode.ToolTipText = preferences.ShowHints ? UiText.InflectionFor(voice.Value, value) : string.Empty;
        inflectionVoiceLabel.Text = UiText.InflectionFor(voice.Value, value);
        inflectionValue.AccessibleName = UiText.InflectionFor(voice.Value, value);
        UpdateDirty();
        SetStatus(UiText.InflectionChanged(voice.Value, value));
    }

    private void ApplyChanges()
    {
        try
        {
            VoiceConfiguration[] voices = pendingOrder
                .Where(pendingInflections.ContainsKey)
                .Select(name => new VoiceConfiguration(name, pendingInflections[name]))
                .ToArray();
            VarioSettings updatedPreferences = preferences with
            {
                SonicBoost = sonicCheck.Checked,
                SonicMode = SelectedSonicMode
            };
            registry.Apply(voices);
            settingsStore.Save(updatedPreferences);
            preferences = updatedPreferences;
            baselineInflections = new Dictionary<string, int>(pendingInflections, StringComparer.OrdinalIgnoreCase);
            baselineSonic = sonicCheck.Checked;
            baselineSonicMode = SelectedSonicMode;
            dirty = false;
            SetStatus(UiText.Saved(voices.Length));
        }
        catch (Exception ex)
        {
            SetStatus(UiText.SaveFailed + " " + ex.Message);
            MessageBox.Show(this, UiText.SaveFailed + Environment.NewLine + ex.Message, UiText.Warning,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnTreeCheckStateChanged(object? sender, TreeViewEventArgs e)
    {
        if (sender is not TriStateTreeView tree || e.Node is not TreeNode node)
            return;
        if (node.Nodes.Count > 0)
            SetDescendantChecks(tree, node, tree.GetCheckState(node));
        else
            UpdateParentCheck(tree, node.Parent);
    }

    private static void SetDescendantChecks(TriStateTreeView tree, TreeNode node, TreeNodeCheckState state)
    {
        foreach (TreeNode child in node.Nodes)
        {
            tree.SetCheckState(child, state);
            SetDescendantChecks(tree, child, state);
        }
    }

    private static void UpdateParentCheck(TriStateTreeView tree, TreeNode? parent)
    {
        while (parent is not null)
        {
            TreeNodeCheckState[] childStates = parent.Nodes.Cast<TreeNode>()
                .Select(tree.GetCheckState)
                .ToArray();
            TreeNodeCheckState parentState = childStates.All(state => state == TreeNodeCheckState.Checked)
                ? TreeNodeCheckState.Checked
                : childStates.All(state => state == TreeNodeCheckState.Unchecked)
                    ? TreeNodeCheckState.Unchecked
                    : TreeNodeCheckState.Partial;
            tree.SetCheckState(parent, parentState);
            parent = parent.Parent;
        }
    }

    private static IEnumerable<string> CheckedVoices(TriStateTreeView tree) =>
        tree.Nodes.Cast<TreeNode>()
            .SelectMany(parent => parent.Nodes.Cast<TreeNode>())
            .Where(node => tree.GetCheckState(node) == TreeNodeCheckState.Checked && node.Tag is VoiceNodeData)
            .Select(node => ((VoiceNodeData)node.Tag!).Value);

    private static IEnumerable<string> VoicesFromNode(TreeNode node)
    {
        if (node.Tag is VoiceNodeData voice)
        {
            yield return voice.Value;
            yield break;
        }
        foreach (TreeNode child in node.Nodes)
            if (child.Tag is VoiceNodeData childVoice)
                yield return childVoice.Value;
    }

    private static HashSet<string> ExpandedGroups(TreeView tree) =>
        tree.Nodes.Cast<TreeNode>()
            .Where(node => node.IsExpanded)
            .Select(node => node.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void SelectInstalledVoice(string voice)
    {
        foreach (TreeNode parent in installedTree.Nodes)
        {
            TreeNode? child = parent.Nodes.Cast<TreeNode>()
                .FirstOrDefault(node => node.Tag is VoiceNodeData data &&
                    data.Value.Equals(voice, StringComparison.OrdinalIgnoreCase));
            if (child is null)
                continue;
            parent.Expand();
            installedTree.SelectedNode = child;
            child.EnsureVisible();
            installedTree.Focus();
            return;
        }
    }

    private static string BaseVoice(string voice)
    {
        int plus = voice.IndexOf('+');
        return plus < 0 ? voice : voice[..plus];
    }

    private void UpdateDirty()
    {
        dirty = sonicCheck.Checked != baselineSonic ||
            SelectedSonicMode != baselineSonicMode ||
            pendingInflections.Count != baselineInflections.Count ||
            pendingInflections.Any(pair => !baselineInflections.TryGetValue(pair.Key, out int value) || value != pair.Value);
    }

    private void SetStatus(string text) => statusLabel.SetAndAnnounce(text);

    private bool Confirm(string text) => MessageBox.Show(this, text, UiText.Warning,
        MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (allowClose || !dirty)
            return;
        if (!Confirm(UiText.ConfirmClose))
            e.Cancel = true;
    }

    private sealed record VoiceNodeData(string Value);

    private sealed class AnnouncingLabel : Label
    {
        internal void SetAndAnnounce(string text)
        {
            Text = text;
            AccessibleName = text;
            AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        }
    }
}
