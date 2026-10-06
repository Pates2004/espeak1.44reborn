using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Vario;

internal enum AppTheme { Light, Dark }
internal enum AppLanguage { System, English, Polish }
internal sealed record VarioSettings(bool SonicBoost, SonicMode SonicMode, AppTheme Theme,
    AppLanguage Language, bool ShowHints);
internal sealed record LegacySpeedSettings(bool HasSettings, bool SonicBoost, SonicMode SonicMode);
internal sealed record LoadedSettings(VarioSettings Value, bool NeedsInitialization, bool MigratedLegacy);

internal sealed class SettingsStore
{
    private const string Section = "Settings";
    private readonly Func<LegacySpeedSettings> readLegacy;
    internal string FilePath { get; }

    internal SettingsStore(string installationDirectory, Func<LegacySpeedSettings> readLegacy)
    {
        FilePath = Path.Combine(Path.GetFullPath(installationDirectory), "vario.ini");
        this.readLegacy = readLegacy;
    }

    internal LoadedSettings Load()
    {
        string boost = Read("SonicBoost");
        string mode = Read("SonicMode");
        bool needsInitialization = boost.Length == 0 || mode.Length == 0;
        LegacySpeedSettings legacy = needsInitialization
            ? readLegacy() : new(false, false, SonicMode.Nvda);
        bool defaultBoost = legacy.HasSettings ? legacy.SonicBoost : true;
        SonicMode defaultMode = legacy.HasSettings ? legacy.SonicMode : SonicMode.Smooth;
        VarioSettings settings = new(
            boost switch { "0" => false, "1" => true, _ => defaultBoost },
            mode switch { "0" => SonicMode.Legacy, "1" => SonicMode.Nvda, "2" => SonicMode.Smooth, _ => defaultMode },
            Read("Theme") == "dark" ? AppTheme.Dark : AppTheme.Light,
            Read("Language") switch { "en" => AppLanguage.English, "pl" => AppLanguage.Polish, _ => AppLanguage.System },
            Read("ShowHints") != "0");
        return new(settings, needsInitialization, needsInitialization && legacy.HasSettings);
    }

    internal void Save(VarioSettings settings)
    {
        if (!Enum.IsDefined(settings.SonicMode))
            throw new ArgumentOutOfRangeException(nameof(settings), UiText.InvalidSonicMode);
        if (!Enum.IsDefined(settings.Theme) || !Enum.IsDefined(settings.Language))
            throw new ArgumentOutOfRangeException(nameof(settings), UiText.InvalidPreferences);

        string temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string existing = File.Exists(FilePath) ? File.ReadAllText(FilePath) : string.Empty;
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(stream, Encoding.Unicode))
                writer.Write(existing);
            Write(temporaryPath, "SonicBoost", settings.SonicBoost ? "1" : "0");
            Write(temporaryPath, "SonicMode", ((int)settings.SonicMode).ToString(CultureInfo.InvariantCulture));
            Write(temporaryPath, "Theme", settings.Theme == AppTheme.Dark ? "dark" : "light");
            Write(temporaryPath, "Language", settings.Language switch
            {
                AppLanguage.English => "en",
                AppLanguage.Polish => "pl",
                _ => "system"
            });
            Write(temporaryPath, "ShowHints", settings.ShowHints ? "1" : "0");
            WritePrivateProfileStringW(null, null, null, temporaryPath);
            File.Move(temporaryPath, FilePath, overwrite: true);
            WritePrivateProfileStringW(null, null, null, FilePath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private string Read(string key)
    {
        StringBuilder value = new(1024);
        GetPrivateProfileStringW(Section, key, string.Empty, value, (uint)value.Capacity, FilePath);
        return value.ToString();
    }

    private static void Write(string path, string key, string value)
    {
        if (!WritePrivateProfileStringW(Section, key, value, path))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetPrivateProfileStringW(string section, string key, string defaultValue,
        StringBuilder value, uint size, string path);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WritePrivateProfileStringW(string? section, string? key, string? value, string path);
}
