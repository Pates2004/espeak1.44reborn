using System.Reflection;
using System.Text;
using Vario;

internal static class FailureAndMetadataTests
{
    internal static void Run(string repository, string fixtures, Action<bool, string> check)
    {
        TestLoadFailures(fixtures, check);
        TestChanges(fixtures, check);
        TestChangeLock(check);
        TestGenders(repository, fixtures, check);
    }

    private static void TestLoadFailures(string fixtures, Action<bool, string> check)
    {
        List<string> errors = new();
        using FakeRegistry registry = new() { FailDiscovery = true };
        SettingsStore store = CreateStore(fixtures, "load-failures");
        using MainForm form = new(registry, store, errors.Add);
        check(!Field<Button>(form, "applyButton").Enabled, "A failed first load disables Apply.");
        Invoke(form, "ApplyChanges");
        check(registry.ApplyCount == 0 && registry.SnapshotCount == 0 && errors.Count == 2,
            "The failed-load hard guard cannot erase installed voices, even when invoked directly.");
        registry.FailDiscovery = false;
        Invoke(form, "LoadData");
        check(Field<Button>(form, "applyButton").Enabled, "A successful reload enables Apply.");
        Dictionary<string, int> pending = Field<Dictionary<string, int>>(form, "pendingInflections");
        pending["pl"] = 70;
        registry.FailRead = true;
        Invoke(form, "LoadData");
        check(!Field<Button>(form, "applyButton").Enabled && pending["pl"] == 70,
            "A failed reload retains pending values and disables Apply.");
        Invoke(form, "ApplyChanges");
        check(registry.ApplyCount == 0, "A failed later reload also blocks registration.");
        registry.FailRead = false;
        Invoke(form, "LoadData");
        check(Field<Button>(form, "applyButton").Enabled && pending["pl"] == 50,
            "Recovery reload restores the real baseline.");
    }

    private static void TestChanges(string fixtures, Action<bool, string> check)
    {
        VoiceConfiguration[] baseline = { new("pl", 50) };
        VoiceConfiguration[] requested = { new("pl+f3", 75), new("en", 40) };
        VarioSettings updated = new(true, SonicMode.Nvda, AppTheme.Dark, AppLanguage.Polish, false);

        SettingsStore missing = new(Path.Combine(fixtures, "absent-transaction-parent"),
            () => new(false, false, SonicMode.Smooth), UniqueLock());
        using (FakeRegistry registry = new())
        {
            Expect<DirectoryNotFoundException>(() => InstallationChanges.Apply(registry, missing, baseline,
                missing.GetRevision(), requested, updated), check);
            check(registry.ApplyCount == 0 && registry.SnapshotCount == 0,
                "A staging failure occurs before any registry mutation or snapshot.");
        }

        SettingsStore prepareFail = CreateStore(fixtures, "prepare-failure");
        byte[] old = File.ReadAllBytes(prepareFail.FilePath);
        using (FakeRegistry registry = new())
        {
            Expect<ArgumentOutOfRangeException>(() => InstallationChanges.Apply(registry, prepareFail, baseline,
                prepareFail.GetRevision(), requested, updated with { SonicMode = (SonicMode)99 }), check);
            check(registry.ApplyCount == 0 && old.SequenceEqual(File.ReadAllBytes(prepareFail.FilePath)),
                "Invalid staged settings preserve the existing INI and all registrations.");
            CheckNoTemporaryFiles(prepareFail, check);
        }

        SettingsStore applyFail = CreateStore(fixtures, "apply-failure");
        old = File.ReadAllBytes(applyFail.FilePath);
        using (FakeRegistry registry = new() { FailApply = true })
        {
            Expect<IOException>(() => InstallationChanges.Apply(registry, applyFail, baseline,
                applyFail.GetRevision(), requested, updated), check);
            check(registry.RestoreCount == 1 && registry.Voices.SequenceEqual(baseline),
                "A partial registry failure restores the original voice names and modulation.");
            check(old.SequenceEqual(File.ReadAllBytes(applyFail.FilePath)), "A registry failure leaves old INI bytes intact.");
            CheckNoTemporaryFiles(applyFail, check);
        }

        SettingsStore captureFail = CreateStore(fixtures, "capture-failure");
        old = File.ReadAllBytes(captureFail.FilePath);
        using (FakeRegistry registry = new() { FailSnapshot = true })
        {
            Expect<IOException>(() => InstallationChanges.Apply(registry, captureFail, baseline,
                captureFail.GetRevision(), requested, updated), check);
            check(registry.ApplyCount == 0 && registry.RestoreCount == 0 &&
                old.SequenceEqual(File.ReadAllBytes(captureFail.FilePath)),
                "A snapshot access failure leaves both stores unchanged.");
            CheckNoTemporaryFiles(captureFail, check);
        }

        SettingsStore commitFail = CreateStore(fixtures, "commit-failure");
        old = File.ReadAllBytes(commitFail.FilePath);
        FileStream? heldTarget = null;
        using (FakeRegistry registry = new())
        {
            registry.AfterApply = () => heldTarget = new FileStream(commitFail.FilePath, FileMode.Open,
                FileAccess.Read, FileShare.Read);
            try
            {
                Expect<UnauthorizedAccessException>(() => InstallationChanges.Apply(registry, commitFail, baseline,
                    commitFail.GetRevision(), requested, updated), check);
                check(registry.ApplyCount == 1 && registry.RestoreCount == 1 && registry.Voices.SequenceEqual(baseline),
                    "A real Windows file replacement failure rolls the changed registry back.");
                check(old.SequenceEqual(File.ReadAllBytes(commitFail.FilePath)),
                    "The locked existing INI is preserved byte for byte.");
                CheckNoTemporaryFiles(commitFail, check);
            }
            finally { heldTarget?.Dispose(); }
        }

        SettingsStore rollbackFail = CreateStore(fixtures, "rollback-failure");
        using (FakeRegistry registry = new() { FailApply = true, FailRestore = true })
        {
            AggregateException error = Expect<AggregateException>(() => InstallationChanges.Apply(registry,
                rollbackFail, baseline, rollbackFail.GetRevision(), requested, updated), check);
            check(error.Message.Contains(UiText.RollbackFailed) && error.InnerExceptions.Count == 2,
                "A rollback failure is explicitly reported with both original and recovery errors.");
            CheckNoTemporaryFiles(rollbackFail, check);
        }

        SettingsStore conflict = CreateStore(fixtures, "conflicts");
        string initialRevision = conflict.GetRevision();
        old = File.ReadAllBytes(conflict.FilePath);
        using (FakeRegistry registry = new())
        {
            registry.Voices = new[] { new VoiceConfiguration("pl", 60) };
            InvalidOperationException error = Expect<InvalidOperationException>(() => InstallationChanges.Apply(registry,
                conflict, baseline, initialRevision, requested, updated), check);
            check(error.Message == UiText.InstallationChanged && registry.ApplyCount == 0 && registry.SnapshotCount == 0,
                "Concurrent voice edits are rejected before mutation.");
            check(old.SequenceEqual(File.ReadAllBytes(conflict.FilePath)), "A voice conflict does not overwrite the INI.");
            registry.Voices = baseline;
            conflict.Save(updated);
            error = Expect<InvalidOperationException>(() => InstallationChanges.Apply(registry, conflict,
                baseline, initialRevision, requested, updated), check);
            check(error.Message == UiText.InstallationChanged && registry.ApplyCount == 0,
                "Concurrent INI edits are rejected before registry mutation.");
        }

        SettingsStore success = CreateStore(fixtures, "success");
        File.AppendAllText(success.FilePath, "\r\n[Other]\r\nUnknown=żółć\r\n", Encoding.Unicode);
        using (FakeRegistry registry = new())
        {
            string revision = InstallationChanges.Apply(registry, success, baseline, success.GetRevision(), requested, updated);
            check(registry.Voices.SequenceEqual(requested) && registry.RestoreCount == 0,
                "A successful transaction keeps all requested voices and modulation.");
            check(success.Load().Value == updated && revision == success.GetRevision(),
                "The native INI reader immediately sees committed speed and interface settings.");
            check(File.ReadAllText(success.FilePath).Contains("Unknown=żółć"),
                "Saving an existing INI preserves unrelated Unicode settings.");
            CheckNoTemporaryFiles(success, check);
        }

        // A loaded form must require another reload after recovery itself fails.
        List<string> errors = new();
        using FakeRegistry brokenRegistry = new();
        using MainForm broken = new(brokenRegistry, CreateStore(fixtures, "rollback-form"), errors.Add);
        brokenRegistry.FailApply = brokenRegistry.FailRestore = true;
        Invoke(broken, "ApplyChanges");
        check(!Field<Button>(broken, "applyButton").Enabled && errors.Any(text => text.Contains(UiText.RollbackFailed)),
            "The UI blocks further Apply calls after an incomplete rollback.");
    }

    private static void TestChangeLock(Action<bool, string> check)
    {
        string name = UniqueLock();
        using ManualResetEventSlim attempted = new();
        Task waiter;
        using (InstallationChangeLock.Acquire(name))
        {
            waiter = Task.Run(() =>
            {
                attempted.Set();
                using IDisposable held = InstallationChangeLock.Acquire(name);
            });
            check(attempted.Wait(TimeSpan.FromSeconds(1)), "The second lock owner began its attempt.");
            check(!waiter.Wait(TimeSpan.FromMilliseconds(100)), "Independent threads cannot edit the same installation simultaneously.");
        }
        check(waiter.Wait(TimeSpan.FromSeconds(3)), "The waiting operation acquires the released installation lock.");
        waiter.GetAwaiter().GetResult();
    }

    private static void TestGenders(string repository, string fixtures, Action<bool, string> check)
    {
        string root = Path.Combine(repository, "espeak-data", "voices");
        VoiceGenderCatalog catalog = new(root);
        string[] femaleNames = { "Alicia", "Andrea", "anika", "anikaRobot", "Annie", "aunty", "belinda",
            "grandma", "linda", "steph", "steph2", "steph3", "whisperf" };
        foreach (string name in femaleNames)
            check(catalog.Resolve("pl+" + name) == "Female", $"The {name} variant uses its declared female gender.");
        foreach (string file in Directory.GetFiles(Path.Combine(root, "!v")))
        {
            string? declared = File.ReadLines(file).Select(line => line.Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries)).Where(parts => parts.Length >= 2 && parts[0] == "gender")
                .Select(parts => parts[1]).LastOrDefault();
            if (declared is "male" or "female")
                check(catalog.Resolve("pl+" + Path.GetFileName(file)) == (declared == "female" ? "Female" : "Male"),
                    $"Gender metadata matches the declaration of {Path.GetFileName(file)}.");
        }
        string definitions = Path.Combine(fixtures, "gender-definitions");
        Directory.CreateDirectory(Path.Combine(definitions, "!v"));
        File.WriteAllText(Path.Combine(definitions, "base"), "name Example\n// gender male\nGender\tFeMaLe// base declaration\n");
        File.WriteAllText(Path.Combine(definitions, "!v", "neutral"), "name Neutral\npitch 80 120\n");
        File.WriteAllText(Path.Combine(definitions, "!v", "male"), "name Male\ngender male\n");
        catalog = new(definitions);
        check(catalog.Resolve("BASE+NEUTRAL") == "Female", "Undeclared variant gender inherits from its base, case insensitively.");
        check(catalog.Resolve("base+male") == "Male", "A variant declaration overrides the base voice gender.");
        check(catalog.Resolve("unknown") == "Male", "The historical default remains male when no gender is declared.");
    }

    private static SettingsStore CreateStore(string fixtures, string name)
    {
        string directory = Path.Combine(fixtures, name);
        Directory.CreateDirectory(directory);
        SettingsStore store = new(directory, () => new(false, false, SonicMode.Smooth), UniqueLock());
        store.Save(store.Load().Value);
        return store;
    }

    private static string UniqueLock() => @"Local\Vario.Tests." + Guid.NewGuid().ToString("N");

    private static void CheckNoTemporaryFiles(SettingsStore store, Action<bool, string> check) =>
        check(Directory.GetFiles(Path.GetDirectoryName(store.FilePath)!, "*.tmp").Length == 0,
            "No abandoned transaction staging file remains.");

    private static TException Expect<TException>(Action action, Action<bool, string> check) where TException : Exception
    {
        try { action(); }
        catch (TException exception) { check(true, $"Expected {typeof(TException).Name}."); return exception; }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private static T Field<T>(MainForm form, string name) => (T)typeof(MainForm)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
    private static void Invoke(MainForm form, string name) => typeof(MainForm)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);

    private sealed class FakeRegistry : IVoiceRegistry
    {
        internal IReadOnlyList<VoiceConfiguration> Voices = new[] { new VoiceConfiguration("pl", 50) };
        internal bool FailDiscovery, FailRead, FailApply, FailRestore, FailSnapshot;
        internal int ApplyCount, SnapshotCount, RestoreCount;
        internal Action? AfterApply;
        public string ArchitectureName => "fixture";
        public IReadOnlyList<VoiceConfiguration> ReadInstalledVoices() => FailRead
            ? throw new IOException("Read fixture failure.") : Voices.ToArray();
        public (IReadOnlyList<string> Voices, IReadOnlyList<string> Variants) DiscoverAvailableVoices() => FailDiscovery
            ? throw new IOException("Discovery fixture failure.") : (new[] { "en", "pl" }, new[] { "f3", "m2" });
        public LegacySpeedSettings ReadLegacySpeedSettings() => new(false, false, SonicMode.Smooth);
        public IVoiceRegistrySnapshot CaptureSnapshot()
        {
            SnapshotCount++;
            if (FailSnapshot) throw new IOException("Snapshot fixture failure.");
            return new Snapshot(this, Voices.ToArray());
        }
        public void Apply(IReadOnlyList<VoiceConfiguration> requestedVoices)
        {
            ApplyCount++;
            Voices = requestedVoices.ToArray();
            if (FailApply) throw new IOException("Partial registry fixture failure.");
            AfterApply?.Invoke();
        }
        public void Dispose() { }
        private sealed record Snapshot(FakeRegistry Owner, VoiceConfiguration[] Baseline) : IVoiceRegistrySnapshot
        {
            public void Restore()
            {
                Owner.RestoreCount++;
                if (Owner.FailRestore) throw new IOException("Restore fixture failure.");
                Owner.Voices = Baseline;
            }
        }
    }
}
