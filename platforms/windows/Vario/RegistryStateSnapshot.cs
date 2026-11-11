using Microsoft.Win32;
using System.Security.AccessControl;
using System.Text.RegularExpressions;

namespace Vario;

internal sealed class RegistryStateSnapshot : IVoiceRegistrySnapshot
{
    private sealed record StoredValue(object Value, RegistryValueKind Kind);
    private sealed record StoredKey(Dictionary<string, StoredValue> Values,
        Dictionary<string, StoredKey> Children, byte[] SecurityDescriptor);

    private readonly RegistryKey machine;
    private readonly string voicesPath;
    private readonly string converterPath;
    private readonly Regex managedNames;
    private readonly Dictionary<string, StoredKey> voices;
    private readonly StoredKey? converter;

    private RegistryStateSnapshot(RegistryKey machine, string voicesPath, string converterPath,
        Regex managedNames, Dictionary<string, StoredKey> voices, StoredKey? converter)
    {
        this.machine = machine;
        this.voicesPath = voicesPath;
        this.converterPath = converterPath;
        this.managedNames = managedNames;
        this.voices = voices;
        this.converter = converter;
    }

    internal static RegistryStateSnapshot Capture(RegistryKey machine, string voicesPath,
        string converterPath, Regex managedNames)
    {
        Dictionary<string, StoredKey> voices = new(StringComparer.OrdinalIgnoreCase);
        using (RegistryKey? tokens = machine.OpenSubKey(voicesPath, writable: false))
            if (tokens is not null)
                foreach (string name in tokens.GetSubKeyNames().Where(name => managedNames.IsMatch(name)))
                {
                    using RegistryKey key = OpenForRestore(tokens, name)
                        ?? throw new IOException(UiText.InstallationChanged);
                    voices.Add(name, Read(key));
                }
        using RegistryKey? converter = OpenForRestore(machine, converterPath);
        return new(machine, voicesPath, converterPath, managedNames, voices,
            converter is null ? null : Read(converter));
    }

    public void Restore()
    {
        List<Exception> errors = new();
        TryRestore(() => RestoreVoices(errors), errors);
        TryRestore(RestoreConverter, errors);
        if (errors.Count > 0)
            throw new AggregateException(UiText.RollbackFailed, errors);
    }

    private void RestoreVoices(List<Exception> errors)
    {
        using RegistryKey? tokens = voices.Count > 0
            ? machine.CreateSubKey(voicesPath, writable: true)
            : machine.OpenSubKey(voicesPath, writable: true);
        if (tokens is not null)
        {
            foreach (string name in tokens.GetSubKeyNames().Where(name => managedNames.IsMatch(name)))
                if (!voices.ContainsKey(name))
                    TryRestore(() => tokens.DeleteSubKeyTree(name, throwOnMissingSubKey: false), errors);
            foreach ((string name, StoredKey stored) in voices)
                TryRestore(() =>
                {
                    using RegistryKey created = tokens.CreateSubKey(name, writable: true);
                    using RegistryKey key = OpenForRestore(tokens, name)
                        ?? throw new IOException(UiText.RollbackFailed);
                    Write(key, stored);
                }, errors);
        }
    }

    private void RestoreConverter()
    {
        if (converter is null)
            machine.DeleteSubKeyTree(converterPath, throwOnMissingSubKey: false);
        else
        {
            using RegistryKey created = machine.CreateSubKey(converterPath, writable: true);
            using RegistryKey key = OpenForRestore(machine, converterPath)
                ?? throw new IOException(UiText.RollbackFailed);
            Write(key, converter);
        }
    }

    private static RegistryKey? OpenForRestore(RegistryKey parent, string path) =>
        parent.OpenSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.FullControl);

    private static void TryRestore(Action action, List<Exception> errors)
    {
        try { action(); }
        catch (Exception exception) { errors.Add(exception); }
    }

    private static StoredKey Read(RegistryKey key)
    {
        Dictionary<string, StoredValue> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in key.GetValueNames())
        {
            object value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                ?? throw new IOException(UiText.InstallationChanged);
            values.Add(name, new(value, key.GetValueKind(name)));
        }
        Dictionary<string, StoredKey> children = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in key.GetSubKeyNames())
        {
            using RegistryKey child = OpenForRestore(key, name)
                ?? throw new IOException(UiText.InstallationChanged);
            children.Add(name, Read(child));
        }
        RegistrySecurity security = key.GetAccessControl(AccessControlSections.Access |
            AccessControlSections.Owner | AccessControlSections.Group);
        return new(values, children, security.GetSecurityDescriptorBinaryForm());
    }

    private static void Write(RegistryKey key, StoredKey stored)
    {
        foreach (string name in key.GetValueNames())
            if (!stored.Values.ContainsKey(name))
                key.DeleteValue(name, throwOnMissingValue: false);
        foreach ((string name, StoredValue value) in stored.Values)
            key.SetValue(name, value.Value, value.Kind);
        foreach (string name in key.GetSubKeyNames())
            if (!stored.Children.ContainsKey(name))
                key.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
        foreach ((string name, StoredKey child) in stored.Children)
        {
            using RegistryKey created = key.CreateSubKey(name, writable: true);
            using RegistryKey target = OpenForRestore(key, name)
                ?? throw new IOException(UiText.RollbackFailed);
            Write(target, child);
        }
        RegistrySecurity security = new();
        security.SetSecurityDescriptorBinaryForm(stored.SecurityDescriptor,
            AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group);
        key.SetAccessControl(security);
    }
}
