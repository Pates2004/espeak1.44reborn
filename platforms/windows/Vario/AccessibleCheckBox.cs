using System.Runtime.InteropServices;

namespace Vario;

internal sealed class AccessibleCheckBox : CheckBox
{
    private const uint ObjectStateChange = 0x800A;
    private const int ClientObject = -4;
    private const int ToggleStateProperty = 30086;
    private static readonly Guid ProviderInterface = new("D6DD68D1-86FD-4332-8666-9ABEDEA2D24C");
    private CheckState announcedState;

    protected override void OnCheckStateChanged(EventArgs e)
    {
        base.OnCheckStateChanged(e);
        if (IsHandleCreated)
        {
            // Some .NET10 servicing versions omit MSAA state events and do not
            // publish a UIA ToggleState change. Notify both actual providers.
            NotifyWinEvent(ObjectStateChange, Handle, ClientObject, 0);
            nint unknown = Marshal.GetIUnknownForObject(AccessibilityObject);
            nint provider = nint.Zero;
            try
            {
                if (Marshal.QueryInterface(unknown, in ProviderInterface, out provider) == 0)
                    UiaRaiseAutomationPropertyChangedEvent(provider, ToggleStateProperty, (int)announcedState, (int)CheckState);
            }
            finally
            {
                if (provider != nint.Zero) Marshal.Release(provider);
                Marshal.Release(unknown);
            }
        }
        announcedState = CheckState;
    }

    [DllImport("user32.dll")]
    private static extern void NotifyWinEvent(uint eventId, nint window, int objectId, int childId);

    [DllImport("uiautomationcore.dll")]
    private static extern int UiaRaiseAutomationPropertyChangedEvent(nint provider, int propertyId,
        [MarshalAs(UnmanagedType.Struct)] object oldValue, [MarshalAs(UnmanagedType.Struct)] object newValue);
}
