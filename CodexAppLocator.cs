using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CodexUniversalLauncher;

internal static class CodexAppLocator
{
    public static async Task<InstalledCodexApp?> FindAsync(CancellationToken cancellationToken)
    {
        try
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var powershell = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            const string command =
                "[Console]::OutputEncoding=[Text.Encoding]::UTF8; " +
                "@(Get-StartApps | Where-Object { ($_.Name -match 'Codex|ChatGPT') -or ($_.AppID -match '^OpenAI\\.') } | " +
                "Where-Object { $_.AppID -match '!' } | Select-Object Name,AppID) | ConvertTo-Json -Compress";
            var start = new ProcessStartInfo(powershell)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-Command");
            start.ArgumentList.Add(command);
            using var process = Process.Start(start);
            if (process is null)
                return null;
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(7), cancellationToken);
            var output = await outputTask;
            var apps = ParseStartApps(output);
            return apps
                .OrderByDescending(app => app.AppId.Contains("OpenAI.Codex", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(app => app.Name.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(app => app.Name.Contains("Codex", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    internal static IReadOnlyList<InstalledCodexApp> ParseStartApps(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        using var document = JsonDocument.Parse(json);
        var elements = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToList()
            : [document.RootElement];
        var results = new List<InstalledCodexApp>();
        foreach (var element in elements)
        {
            if (!element.TryGetProperty("Name", out var nameElement) ||
                !element.TryGetProperty("AppID", out var idElement))
                continue;
            var name = nameElement.GetString();
            var appId = idElement.GetString();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(appId) ||
                !appId.Contains('!'))
                continue;
            results.Add(new InstalledCodexApp(name, appId));
        }
        return results;
    }

    public static void Launch(InstalledCodexApp app)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"shell:AppsFolder\\{app.AppId}",
            UseShellExecute = true
        });
    }
}
