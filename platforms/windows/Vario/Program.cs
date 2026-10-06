namespace Vario;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception exception)
        {
            MessageBox.Show(UiText.StartFailed + Environment.NewLine + exception.Message, UiText.Warning,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
