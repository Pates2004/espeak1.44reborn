namespace Vario;

internal sealed class MainForm : Form
{
    private readonly RegistryService registry = new();
    private readonly ComboBox voiceCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox variantCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListBox installedList = new() { SelectionMode = SelectionMode.MultiExtended, IntegralHeight = false };
    private readonly CheckBox sonicCheck = new() { AutoSize = true };
    private readonly AnnouncingLabel statusLabel = new() { AutoSize = false, AutoEllipsis = true };
    private bool dirty;
    private bool allowClose;

    internal MainForm()
    {
        Text = UiText.Title;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 520);
        ClientSize = new Size(760, 580);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);
        BuildInterface();
        FormClosing += OnFormClosing;
        FormClosed += (_, _) => registry.Dispose();
        LoadData();
    }

    private void BuildInterface()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label architecture = new()
        {
            Text = UiText.Architecture(registry.ArchitectureName),
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Margin = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(architecture, 0, 0);

        GroupBox addGroup = new() { Text = UiText.AddGroup, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        TableLayoutPanel addLayout = new() { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 2 };
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Label voiceLabel = new() { Text = UiText.Language, AutoSize = true };
        Label variantLabel = new() { Text = UiText.Variant, AutoSize = true };
        voiceLabel.TabIndex = 0;
        variantLabel.TabIndex = 2;
        voiceLabel.UseMnemonic = true;
        variantLabel.UseMnemonic = true;
        voiceCombo.Dock = DockStyle.Fill;
        variantCombo.Dock = DockStyle.Fill;
        voiceCombo.TabIndex = 1;
        variantCombo.TabIndex = 3;
        voiceLabel.Click += (_, _) => voiceCombo.Focus();
        variantLabel.Click += (_, _) => variantCombo.Focus();
        Button addButton = new() { Text = UiText.Add, AutoSize = true, Anchor = AnchorStyles.Bottom, TabIndex = 4 };
        addButton.Click += (_, _) => AddVoice();
        addLayout.Controls.Add(voiceLabel, 0, 0);
        addLayout.Controls.Add(variantLabel, 1, 0);
        addLayout.Controls.Add(voiceCombo, 0, 1);
        addLayout.Controls.Add(variantCombo, 1, 1);
        addLayout.Controls.Add(addButton, 2, 1);
        addGroup.Controls.Add(addLayout);
        root.Controls.Add(addGroup, 0, 1);

        GroupBox installedGroup = new() { Text = UiText.InstalledGroup, Dock = DockStyle.Fill, Padding = new Padding(10) };
        TableLayoutPanel installedLayout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        installedLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        installedLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        installedList.Dock = DockStyle.Fill;
        installedList.TabIndex = 5;
        Button removeButton = new() { Text = UiText.Remove, AutoSize = true, Anchor = AnchorStyles.Right, TabIndex = 6 };
        removeButton.Click += (_, _) => RemoveSelected();
        installedLayout.Controls.Add(installedList, 0, 0);
        installedLayout.Controls.Add(removeButton, 0, 1);
        installedGroup.Controls.Add(installedLayout);
        root.Controls.Add(installedGroup, 0, 2);

        sonicCheck.Text = UiText.Sonic;
        sonicCheck.TabIndex = 7;
        sonicCheck.Margin = new Padding(0, 10, 0, 8);
        sonicCheck.CheckedChanged += (_, _) => dirty = true;
        root.Controls.Add(sonicCheck, 0, 3);

        FlowLayoutPanel buttons = new() { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        Button closeButton = new() { Text = UiText.Close, AutoSize = true, TabIndex = 10 };
        Button reloadButton = new() { Text = UiText.Reload, AutoSize = true, TabIndex = 9 };
        Button applyButton = new() { Text = UiText.Apply, AutoSize = true, TabIndex = 8 };
        closeButton.Click += (_, _) => { allowClose = !dirty || Confirm(UiText.ConfirmClose); if (allowClose) Close(); };
        reloadButton.Click += (_, _) => { if (!dirty || Confirm(UiText.ConfirmReload)) LoadData(); };
        applyButton.Click += (_, _) => ApplyChanges();
        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(reloadButton);
        buttons.Controls.Add(applyButton);
        root.Controls.Add(buttons, 0, 4);

        statusLabel.Text = UiText.Ready;
        statusLabel.Height = 34;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.AccessibleRole = AccessibleRole.StaticText;
        statusLabel.TabIndex = 11;
        root.Controls.Add(statusLabel, 0, 5);

        AcceptButton = applyButton;
        CancelButton = closeButton;
        Controls.Add(root);
    }

    private void LoadData()
    {
        try
        {
            var available = registry.DiscoverAvailableVoices();
            voiceCombo.BeginUpdate();
            voiceCombo.Items.Clear();
            voiceCombo.Items.AddRange(available.Voices.Cast<object>().ToArray());
            voiceCombo.EndUpdate();
            if (voiceCombo.Items.Count > 0) voiceCombo.SelectedIndex = 0;

            variantCombo.BeginUpdate();
            variantCombo.Items.Clear();
            variantCombo.Items.Add(new VariantItem(string.Empty, UiText.NoVariant));
            foreach (string variant in available.Variants)
                variantCombo.Items.Add(new VariantItem(variant, variant));
            variantCombo.EndUpdate();
            variantCombo.SelectedIndex = 0;

            installedList.Items.Clear();
            foreach (string voice in registry.ReadInstalledVoices())
                installedList.Items.Add(voice);
            sonicCheck.Checked = registry.ReadSonicBoost();
            dirty = false;
            SetStatus(UiText.Ready);
        }
        catch (Exception ex)
        {
            SetStatus(UiText.LoadFailed + " " + ex.Message);
            MessageBox.Show(this, UiText.LoadFailed + Environment.NewLine + ex.Message, UiText.Warning,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddVoice()
    {
        if (voiceCombo.SelectedItem is not string voice) return;
        string variant = (variantCombo.SelectedItem as VariantItem)?.Value ?? string.Empty;
        string combined = string.IsNullOrEmpty(variant) ? voice : voice + "+" + variant;
        if (installedList.Items.Cast<string>().Contains(combined, StringComparer.OrdinalIgnoreCase))
        {
            SetStatus(UiText.Duplicate);
            return;
        }
        installedList.Items.Add(combined);
        installedList.SelectedItem = combined;
        dirty = true;
        SetStatus(UiText.Added(combined));
    }

    private void RemoveSelected()
    {
        object[] selected = installedList.SelectedItems.Cast<object>().ToArray();
        if (selected.Length == 0)
        {
            SetStatus(UiText.NothingSelected);
            return;
        }
        foreach (object item in selected) installedList.Items.Remove(item);
        dirty = true;
        SetStatus(UiText.Removed(selected.Length));
    }

    private void ApplyChanges()
    {
        try
        {
            string[] voices = installedList.Items.Cast<string>().ToArray();
            registry.Apply(voices, sonicCheck.Checked);
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

    private void SetStatus(string text)
    {
        statusLabel.SetAndAnnounce(text);
    }

    private bool Confirm(string text) => MessageBox.Show(this, text, UiText.Warning,
        MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (allowClose || !dirty) return;
        if (!Confirm(UiText.ConfirmClose)) e.Cancel = true;
    }

    private sealed record VariantItem(string Value, string Label)
    {
        public override string ToString() => Label;
    }

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
