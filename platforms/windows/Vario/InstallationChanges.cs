namespace Vario;

internal interface IVoiceRegistrySnapshot
{
    void Restore();
}

// This coordinates recoverable errors in one process, not power-loss recovery.
internal static class InstallationChanges
{
    internal static string Apply(IVoiceRegistry registry, SettingsStore settingsStore,
        IReadOnlyList<VoiceConfiguration> baselineVoices, string baselineSettingsRevision,
        IReadOnlyList<VoiceConfiguration> requestedVoices, VarioSettings settings)
    {
        using IDisposable installationLock = settingsStore.AcquireChangeLock();
        if (!SameVoices(registry.ReadInstalledVoices(), baselineVoices) ||
            settingsStore.GetRevision() != baselineSettingsRevision)
            throw new InvalidOperationException(UiText.InstallationChanged);

        // Prepare the complete INI first: ordinary filesystem failures must not
        // begin changing SAPI registration at all.
        using SettingsStore.PreparedSave prepared = settingsStore.PrepareSave(settings);
        IVoiceRegistrySnapshot snapshot = registry.CaptureSnapshot();
        try
        {
            registry.Apply(requestedVoices);
            prepared.Commit();
            return prepared.Revision;
        }
        catch (Exception saveError)
        {
            try { snapshot.Restore(); }
            catch (Exception rollbackError)
            {
                throw new AggregateException(UiText.RollbackFailed, saveError, rollbackError);
            }
            throw;
        }
    }

    private static bool SameVoices(IReadOnlyList<VoiceConfiguration> left, IReadOnlyList<VoiceConfiguration> right)
    {
        if (left.Count != right.Count)
            return false;
        Dictionary<string, int> expected = right.ToDictionary(value => value.Name, value => value.Inflection,
            StringComparer.OrdinalIgnoreCase);
        return left.All(value => expected.TryGetValue(value.Name, out int inflection) && inflection == value.Inflection);
    }
}

internal sealed class InstallationChangeLock : IDisposable
{
    private Mutex? mutex;

    private InstallationChangeLock(Mutex mutex) => this.mutex = mutex;

    internal static IDisposable Acquire(string name)
    {
        Mutex mutex = new(false, name);
        bool acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(2)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired)
                throw new InvalidOperationException(UiText.InstallationBusy);
            return new InstallationChangeLock(mutex);
        }
        catch
        {
            if (acquired)
                mutex.ReleaseMutex();
            mutex.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Mutex? current = Interlocked.Exchange(ref mutex, null);
        if (current is null)
            return;
        try { current.ReleaseMutex(); }
        finally { current.Dispose(); }
    }
}
