using System.Runtime.InteropServices;

namespace Vario;

internal enum TreeNodeCheckState
{
    Unchecked = 1,
    Checked = 2,
    Partial = 3
}

internal sealed class TriStateTreeView : TreeView
{
    private const int WmLeftButtonDown = 0x0201;
    private const int TvFirst = 0x1100;
    private const int TvmSetItemW = TvFirst + 63;
    private const int TvmSetExtendedStyle = TvFirst + 44;
    private const int TvsExPartialCheckBoxes = 0x0080;
    private const uint TvifState = 0x0008;
    private const uint TvisStateImageMask = 0xF000;

    internal event EventHandler<TreeViewEventArgs>? CheckStateChanged;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SendMessage(Handle, TvmSetExtendedStyle,
            (nint)TvsExPartialCheckBoxes, (nint)TvsExPartialCheckBoxes);
        ApplyStoredStates(Nodes);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && SelectedNode is not null)
        {
            ToggleNode(SelectedNode);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmLeftButtonDown)
        {
            long coordinates = message.LParam.ToInt64();
            int x = unchecked((short)(coordinates & 0xFFFF));
            int y = unchecked((short)((coordinates >> 16) & 0xFFFF));
            TreeViewHitTestInfo hit = HitTest(x, y);
            if (hit.Node is not null && (hit.Location & TreeViewHitTestLocations.StateImage) != 0)
            {
                SelectedNode = hit.Node;
                Focus();
                ToggleNode(hit.Node);
                return;
            }
        }

        base.WndProc(ref message);
    }

    internal TreeNodeCheckState GetCheckState(TreeNode node) =>
        node.StateImageIndex switch
        {
            1 => TreeNodeCheckState.Checked,
            2 => TreeNodeCheckState.Partial,
            _ => TreeNodeCheckState.Unchecked
        };

    internal void SetCheckState(TreeNode node, TreeNodeCheckState state)
    {
        node.StateImageIndex = (int)state - 1;
        if (IsHandleCreated && node.TreeView == this && node.Handle != nint.Zero)
            ApplyNativeState(node, state);
    }

    private void ToggleNode(TreeNode node)
    {
        TreeNodeCheckState next = GetCheckState(node) == TreeNodeCheckState.Checked
            ? TreeNodeCheckState.Unchecked
            : TreeNodeCheckState.Checked;
        SetCheckState(node, next);
        CheckStateChanged?.Invoke(this, new TreeViewEventArgs(node));
    }

    private void ApplyStoredStates(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            ApplyNativeState(node, GetCheckState(node));
            ApplyStoredStates(node.Nodes);
        }
    }

    private void ApplyNativeState(TreeNode node, TreeNodeCheckState state)
    {
        TvItem item = new()
        {
            Mask = TvifState,
            Item = node.Handle,
            State = (uint)state << 12,
            StateMask = TvisStateImageMask
        };
        SendMessage(Handle, TvmSetItemW, nint.Zero, ref item);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TvItem
    {
        internal uint Mask;
        internal nint Item;
        internal uint State;
        internal uint StateMask;
        internal nint Text;
        internal int TextLength;
        internal int Image;
        internal int SelectedImage;
        internal int Children;
        internal nint Parameter;
    }

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint parameter, nint value);

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint parameter, ref TvItem value);
}
