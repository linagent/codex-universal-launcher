namespace CodexUniversalLauncher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var options = LauncherOptions.Parse(args);
#if SCREENSHOT
        options = options with { CheckOnly = true, Silent = false, NoRepair = true, Demo = true };
#endif
        if (options.SelfTest)
            return SelfTests.Run(options.ResultPath);

        ApplicationConfiguration.Initialize();
        using var form = new LauncherForm(options);
#if SCREENSHOT
        var captureDirectory = Environment.GetEnvironmentVariable("CODEX_SCREENSHOT_DIR");
        if (!string.IsNullOrWhiteSpace(captureDirectory))
        {
            form.Shown += async (_, _) =>
            {
                Directory.CreateDirectory(captureDirectory);
                await Task.Delay(1500);
                CaptureForm(form, Path.Combine(captureDirectory, "preflight-in-progress.png"));
                await Task.Delay(4300);
                CaptureForm(form, Path.Combine(captureDirectory, "preflight-success.png"));
                form.Close();
            };
        }
#endif
        Application.Run(form);
        return form.ExitCode;
    }

#if SCREENSHOT
    private static void CaptureForm(Form form, string path)
    {
        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bitmap, form.ClientRectangle);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
#endif
}
