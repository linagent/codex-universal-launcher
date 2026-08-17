using System.Text;

namespace CodexUniversalLauncher;

internal static class AppStorage
{
    private const long MaxLogBytes = 1_000_000;

    public static string RootDirectory { get; } = ResolveRoot();
    public static string BackupDirectory => Path.Combine(RootDirectory, "backups");
    public static string ReportDirectory => Path.Combine(RootDirectory, "reports");
    public static string LogPath => Path.Combine(RootDirectory, "launcher-v2.log");

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(RootDirectory);
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogBytes)
                File.Move(LogPath, LogPath + ".old", overwrite: true);
            File.AppendAllText(
                LogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {SafeText.Redact(message)}{Environment.NewLine}",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
            // Logging must never prevent startup.
        }
    }

    public static string? WriteDiagnosticReport(
        CodexContext context,
        ProxyDiscoveryResult discovery,
        ConnectivityDecision decision,
        EnvironmentRepairResult repair,
        InstalledCodexApp? app)
    {
        try
        {
            Directory.CreateDirectory(ReportDirectory);
            var path = Path.Combine(ReportDirectory, $"Codex-Network-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var lines = new List<string>
            {
                "Codex 通用启动器脱敏诊断报告",
                $"版本: 2.0.0",
                $"时间: {DateTimeOffset.Now:O}",
                "说明: 未收集 API Key、Token、Cookie、聊天正文或项目文件。",
                "",
                "[目标]"
            };
            lines.AddRange(context.Targets.Select(target =>
                $"- {target.Display} ({target.Reason}{(target.Required ? string.Empty : "，辅助目标")})"));
            lines.Add("");
            lines.Add("[系统信号]");
            lines.Add("- " + discovery.SystemProxySummary);
            lines.Add("- " + discovery.PacSummary);
            lines.AddRange(discovery.ActiveTunnelAdapters.Select(adapter => "- 隧道适配器: " + adapter));
            lines.Add("");
            lines.Add("[代理候选]");
            lines.AddRange(discovery.Candidates.Select(candidate =>
                $"- {candidate.Display} | {candidate.Source} | {candidate.Owner ?? "owner unknown"}"));
            lines.Add("");
            lines.Add("[连通性探测]");
            lines.AddRange(decision.Probes.Select(probe =>
                $"- {(probe.Success ? "OK" : "FAIL")} | {probe.Route} | {probe.Detail}"));
            lines.Add("");
            lines.Add("[结论]");
            lines.Add("- " + decision.Summary);
            lines.Add("- 环境变量: " + repair.Summary);
            lines.Add("- 启动目标: " + (app is null ? "未找到" : $"{app.Name} | {app.AppId}"));
            File.WriteAllLines(path, lines.Select(SafeText.Redact), new UTF8Encoding(true));
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveRoot()
    {
        var preferred = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexUniversalLauncher");
        try
        {
            Directory.CreateDirectory(preferred);
            return preferred;
        }
        catch
        {
            return Path.Combine(AppContext.BaseDirectory, "data");
        }
    }
}
