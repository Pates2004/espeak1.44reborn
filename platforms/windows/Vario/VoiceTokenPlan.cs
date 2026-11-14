namespace Vario;

internal sealed record RegisteredVoice(string TokenName, string VoiceName);
internal sealed record PlannedVoice(string TokenName, VoiceConfiguration Configuration);

internal static class VoiceTokenPlan
{
    internal static IReadOnlyList<PlannedVoice> Create(IReadOnlyList<RegisteredVoice> existing,
        IReadOnlyList<VoiceConfiguration> requested)
    {
        Dictionary<string, string> retained = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> reserved = new(StringComparer.OrdinalIgnoreCase);
        foreach (RegisteredVoice voice in existing)
        {
            reserved.Add(voice.TokenName);
            if (!string.IsNullOrWhiteSpace(voice.VoiceName))
                retained.TryAdd(voice.VoiceName, voice.TokenName);
        }
        List<PlannedVoice> result = new();
        int nextIndex = 0;
        foreach (VoiceConfiguration voice in requested)
        {
            if (!retained.TryGetValue(voice.Name, out string? token))
            {
                do
                {
                    token = nextIndex == 0 ? "eSpeak" : $"eSpeak_{nextIndex}";
                    nextIndex = checked(nextIndex + 1);
                } while (!reserved.Add(token));
            }
            result.Add(new(token, voice));
        }
        return result;
    }
}
