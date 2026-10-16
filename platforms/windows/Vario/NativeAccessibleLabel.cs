using System.Runtime.InteropServices;

namespace Vario;

// Dynamic annotation labels the native tree root without replacing its item
// provider or repeating the heading as part of every child item's name.
internal sealed class NativeAccessibleLabel : IDisposable
{
    private static readonly Guid NameProperty = new("608D3DF8-8128-4AA7-A428-F55E49267291");
    private static readonly Guid DescriptionProperty = new("4D48DFE4-BD3F-491F-A648-492D6F20C588");
    private const uint ClientObject = unchecked((uint)-4);
    private readonly nint handle;
    private IAccPropServices? services;

    internal NativeAccessibleLabel(nint handle)
    {
        this.handle = handle;
        Type type = Type.GetTypeFromCLSID(new("B5F8350B-0548-48B1-A6EE-88BD00B4A5E7"), throwOnError: true)!;
        services = (IAccPropServices)Activator.CreateInstance(type)!;
    }

    internal void Set(string name, string description)
    {
        IAccPropServices current = services ?? throw new ObjectDisposedException(nameof(NativeAccessibleLabel));
        current.SetHwndPropStr(handle, ClientObject, 0, NameProperty, name);
        current.SetHwndPropStr(handle, ClientObject, 0, DescriptionProperty, description);
    }

    public void Dispose()
    {
        IAccPropServices? current = Interlocked.Exchange(ref services, null);
        if (current is null)
            return;
        try
        {
            current.ClearHwndProps(handle, ClientObject, 0, new[] { NameProperty, DescriptionProperty }, 2);
        }
        catch (COMException)
        {
            // The native HWND can already be gone during handle destruction.
            // Annotation setup errors still propagate from Set.
        }
        finally
        {
            Marshal.ReleaseComObject(current);
        }
    }

    [ComImport, Guid("6E26E776-04F0-495D-80E4-3330352E3169"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAccPropServices
    {
        void SetPropValue(nint identity, uint length, Guid property, [MarshalAs(UnmanagedType.Struct)] object value);
        void SetPropServer(nint identity, uint length, nint properties, int count, nint server, int scope);
        void ClearProps(nint identity, uint length, nint properties, int count);
        void SetHwndProp(nint window, uint objectId, uint child, Guid property, [MarshalAs(UnmanagedType.Struct)] object value);
        void SetHwndPropStr(nint window, uint objectId, uint child, Guid property, [MarshalAs(UnmanagedType.LPWStr)] string value);
        void SetHwndPropServer(nint window, uint objectId, uint child, nint properties, int count, nint server, int scope);
        void ClearHwndProps(nint window, uint objectId, uint child,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] Guid[] properties, int count);
    }
}
