using System.Runtime.InteropServices;
using System.Text.Json;

namespace CodexUniversalLauncher;

internal static class EnvironmentRepair
{
    private static readonly string[] ManagedNames =
    [
        "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY"
    ];

    public static EnvironmentRepairResult Apply(
        ConnectivityDecision decision,
        ProxyDiscoveryResult discovery,
        bool repair)
    {
        var current = ManagedNames.ToDictionary(
            name => name,
            name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User),
            StringComparer.OrdinalIgnoreCase);
        var desired = new Dictionary<string, string?>(current, StringComparer.OrdinalIgnoreCase);

        switch (decision.Kind)
        {
            case RouteKind.HttpProxy when decision.Proxy is not null:
                desired["HTTP_PROXY"] = decision.Proxy.ValueForEnvironment;
                desired["HTTPS_PROXY"] = decision.Proxy.ValueForEnvironment;
                desired["ALL_PROXY"] = decision.Proxy.ValueForEnvironment;
                desired["NO_PROXY"] = MergeNoProxy(current["NO_PROXY"]);
                break;

            case RouteKind.Socks5Proxy when decision.Proxy is not null:
                desired["ALL_PROXY"] = decision.Proxy.ValueForEnvironment;
                desired["NO_PROXY"] = MergeNoProxy(current["NO_PROXY"]);
                break;

            case RouteKind.Direct when decision.ExplicitProxyFailed:
                foreach (var name in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY" })
                {
                    var value = current[name];
                    if (IsLocalProxyValue(value))
                        desired[name] = null;
                }
                break;
        }

        var changes = ManagedNames
            .Where(name => !string.Equals(current[name], desired[name], StringComparison.OrdinalIgnoreCase))
            .Select(name => new EnvironmentChange(name, current[name], desired[name]))
            .ToList();

        if (changes.Count == 0)
            return new EnvironmentRepairResult(changes, "无需修改用户代理环境变量", null);

        if (changes.Any(change => ContainsCredentials(change.Before) || ContainsCredentials(change.After)))
        {
            return new EnvironmentRepairResult(
                [],
                "代理地址含凭据；为避免复制或落盘敏感信息，启动器未自动修改环境变量",
                null);
        }

        if (!repair)
        {
            var preview = string.Join("、", changes.Select(change =>
                $"{change.Name}:{FormatValue(change.Before)}→{FormatValue(change.After)}"));
            return new EnvironmentRepairResult(changes, "计划修改：" + preview, null);
        }

        var backupPath = SaveBackup(current);
        foreach (var change in changes)
        {
            Environment.SetEnvironmentVariable(change.Name, change.After, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(change.Name, change.After, EnvironmentVariableTarget.Process);
        }
        BroadcastEnvironmentChange();

        var summary = "已备份并修改：" + string.Join("、", changes.Select(change => change.Name));
        return new EnvironmentRepairResult(changes, summary, backupPath);
    }

    public static string? FindLatestBackup()
    {
        var directory = AppStorage.BackupDirectory;
        if (!Directory.Exists(directory))
            return null;
        return Directory.GetFiles(directory, "proxy-environment-*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public static IReadOnlyList<EnvironmentChange> Restore(string path)
    {
        var json = File.ReadAllText(path);
        var backup = JsonSerializer.Deserialize<ProxyBackup>(json) ??
                     throw new InvalidDataException("备份文件格式无效");
        var changes = new List<EnvironmentChange>();
        foreach (var name in ManagedNames)
        {
            backup.Values.TryGetValue(name, out var desired);
            var current = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            if (string.Equals(current, desired, StringComparison.Ordinal))
                continue;
            Environment.SetEnvironmentVariable(name, desired, EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(name, desired, EnvironmentVariableTarget.Process);
            changes.Add(new EnvironmentChange(name, current, desired));
        }
        BroadcastEnvironmentChange();
        return changes;
    }

    internal static string MergeNoProxy(string? existing)
    {
        var values = (existing ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        foreach (var required in new[] { "localhost", "127.0.0.1", "::1" })
        {
            if (!values.Contains(required, StringComparer.OrdinalIgnoreCase))
                values.Add(required);
        }
        return string.Join(",", values);
    }

    private static bool IsLocalProxyValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        foreach (var candidate in ProxyDiscovery.ParseProxyValues(value, "stale-check", 0))
        {
            if (candidate.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                candidate.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                candidate.Host.Equals("::1", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool ContainsCredentials(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;
        return !string.IsNullOrWhiteSpace(uri.UserInfo);
    }

    private static string FormatValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "<空>" : SafeText.Redact(value);

    private static string SaveBackup(IReadOnlyDictionary<string, string?> values)
    {
        Directory.CreateDirectory(AppStorage.BackupDirectory);
        var path = Path.Combine(
            AppStorage.BackupDirectory,
            $"proxy-environment-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var backup = new ProxyBackup(DateTimeOffset.Now, values.ToDictionary(pair => pair.Key, pair => pair.Value));
        File.WriteAllText(path, JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static void BroadcastEnvironmentChange()
    {
        SendMessageTimeout(
            new IntPtr(0xffff), 0x001A, UIntPtr.Zero, "Environment",
            0x0002, 5000, out _);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint msg, UIntPtr wParam, string lParam,
        uint flags, uint timeout, out UIntPtr result);

    private sealed record ProxyBackup(DateTimeOffset CreatedAt, Dictionary<string, string?> Values);
}
