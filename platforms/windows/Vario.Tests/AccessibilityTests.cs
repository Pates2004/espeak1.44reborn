using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;
using Vario;

internal static class AccessibilityTests
{
    private const uint StateChange = 0x800A;
    private const uint FocusChange = 0x8005;
    private const int ClientObject = -4;
    private const int TreeItemToAccessibleId = 0x112B;
    private const uint CheckedState = 0x10;
    private const uint MixedState = 0x20;
    private sealed record NativeEvent(uint Event, long Window, int Object, int Child, string Name, uint State);
    private sealed record UiaEvent(string Name, string Value);
    private sealed record Observation(string Action, string Element, string Toggle, uint State,
        uint ExpectedState, int MatchingNativeEvents, int MatchingUiaEvents);

    internal static void Run(string fixtures, bool recordOnly = false)
    {
        foreach (bool hints in new[] { false, true })
            RunForm(Path.Combine(fixtures, hints ? "accessibility-hints-on" : "accessibility-hints-off"), hints, recordOnly);
    }

    private static void RunForm(string path, bool hints, bool recordOnly)
    {
        Directory.CreateDirectory(path);
        SettingsStore store = new(path, () => new(false, false, SonicMode.Smooth));
        store.Save(store.Load().Value with { Language = AppLanguage.English, ShowHints = hints });
        using FixtureRegistry registry = new();
        using MainForm form = new(registry, store);
        TriStateTreeView available = Field<TriStateTreeView>(form, "availableTree");
        TriStateTreeView installed = Field<TriStateTreeView>(form, "installedTree");
        CheckBox checkbox = Field<CheckBox>(form, "sonicCheck");
        checkbox.Checked = false;
        TreeNode availableParent = available.Nodes.Cast<TreeNode>().First(node => node.Nodes.Count >= 2);
        TreeNode installedParent = installed.Nodes.Cast<TreeNode>().First(node => node.Nodes.Count >= 2);
        form.Show();
        form.Activate();
        availableParent.Expand();
        installedParent.Expand();
        Application.DoEvents();
        nint checkboxHandle = checkbox.Handle;
        ConcurrentQueue<NativeEvent> nativeEvents = new();
        ConcurrentQueue<UiaEvent> uiaEvents = new();
        List<Observation> observations = new();
        List<string> failures = new();
        WinEventDelegate callback = (_, eventId, window, objectId, childId, _, _) =>
        {
            if (objectId != ClientObject) return;
            string name = "";
            uint state = 0;
            if (AccessibleObjectFromEvent(window, objectId, childId, out Accessibility.IAccessible accessible, out object child) == 0)
            {
                try { name = accessible.get_accName(child) ?? ""; state = Convert.ToUInt32(accessible.get_accState(child)); }
                catch (COMException) { }
                catch (ArgumentException) { }
            }
            nativeEvents.Enqueue(new(eventId, window, objectId, childId, name, state));
        };
        nint hook = SetWinEventHook(FocusChange, StateChange, nint.Zero, callback, (uint)Environment.ProcessId, 0, 0);
        if (hook == nint.Zero) throw new InvalidOperationException("The native accessibility event hook failed.");

        void OnUi(Action action)
        {
            TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            form.BeginInvoke(() =>
            {
                try { action(); completion.SetResult(); }
                catch (Exception error) { completion.SetException(error); }
            });
            if (!completion.Task.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("UI action timed out.");
        }
        void Require(bool condition, string description)
        {
            if (!condition) failures.Add(description);
        }
        Task worker = Task.Run(() =>
        {
            AutomationElement check = AutomationElement.FromHandle(checkboxHandle);
            AutomationPropertyChangedEventHandler handler = (sender, args) =>
            {
                try { uiaEvents.Enqueue(new(((AutomationElement)sender).Current.Name, args.NewValue?.ToString() ?? "")); }
                catch (ElementNotAvailableException) { }
            };
            Automation.AddAutomationPropertyChangedEventHandler(check, TreeScope.Element, handler, TogglePattern.ToggleStateProperty);
            try
            {
                void BeginAction(Control control, TreeNode? selected = null)
                {
                    OnUi(() => { form.Activate(); if (control is TreeView tree && selected is not null) tree.SelectedNode = selected; control.Focus(); });
                    Thread.Sleep(150);
                    nativeEvents.Clear();
                    uiaEvents.Clear();
                }
                void Keyboard(Control control, Keys key)
                {
                    OnUi(() =>
                    {
                        control.Focus();
                        SendMessage(control.Handle, 0x0100, (nint)key, (nint)1);
                        SendMessage(control.Handle, 0x0101, (nint)key, unchecked((nint)(int)0xC0000001));
                    });
                }
                void Observe(string label, Control control, TreeNode? node, uint expectedState, bool expectEvent, bool expectUia)
                {
                    Thread.Sleep(350);
                    uint state = 0;
                    string name = "";
                    int childId = 0;
                    OnUi(() =>
                    {
                        Accessibility.IAccessible accessible = Client(control.Handle);
                        childId = node is null ? 0 : (int)SendMessage(control.Handle, TreeItemToAccessibleId, node.Handle, nint.Zero);
                        state = Convert.ToUInt32(accessible.get_accState(childId));
                        name = accessible.get_accName(childId) ?? "";
                    });
                    string toggle = node is null ? ((TogglePattern)check.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState.ToString() : "native MSAA";
                    NativeEvent[] matching = nativeEvents.Where(value => value.Event == StateChange && value.Window == control.Handle.ToInt64() && value.Child == childId).ToArray();
                    int nativeCount = matching.Count(value => (value.State & (CheckedState | MixedState)) == expectedState);
                    int uiaCount = uiaEvents.Count(value => value.Name == name && value.Value == (expectedState == CheckedState ? "1" : "0"));
                    observations.Add(new(label, name, toggle, state, expectedState, nativeCount, uiaCount));
                    Require((state & (CheckedState | MixedState)) == expectedState, $"{label}: wrong queried MSAA state");
                    Require(!expectEvent || nativeCount > 0, $"{label}: no resolvable final-state MSAA event for the actual item");
                    Require(!expectUia || uiaCount > 0, $"{label}: no final-state UIA Toggle event for the actual checkbox");
                    Require(node is not null || toggle == (expectedState == CheckedState ? "On" : "Off"), $"{label}: wrong queried UIA checkbox state");
                    if (node is not null) Require(name == node.Text, $"{label}: node name was replaced by its tree heading/help");
                }

                BeginAction(checkbox);
                Observe("plain-initial-off", checkbox, null, 0, false, false);
                Keyboard(checkbox, Keys.Space);
                Observe("plain-keyboard-on", checkbox, null, CheckedState, true, true);
                BeginAction(checkbox);
                OnUi(() => SendMessage(checkboxHandle, 0x00F5, nint.Zero, nint.Zero));
                Observe("plain-mouse-off", checkbox, null, 0, true, true);
                if (!recordOnly && failures.Count != 0)
                    return;

                foreach ((TriStateTreeView tree, TreeNode parent, string label) in new[]
                {
                    (available, availableParent, "available"), (installed, installedParent, "installed")
                })
                {
                    OnUi(() =>
                    {
                        Accessibility.IAccessible accessible = Client(tree.Handle);
                        Require(accessible.get_accName(0) == tree.AccessibleName, $"{label}: accessible tree label lost: '{accessible.get_accName(0)}'");
                        Require(string.IsNullOrEmpty(accessible.get_accDescription(0)), $"{label}: unexpected root description");
                        Require(tree.ShowNodeToolTips == hints, $"{label}: optional hints setting lost");
                        Require(tree.Nodes.Cast<TreeNode>().All(node => hints || node.ToolTipText.Length == 0), $"{label}: disabled node hint leaked");
                    });
                    BeginAction(tree, parent);
                    Observe(label + "-initial-off", tree, parent, 0, false, false);
                    Keyboard(tree, Keys.Space);
                    Observe(label + "-keyboard-on", tree, parent, CheckedState, true, false);
                    BeginAction(tree, parent);
                    Keyboard(tree, Keys.Space);
                    Observe(label + "-keyboard-off", tree, parent, 0, true, false);
                    BeginAction(tree, parent.Nodes[0]);
                    Keyboard(tree, Keys.Space);
                    Observe(label + "-child-on", tree, parent.Nodes[0], CheckedState, true, false);
                    Observe(label + "-partial", tree, parent, MixedState, true, false);
                    BeginAction(tree, parent.Nodes[0]);
                    Keyboard(tree, Keys.Space);
                    Observe(label + "-partial-to-off", tree, parent, 0, true, false);
                    BeginAction(tree, parent.Nodes[0]);
                    Keyboard(tree, Keys.Space);
                    Observe(label + "-partial-again", tree, parent, MixedState, true, false);
                    BeginAction(tree, parent);
                    OnUi(() =>
                    {
                        Rectangle bounds = parent.Bounds;
                        int y = bounds.Top + bounds.Height / 2;
                        int x = Enumerable.Range(0, bounds.Left).First(value => (tree.HitTest(value, y).Location & TreeViewHitTestLocations.StateImage) != 0);
                        nint point = (nint)((y << 16) | x);
                        SendMessage(tree.Handle, 0x0201, (nint)1, point);
                        SendMessage(tree.Handle, 0x0202, nint.Zero, point);
                    });
                    Observe(label + "-mouse-on", tree, parent, CheckedState, true, false);
                    OnUi(() => parent.Expand());
                    BeginAction(tree, parent);
                    Keyboard(tree, Keys.Down);
                    Thread.Sleep(150);
                    OnUi(() => Require(tree.SelectedNode == parent.Nodes[0], $"{label}: Down did not select the first child"));
                    NativeEvent[] focusEvents = nativeEvents.Where(value => value.Event == FocusChange && value.Window == tree.Handle.ToInt64()).ToArray();
                    Require(focusEvents.Length == 1 && focusEvents[0].Name == parent.Nodes[0].Text,
                        $"{label}: child focus must announce exactly the child, without a repeated heading");
                    Keyboard(tree, Keys.Left);
                    OnUi(() => Require(tree.SelectedNode == parent, $"{label}: Left did not return to the parent"));
                    Keyboard(tree, Keys.Left);
                    OnUi(() => Require(!parent.IsExpanded, $"{label}: Left did not collapse the parent"));
                    Keyboard(tree, Keys.Down);
                    OnUi(() => Require(tree.SelectedNode == parent.NextNode || parent.NextNode is null, $"{label}: Down did not move between collapsed languages"));
                    OnUi(() => { parent.Expand(); tree.SelectedNode = parent; });
                    OnUi(() => typeof(Control).GetMethod("RecreateHandle", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(tree, null));
                    Observe(label + "-recreated-on", tree, parent, CheckedState, false, false);
                    OnUi(() => Require(Client(tree.Handle).get_accName(0) == tree.AccessibleName, $"{label}: recreated handle label lost"));
                }
                OnUi(() =>
                {
                    Field<ToolStripMenuItem>(form, "polishLanguageItem").PerformClick();
                    Require(Client(installed.Handle).get_accName(0) == UiText.InstalledGroup, "Localized native tree label lost");
                    typeof(MainForm).GetMethod("RebuildTrees", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, new object?[] { null });
                    foreach (TriStateTreeView tree in new[] { available, installed })
                    foreach (TreeNode parent in tree.Nodes)
                    foreach (TreeNode node in new[] { parent }.Concat(parent.Nodes.Cast<TreeNode>()))
                        Require((int)SendMessage(tree.Handle, 0x1127, node.Handle, (nint)0xF000) == 0x1000,
                            "Rebuilt nodes retain their native unchecked image");
                });
            }
            finally { Automation.RemoveAutomationPropertyChangedEventHandler(check, handler); }
        });
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            while (!worker.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(55))
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }
            if (!worker.IsCompleted) throw new TimeoutException("Real-provider accessibility regression timed out.");
            worker.GetAwaiter().GetResult();
        }
        finally { UnhookWinEvent(hook); GC.KeepAlive(callback); form.Dispose(); }
        File.WriteAllText(Path.Combine(path, "events.json"), JsonSerializer.Serialize(new
        {
            Runtime = RuntimeInformation.FrameworkDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(), Hints = hints,
            Observations = observations, Failures = failures
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (!recordOnly && failures.Count != 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
    }

    private static Accessibility.IAccessible Client(nint handle)
    {
        Guid interfaceId = new("618736E0-3C3D-11CF-810C-00AA00389B71");
        Marshal.ThrowExceptionForHR(AccessibleObjectFromWindow(handle, unchecked((uint)ClientObject), ref interfaceId, out object accessible));
        return (Accessibility.IAccessible)accessible;
    }

    private static T Field<T>(MainForm form, string name) => (T)typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;

    private sealed class FixtureRegistry : IVoiceRegistry
    {
        public string ArchitectureName => Environment.Is64BitProcess ? "64-bit" : "32-bit";
        public IReadOnlyList<VoiceConfiguration> ReadInstalledVoices() => new[] { new VoiceConfiguration("en", 50), new VoiceConfiguration("en+f1", 50), new VoiceConfiguration("pl", 50) };
        public (IReadOnlyList<string> Voices, IReadOnlyList<string> Variants) DiscoverAvailableVoices() => (new[] { "en", "pl" }, new[] { "m1", "f1" });
        public LegacySpeedSettings ReadLegacySpeedSettings() => new(false, false, SonicMode.Smooth);
        public void Apply(IReadOnlyList<VoiceConfiguration> requestedVoices) => throw new InvalidOperationException("Accessibility checks do not register SAPI voices.");
        public void Dispose() { }
    }

    private delegate void WinEventDelegate(nint hook, uint eventId, nint window, int objectId, int childId, uint eventThread, uint eventTime);
    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(uint minimum, uint maximum, nint module, WinEventDelegate callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint parameter, nint value);
    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(nint window, uint objectId, ref Guid interfaceId, [MarshalAs(UnmanagedType.Interface)] out object accessible);
    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromEvent(nint window, int objectId, int childId,
        [MarshalAs(UnmanagedType.Interface)] out Accessibility.IAccessible accessible, [MarshalAs(UnmanagedType.Struct)] out object child);
}
