namespace Vario;

internal sealed class VoiceGenderCatalog
{
    private readonly Dictionary<string, string> baseGenders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> variantGenders = new(StringComparer.OrdinalIgnoreCase);

    internal VoiceGenderCatalog(string voicesDirectory)
    {
        foreach (string file in Directory.EnumerateFiles(voicesDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(voicesDirectory, file);
            string firstPart = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            if (firstPart.Equals("test", StringComparison.OrdinalIgnoreCase))
                continue;
            string? gender = ReadGender(file);
            if (gender is null)
                continue;
            Dictionary<string, string> target = firstPart.Equals("!v", StringComparison.OrdinalIgnoreCase)
                ? variantGenders : baseGenders;
            target[Path.GetFileName(file)] = gender;
        }
    }

    internal string Resolve(string voice)
    {
        string[] parts = voice.Split('+', 2);
        if (parts.Length == 2 && variantGenders.TryGetValue(parts[1], out string? variantGender))
            return variantGender;
        return baseGenders.TryGetValue(parts[0], out string? baseGender) ? baseGender : "Male";
    }

    private static string? ReadGender(string file)
    {
        string? result = null;
        foreach (string line in File.ReadLines(file))
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            string content = comment < 0 ? line : line[..comment];
            string[] fields = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 2 || !fields[0].Equals("gender", StringComparison.OrdinalIgnoreCase))
                continue;
            if (fields[1].Equals("female", StringComparison.OrdinalIgnoreCase))
                result = "Female";
            else if (fields[1].Equals("male", StringComparison.OrdinalIgnoreCase))
                result = "Male";
        }
        return result;
    }
}
