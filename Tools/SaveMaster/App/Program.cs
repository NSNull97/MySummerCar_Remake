namespace MySummerRemake.SaveMaster;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        bool responsivenessCheck = args.Length == 3 && args[0] is "--responsiveness-check" or "--responsiveness-stress";
        var window = new MainWindow(null);
        Application.ThreadException += (_, e) => MessageBox.Show(
            Form.ActiveForm ?? window, e.Exception.Message, "Save Master — операция прервана", MessageBoxButtons.OK, MessageBoxIcon.Error);
        window.Shown += (_, _) => window.BeginInvoke(async () =>
        {
            if (responsivenessCheck) await window.RunResponsivenessCheckAsync(args[1], args[2], includeWindowStress: args[0] == "--responsiveness-stress");
            else
            {
                string? initialPath = args.FirstOrDefault(File.Exists);
                if (initialPath is not null) await window.OpenFileAsync(initialPath);
                if (args.Length == 3 && args[0] == "--ui-check") window.RunUiCheck(args[2]);
            }
        });
        Application.Run(window);
    }
}
