using Microsoft.Win32;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Vario;

internal sealed class RegistryService : IDisposable
{
    private const string VoicesKey = @"SOFTWARE\Microsoft\Speech\Voices\Tokens";
    private const string PhoneConvertersKey = @"SOFTWARE\Microsoft\Speech\PhoneConverters\Tokens";
    private const string SettingsKey = @"SOFTWARE\eSpeak\Vario";
    private const string EngineClsid = "{BE985C8D-BE32-4A22-AA93-55C16A6D1D91}";
    private const string PhoneConverterClsid = "{9185F743-1143-4C28-86B5-BFF14F20E5C8}";
    private static readonly Regex TokenNamePattern = new(@"^eSpeak(?:_\d+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly RegistryKey machine;

    internal RegistryService()
    {
        RegistryView view = Environment.Is64BitProcess ? RegistryView.Registry64 : RegistryView.Registry32;
        machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
    }

    internal string ArchitectureName => Environment.Is64BitProcess ? "64-bit" : "32-bit";

    internal IReadOnlyList<string> ReadInstalledVoices()
    {
        using RegistryKey? tokens = machine.OpenSubKey(VoicesKey, writable: false);
        if (tokens is null)
            return Array.Empty<string>();

        return GetManagedTokenNames(tokens)
            .Select(name =>
            {
                using RegistryKey? token = tokens.OpenSubKey(name, writable: false);
                return token?.GetValue("VoiceName") as string;
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal (IReadOnlyList<string> Voices, IReadOnlyList<string> Variants) DiscoverAvailableVoices()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "espeak-data", "voices");
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(root);

        List<string> voices = new();
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file);
            string firstPart = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            if (firstPart.Equals("!v", StringComparison.OrdinalIgnoreCase) ||
                firstPart.Equals("test", StringComparison.OrdinalIgnoreCase))
                continue;
            voices.Add(Path.GetFileName(file));
        }

        string variantsRoot = Path.Combine(root, "!v");
        IEnumerable<string> variants = Directory.Exists(variantsRoot)
            ? Directory.EnumerateFiles(variantsRoot).Select(file => Path.GetFileName(file)!)
            : Array.Empty<string>();

        return (
            voices.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            variants.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).ToArray());
    }

    internal bool ReadSonicBoost()
    {
        using RegistryKey? settings = machine.OpenSubKey(SettingsKey, writable: false);
        return settings?.GetValue("SonicBoost") is int value && value != 0;
    }

    internal void Apply(IReadOnlyList<string> requestedVoices, bool sonicBoost)
    {
        string[] voices = requestedVoices
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (voices.Length == 0)
            throw new InvalidOperationException("At least one SAPI voice is required.");
        if (voices.Length > 200)
            throw new InvalidOperationException("No more than 200 SAPI voices can be registered.");

        using RegistryKey tokens = machine.CreateSubKey(VoicesKey, writable: true)
            ?? throw new InvalidOperationException("The SAPI voice registry could not be opened.");
        string[] previousNames = GetManagedTokenNames(tokens).ToArray();
        HashSet<string> desiredNames = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < voices.Length; index++)
        {
            string keyName = index == 0 ? "eSpeak" : $"eSpeak_{index}";
            desiredNames.Add(keyName);
            WriteVoiceToken(tokens, keyName, voices[index]);
            EnsurePhoneConverter(LanguageFromVoice(voices[index]));
        }

        foreach (string obsolete in previousNames.Where(name => !desiredNames.Contains(name)))
            tokens.DeleteSubKeyTree(obsolete, throwOnMissingSubKey: false);

        using RegistryKey settings = machine.CreateSubKey(SettingsKey, writable: true)
            ?? throw new InvalidOperationException("The Vario settings registry could not be opened.");
        settings.SetValue("SonicBoost", sonicBoost ? 1 : 0, RegistryValueKind.DWord);
    }

    private static IEnumerable<string> GetManagedTokenNames(RegistryKey tokens) =>
        tokens.GetSubKeyNames()
            .Where(name => TokenNamePattern.IsMatch(name))
            .OrderBy(TokenIndex);

    private static int TokenIndex(string name)
    {
        int separator = name.IndexOf('_');
        return separator < 0 || !int.TryParse(name[(separator + 1)..], out int index) ? 0 : index;
    }

    private static void WriteVoiceToken(RegistryKey tokens, string keyName, string voice)
    {
        using RegistryKey token = tokens.CreateSubKey(keyName, writable: true)
            ?? throw new InvalidOperationException($"Cannot create SAPI token {keyName}.");
        string displayVoice = voice.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? "default" : voice.ToUpperInvariant();
        token.SetValue(string.Empty, "eSpeak-" + displayVoice, RegistryValueKind.String);
        token.SetValue("CLSID", EngineClsid, RegistryValueKind.String);
        token.SetValue("Path", AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), RegistryValueKind.String);
        token.SetValue("VoiceName", voice, RegistryValueKind.String);

        using RegistryKey attributes = token.CreateSubKey("Attributes", writable: true)
            ?? throw new InvalidOperationException($"Cannot create attributes for {keyName}.");
        attributes.SetValue("Name", "eSpeak-" + voice, RegistryValueKind.String);
        attributes.SetValue("Gender", VariantIsFemale(voice) ? "Female" : "Male", RegistryValueKind.String);
        attributes.SetValue("Age", "Adult", RegistryValueKind.String);
        attributes.SetValue("Language", LanguageFromVoice(voice), RegistryValueKind.String);
        attributes.SetValue("Vendor", "https://github.com/Pates2004", RegistryValueKind.String);
    }

    private static bool VariantIsFemale(string voice)
    {
        int plus = voice.LastIndexOf('+');
        return plus >= 0 && plus + 1 < voice.Length && char.ToLowerInvariant(voice[plus + 1]) == 'f';
    }

    private void EnsurePhoneConverter(string languageCode)
    {
        using RegistryKey converter = machine.CreateSubKey(PhoneConvertersKey + @"\eSpeak", writable: true)
            ?? throw new InvalidOperationException("The eSpeak phone converter could not be opened.");
        converter.SetValue("CLSID", PhoneConverterClsid, RegistryValueKind.String);
        converter.SetValue("PhoneMap", "- 0001", RegistryValueKind.String);
        using RegistryKey attributes = converter.CreateSubKey("Attributes", writable: true)
            ?? throw new InvalidOperationException("The eSpeak phone converter attributes could not be opened.");
        string current = attributes.GetValue("Language") as string ?? string.Empty;
        HashSet<string> languages = current.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (languages.Add(languageCode))
            attributes.SetValue("Language", string.Join(';', languages), RegistryValueKind.String);
    }

    private static string LanguageFromVoice(string voice)
    {
        string baseVoice = voice.Split('+')[0].ToLowerInvariant();
        if (baseVoice == "pt-pt") return "816";
        if (baseVoice.StartsWith("mb-", StringComparison.Ordinal))
        {
            if (baseVoice.EndsWith("-en", StringComparison.Ordinal)) return "409";
            string mbCode = baseVoice.Length >= 5 ? baseVoice[..5] : baseVoice;
            if (MbrolaLanguages.TryGetValue(mbCode, out string? mbLanguage)) return mbLanguage;
        }

        string language = baseVoice.Split('-')[0];
        return LanguageCodes.TryGetValue(language, out string? code) ? code : "409";
    }

    private static readonly IReadOnlyDictionary<string, string> LanguageCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["af"]="436", ["az"]="42C", ["bg"]="402", ["bs"]="41A", ["ca"]="403", ["cs"]="405",
        ["cy"]="452", ["da"]="406", ["de"]="407", ["el"]="408", ["en"]="409", ["eo"]="409",
        ["es"]="40A", ["eu"]="42D", ["fi"]="40B", ["fr"]="40C", ["hi"]="439", ["hr"]="41A",
        ["hu"]="40E", ["hy"]="42B", ["id"]="421", ["is"]="40F", ["it"]="410", ["kn"]="44B",
        ["ko"]="412", ["ku"]="409", ["la"]="409", ["lv"]="426", ["mk"]="42F", ["mn"]="450",
        ["ne"]="461", ["nl"]="413", ["no"]="414", ["pl"]="415", ["pt"]="416", ["ro"]="418",
        ["ru"]="419", ["rw"]="487", ["sk"]="41B", ["sq"]="41C", ["sr"]="81A", ["sv"]="41D",
        ["sw"]="441", ["ta"]="449", ["tr"]="41F", ["vi"]="42A", ["zh"]="804"
    };

    private static readonly IReadOnlyDictionary<string, string> MbrolaLanguages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["mb-af"]="436", ["mb-br"]="416", ["mb-ca"]="C0C", ["mb-cr"]="41A", ["mb-cz"]="405",
        ["mb-de"]="407", ["mb-en"]="809", ["mb-es"]="40A", ["mb-fr"]="40C", ["mb-gr"]="408",
        ["mb-hu"]="40E", ["mb-ic"]="40F", ["mb-in"]="439", ["mb-it"]="410", ["mb-mx"]="80A",
        ["mb-nl"]="413", ["mb-pl"]="415", ["mb-pt"]="816", ["mb-ro"]="418", ["mb-sw"]="41D",
        ["mb-us"]="409", ["mb-vz"]="200A"
    };

    public void Dispose() => machine.Dispose();
}
