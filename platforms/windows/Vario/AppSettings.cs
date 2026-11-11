using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
    private readonly string changeLockName;
    internal string FilePath { get; }

    internal SettingsStore(string installationDirectory, Func<LegacySpeedSettings> readLegacy, string? changeLockName = null)
    {
        FilePath = Path.Combine(Path.GetFullPath(installationDirectory), "vario.ini");
        this.readLegacy = readLegacy;
        this.changeLockName = changeLockName ?? (Environment.Is64BitProcess
            ? @"Local\Pates2004.eSpeak.Vario.Registry64" : @"Local\Pates2004.eSpeak.Vario.Registry32");
    }

    internal IDisposable AcquireChangeLock() => InstallationChangeLock.Acquire(changeLockName);

    internal string GetRevision() => File.Exists(FilePath)
        ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(FilePath))) : "missing";

    internal LoadedSettings Load()
    {
        string boost = Read("SonicBoost");
        string mode = Read("SonicMode");
        bool needsInitialization = boost.Length == 0 || mode.Length == 0;
        LegacySpeedSettings legacy = needsInitialization
            ? readLegacy() : new(false, false, SonicMode.Nvda);
        bool defaultBoost = legacy.HasSettings && legacy.SonicBoost;
        SonicMode defaultMode = legacy.HasSettings ? legacy.SonicMode : SonicMode.Smooth;
        VarioSettings settings = new(
            boost switch { "0" => false, "1" => true, _ => defaultBoost },
            mode switch { "0" => SonicMode.Legacy, "1" => SonicMode.Nvda, "2" => SonicMode.Smooth, _ => defaultMode },
            Read("Theme") == "dark" ? AppTheme.Dark : AppTheme.Light,
            Read("Language") switch { "en" => AppLanguage.English, "pl" => AppLanguage.Polish, _ => AppLanguage.System },
            Read("ShowHints") != "0");
        return new(settings, needsInitialization, needsInitialization && legacy.HasSettings);
    }

    internal string Save(VarioSettings settings)
    {
        using IDisposable installationLock = AcquireChangeLock();
        using PreparedSave prepared = PrepareSave(settings);
        prepared.Commit();
        return prepared.Revision;
    }

    internal PreparedSave PrepareSave(VarioSettings settings)
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
            Dictionary<string, string> values = new()
            {
                ["SonicBoost"] = settings.SonicBoost ? "1" : "0",
                ["SonicMode"] = ((int)settings.SonicMode).ToString(CultureInfo.InvariantCulture),
                ["Theme"] = settings.Theme == AppTheme.Dark ? "dark" : "light",
                ["Language"] = settings.Language switch
                {
                    AppLanguage.English => "en",
                    AppLanguage.Polish => "pl",
                    _ => "system"
                },
                ["ShowHints"] = settings.ShowHints ? "1" : "0"
            };
            foreach ((string key, string value) in values)
                Write(temporaryPath, key, value);
            // A zero return is also documented for successful cache flushing.
            WritePrivateProfileStringW(null, null, null, temporaryPath);
            foreach ((string key, string value) in values)
                if (Read(temporaryPath, key) != value)
                    throw new IOException(UiText.SettingsWriteFailed);
            using (FileStream stream = new(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                stream.Flush(flushToDisk: true);
            return new PreparedSave(temporaryPath, FilePath,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temporaryPath))));
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            throw;
        }
    }

    internal sealed class PreparedSave : IDisposable
    {
        private string? temporaryPath;
        private readonly string targetPath;
        internal string Revision { get; }

        internal PreparedSave(string temporaryPath, string targetPath, string revision)
        {
            this.temporaryPath = temporaryPath;
            this.targetPath = targetPath;
            Revision = revision;
        }

        internal void Commit()
        {
            string source = temporaryPath ?? throw new ObjectDisposedException(nameof(PreparedSave));
            // Source and target are siblings, so replacement stays on one volume.
            File.Move(source, targetPath, overwrite: true);
            temporaryPath = null;
            // Only the unique staging file was written with the profile API;
            // the target has no pending writes which could undo the rename.
            WritePrivateProfileStringW(null, null, null, targetPath);
        }

        public void Dispose()
        {
            string? source = Interlocked.Exchange(ref temporaryPath, null);
            if (source is not null && File.Exists(source))
                File.Delete(source);
        }
    }

    private string Read(string key) => Read(FilePath, key);

    private static string Read(string path, string key)
    {
        StringBuilder value = new(1024);
        GetPrivateProfileStringW(Section, key, string.Empty, value, (uint)value.Capacity, path);
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
