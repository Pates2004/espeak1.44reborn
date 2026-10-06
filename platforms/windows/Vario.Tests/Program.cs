using System.Globalization;
using System.Reflection;
using System.Text;
using Vario;

internal static class Program
{
    private static int assertions;

    [STAThread]
    private static int Main(string[] arguments)
    {
        if (arguments.Length != 3)
        {
            Console.Error.WriteLine("Usage: Vario.Tests <repository> <fixture-directory> <UI-culture>");
            return 2;
        }
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(arguments[2]);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string fixtures = Path.GetFullPath(arguments[1]);
            Directory.CreateDirectory(fixtures);
            TestSettings(fixtures);
            TestLanguageSelection(arguments[2]);
            Check(Directory.Exists(Path.Combine(Path.GetFullPath(arguments[0]), "espeak-data", "voices")), "The repository contains voice definitions.");
            TestPendingUi(fixtures);
            Console.WriteLine($"PASS: {assertions} assertions, culture {arguments[2]}; no SAPI registry writes.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void TestSettings(string fixtures)
    {
        SettingsStore fresh = Store(fixtures, "fresh", new(false, false, SonicMode.Nvda));
        LoadedSettings initial = fresh.Load();
        Check(initial.NeedsInitialization && !initial.MigratedLegacy, "Fresh configuration needs initialization.");
        Check(initial.Value.SonicBoost && initial.Value.SonicMode == SonicMode.Smooth, "Fresh configuration uses enabled smooth mode.");
        Check(initial.Value.Theme == AppTheme.Light && initial.Value.Language == AppLanguage.System && initial.Value.ShowHints,
            "Fresh interface defaults.");
        fresh.Save(initial.Value);
        byte[] bytes = File.ReadAllBytes(fresh.FilePath);
        Check(bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe, "Settings are UTF-16LE with a BOM.");
        Check(fresh.Load().Value == initial.Value && !fresh.Load().NeedsInitialization, "Settings round trip through native INI API.");
        Check(Directory.GetFiles(Path.GetDirectoryName(fresh.FilePath)!).Length == 1, "No transactional temporary file remains.");

        foreach (SonicMode mode in new[] { SonicMode.Legacy, SonicMode.Nvda })
        foreach (bool boost in new[] { false, true })
        {
            SettingsStore legacy = Store(fixtures, $"legacy-{mode}-{boost}", new(true, boost, mode));
            LoadedSettings loaded = legacy.Load();
            Check(loaded.MigratedLegacy && loaded.Value.SonicBoost == boost && loaded.Value.SonicMode == mode,
                "Migration preserves existing boost and mode.");
            legacy.Save(loaded.Value);
            Check(legacy.Load().Value == loaded.Value && !legacy.Load().MigratedLegacy, "Migration is not repeated.");
        }

        SettingsStore partial = Store(fixtures, "partial", new(true, true, SonicMode.Nvda));
        File.WriteAllText(partial.FilePath, "[Settings]\r\nSonicMode=0\r\nCustom=retain me\r\n[Other]\r\nValue=żółć\r\n", Encoding.Unicode);
        LoadedSettings partialValue = partial.Load();
        Check(partialValue.Value.SonicMode == SonicMode.Legacy && partialValue.Value.SonicBoost,
            "Existing INI values take precedence over legacy values.");
        partial.Save(partialValue.Value with { Theme = AppTheme.Dark, Language = AppLanguage.Polish, ShowHints = false });
        string contents = File.ReadAllText(partial.FilePath);
        Check(contents.Contains("Custom=retain me") && contents.Contains("Value=żółć"), "Unknown INI keys and Unicode data survive saving.");
        VarioSettings saved = partial.Load().Value;
        Check(saved.Theme == AppTheme.Dark && saved.Language == AppLanguage.Polish && !saved.ShowHints, "Interface settings persist.");
        ExpectFailure<ArgumentOutOfRangeException>(() => partial.Save(saved with { SonicMode = (SonicMode)99 }));
        Check(File.ReadAllText(partial.FilePath) == contents, "Invalid settings do not alter existing data.");
        SettingsStore missing = new(Path.Combine(fixtures, "missing-parent"), () => new(false, false, SonicMode.Nvda));
        ExpectFailure<DirectoryNotFoundException>(() => missing.Save(initial.Value));
        Check(!Directory.Exists(Path.Combine(fixtures, "missing-parent")), "No fallback directory is created on save failure.");
    }

    private static void TestLanguageSelection(string culture)
    {
        UiText.SetLanguage(AppLanguage.System);
        Check(UiText.Title.Contains("menedżer") == culture.StartsWith("pl", StringComparison.OrdinalIgnoreCase),
            "System Polish selects Polish; other system languages select English.");
        UiText.SetLanguage(AppLanguage.Polish);
        Check(UiText.Title.Contains("menedżer") && UiText.InvalidInflection.Contains("Modulacja"), "Polish UI and validation messages.");
        UiText.SetLanguage(AppLanguage.English);
        Check(UiText.Title.Contains("voice manager") && UiText.InvalidInflection.Contains("modulation"), "English UI and validation messages.");
        foreach (AppLanguage language in new[] { AppLanguage.English, AppLanguage.Polish })
        {
            UiText.SetLanguage(language);
            foreach (PropertyInfo property in typeof(UiText).GetProperties(BindingFlags.Static | BindingFlags.NonPublic))
                if (property.PropertyType == typeof(string))
                    Check(!string.IsNullOrWhiteSpace((string?)property.GetValue(null)), $"Localized string {property.Name} is not empty.");
        }
    }

    private static void TestPendingUi(string fixtures)
    {
        SettingsStore store = Store(fixtures, "ui", new(false, false, SonicMode.Nvda));
        store.Save(store.Load().Value with { Language = AppLanguage.English });
        using TestVoiceRegistry registry = new();
        using MainForm form = new(registry, store);
        TriStateTreeView available = Field<TriStateTreeView>(form, "availableTree");
        TriStateTreeView installed = Field<TriStateTreeView>(form, "installedTree");
        Check(available.Nodes.Count > 0 && available.Nodes[0].Nodes.Count > 0, "The available voice tree is populated.");
        TreeNode candidate = available.Nodes[0].Nodes[0];
        available.SetCheckState(candidate, TreeNodeCheckState.Checked);
        Invoke(form, "AddCheckedVoices");
        TreeNode installedVoice = installed.SelectedNode ?? throw new InvalidOperationException("Added voice not selected.");
        NumericUpDown modulation = Field<NumericUpDown>(form, "inflectionValue");
        modulation.Value = 75;
        installed.SetCheckState(installedVoice, TreeNodeCheckState.Checked);
        Check(installedVoice.Parent is not null, "Installed voice belongs to a language.");
        installed.SetCheckState(installedVoice.Parent!, TreeNodeCheckState.Partial);
        installedVoice.Parent!.Expand();
        Field<RadioButton>(form, "sonicModeLegacy").Checked = true;
        Field<CheckBox>(form, "sonicCheck").Checked = false;
        var pending = Field<Dictionary<string, int>>(form, "pendingInflections").ToDictionary(pair => pair.Key, pair => pair.Value);

        Field<ToolStripMenuItem>(form, "darkThemeItem").PerformClick();
        Check(form.BackColor == (SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(32, 32, 32)),
            "Dark theme changes real control colors while respecting high contrast.");
        Check(form.MainMenuStrip == Field<MenuStrip>(form, "settingsMenu"), "Preferences use the standard keyboard-accessible menu.");
        Field<ToolStripMenuItem>(form, "polishLanguageItem").PerformClick();
        Field<ToolStripMenuItem>(form, "showHintsItem").PerformClick();
        Check(ReferenceEquals(installed.SelectedNode, installedVoice), "Changing preferences preserves the selected tree node.");
        Check(installed.GetCheckState(installedVoice) == TreeNodeCheckState.Checked &&
            installed.GetCheckState(installedVoice.Parent!) == TreeNodeCheckState.Partial, "Changing preferences preserves check states.");
        Check(installedVoice.Parent!.IsExpanded, "Changing preferences preserves expanded nodes.");
        Check(modulation.Value == 75 && Field<bool>(form, "dirty"), "Pending modulation and dirty state survive preference changes.");
        Check(pending.All(pair => Field<Dictionary<string, int>>(form, "pendingInflections")[pair.Key] == pair.Value),
            "Pending voice values are not reloaded.");
        Check(Field<RadioButton>(form, "sonicModeLegacy").Checked && !Field<CheckBox>(form, "sonicCheck").Checked,
            "Pending speed controls survive preference changes.");
        Check(store.Load().Value.SonicMode == SonicMode.Smooth && store.Load().Value.SonicBoost,
            "Interface changes do not prematurely apply pending speed settings.");
        Check(form.Text.Contains("menedżer") && installedVoice.Text.Contains("modulacja"), "Dynamic UI text changes language.");
        Check(!installed.ShowNodeToolTips && modulation.AccessibleDescription == string.Empty && installedVoice.ToolTipText == string.Empty,
            "Disabling hints removes optional accessible descriptions and tooltips.");
        Check(!string.IsNullOrWhiteSpace(installed.AccessibleName) && !string.IsNullOrWhiteSpace(modulation.AccessibleName),
            "Essential accessible names remain.");
        Check(store.Load().Value.Theme == AppTheme.Dark && store.Load().Value.Language == AppLanguage.Polish && !store.Load().Value.ShowHints,
            "Menu changes are saved immediately.");
        Field<ToolStripMenuItem>(form, "showHintsItem").PerformClick();
        Field<ToolStripMenuItem>(form, "lightThemeItem").PerformClick();
        Check(form.BackColor == SystemColors.Control && installed.BackColor == SystemColors.Window,
            "Light theme restores standard control and input colors.");
        Field<ToolStripMenuItem>(form, "englishLanguageItem").PerformClick();
        Check(installed.ShowNodeToolTips && !string.IsNullOrEmpty(modulation.AccessibleDescription), "Hints can be enabled again.");
        Check(form.Text.Contains("voice manager") && installedVoice.Text.Contains("modulation"), "Switching back to English updates dynamic text.");
    }

    private sealed class TestVoiceRegistry : IVoiceRegistry
    {
        public string ArchitectureName => Environment.Is64BitProcess ? "64-bit" : "32-bit";
        public IReadOnlyList<VoiceConfiguration> ReadInstalledVoices() => Array.Empty<VoiceConfiguration>();
        public (IReadOnlyList<string> Voices, IReadOnlyList<string> Variants) DiscoverAvailableVoices() =>
            (new[] { "en", "pl" }, new[] { "m1", "f1" });
        public LegacySpeedSettings ReadLegacySpeedSettings() => new(false, false, SonicMode.Nvda);
        public void Apply(IReadOnlyList<VoiceConfiguration> requestedVoices) =>
            throw new InvalidOperationException("The UI regression must not apply voice registration.");
        public void Dispose() { }
    }

    private static SettingsStore Store(string fixtures, string name, LegacySpeedSettings legacy)
    {
        string directory = Path.Combine(fixtures, name);
        Directory.CreateDirectory(directory);
        return new(directory, () => legacy);
    }

    private static T Field<T>(MainForm form, string name) => (T)(typeof(MainForm)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form)
        ?? throw new InvalidOperationException($"Missing field {name}."));

    private static void Invoke(MainForm form, string name) => typeof(MainForm)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);

    private static void ExpectFailure<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { assertions++; return; }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
