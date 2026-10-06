using System.Runtime.InteropServices;

namespace Vario;

internal static class Appearance
{
    internal static void Apply(Form form, MenuStrip menu, AppTheme theme)
    {
        bool dark = theme == AppTheme.Dark && !SystemInformation.HighContrast;
        ApplyColors(form, dark);
        menu.Renderer = dark ? new ToolStripProfessionalRenderer(new DarkMenuColors()) : new ToolStripSystemRenderer();
        ApplyMenuColors(menu.Items, dark);
        if (form.IsHandleCreated)
        {
            int enabled = dark ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
        }
    }

    private static void ApplyColors(Control control, bool dark)
    {
        bool input = control is TreeView or NumericUpDown or TextBox;
        control.BackColor = dark ? Color.FromArgb(input ? 43 : 32, input ? 43 : 32, input ? 43 : 32)
            : input ? SystemColors.Window : SystemColors.Control;
        control.ForeColor = dark ? Color.FromArgb(242, 242, 242)
            : input ? SystemColors.WindowText : SystemColors.ControlText;
        if (control is ButtonBase button)
            button.UseVisualStyleBackColor = !dark;
        foreach (Control child in control.Controls)
            ApplyColors(child, dark);
    }

    private static void ApplyMenuColors(ToolStripItemCollection items, bool dark)
    {
        foreach (ToolStripItem item in items)
        {
            item.ForeColor = dark ? Color.FromArgb(242, 242, 242) : SystemColors.ControlText;
            item.BackColor = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
            if (item is ToolStripMenuItem submenu)
                ApplyMenuColors(submenu.DropDownItems, dark);
        }
    }

    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(32, 32, 32);
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
        public override Color MenuItemSelected => Color.FromArgb(62, 62, 62);
        public override Color MenuItemSelectedGradientBegin => MenuItemSelected;
        public override Color MenuItemSelectedGradientEnd => MenuItemSelected;
        public override Color MenuItemPressedGradientBegin => MenuItemSelected;
        public override Color MenuItemPressedGradientMiddle => MenuItemSelected;
        public override Color MenuItemPressedGradientEnd => MenuItemSelected;
        public override Color MenuBorder => Color.FromArgb(100, 100, 100);
        public override Color MenuItemBorder => MenuBorder;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
