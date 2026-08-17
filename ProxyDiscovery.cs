using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CodexUniversalLauncher;

internal static partial class ProxyDiscovery
{
    private static readonly string[] ProxyEnvironmentNames =
    [
        "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY"
    ];

    public static async Task<ProxyDiscoveryResult> DiscoverAsync(
        IReadOnlyList<TargetEndpoint> targets,
        CancellationToken cancellationToken)
    {
        var candidates = new Dictionary<string, ProxyCandidate>(StringComparer.OrdinalIgnoreCase);
        var userEnvironment = ProxyEnvironmentNames
            .Append("NO_PROXY")
            .ToDictionary(
                name => name,
                name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User),
                StringComparer.OrdinalIgnoreCase);

        var hasExplicitConfiguration = false;
        foreach (var name in ProxyEnvironmentNames)
        {
            AddEnvironmentCandidate(name, EnvironmentVariableTarget.User, 10, candidates, ref hasExplicitConfiguration);
            AddEnvironmentCandidate(name, EnvironmentVariableTarget.Process, 14, candidates, ref hasExplicitConfiguration);
            AddEnvironmentCandidate(name, EnvironmentVariableTarget.Machine, 18, candidates, ref hasExplicitConfiguration);
        }

        var systemProxySummary = "未启用静态系统代理";
        var pacSummary = "未配置 PAC";
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", writable: false);
            if (key is not null)
            {
                var enabled = Convert.ToInt32(key.GetValue("ProxyEnable", 0)) == 1;
                var server = Convert.ToString(key.GetValue("ProxyServer"));
                var pac = Convert.ToString(key.GetValue("AutoConfigURL"));
                if (enabled && !string.IsNullOrWhiteSpace(server))
                {
                    hasExplicitConfiguration = true;
                    foreach (var parsed in ParseProxyValues(server, "Windows 系统代理", 5))
                        AddCandidate(candidates, parsed);
                    systemProxySummary = "已启用：" + SummarizeProxyValue(server);
                }

                if (!string.IsNullOrWhiteSpace(pac))
                {
                    hasExplicitConfiguration = true;
                    pacSummary = "已配置：" + SafeText.Redact(pac);
                }
            }
        }
        catch (Exception ex)
        {
            systemProxySummary = "读取失败：" + SafeText.Redact(ex.Message);
        }

        await AddSystemResolvedProxyCandidatesAsync(targets, candidates, cancellationToken);
        await AddWinHttpCandidatesAsync(candidates, cancellationToken);
        AddProxyProcessListenerCandidates(candidates);

        var tunnelAdapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
            .Where(adapter => adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel ||
                              TunnelAdapterRegex().IsMatch(adapter.Name + " " + adapter.Description))
            .Select(adapter => SafeText.Redact($"{adapter.Name} ({adapter.Description})"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProxyDiscoveryResult(
            candidates.Values.OrderBy(candidate => candidate.Priority).ThenBy(candidate => candidate.Display).ToList(),
            userEnvironment,
            systemProxySummary,
            pacSummary,
            tunnelAdapters,
            hasExplicitConfiguration);
    }

    internal static IReadOnlyList<ProxyCandidate> ParseProxyValues(string raw, string source, int priority)
    {
        var results = new List<ProxyCandidate>();
        if (string.IsNullOrWhiteSpace(raw))
            return results;

        var parts = raw.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            parts = [raw.Trim()];

        foreach (var part in parts)
        {
            var value = part;
            string? protocolHint = null;
            var equals = part.IndexOf('=');
            if (equals > 0 && !part[..equals].Contains("://", StringComparison.Ordinal))
            {
                protocolHint = part[..equals].Trim();
                value = part[(equals + 1)..].Trim();
            }

            if (TryParseProxy(value, source, priority, protocolHint, out var candidate))
                results.Add(candidate);
        }

        return results;
    }

    internal static bool TryParseProxy(
        string value,
        string source,
        int priority,
        string? protocolHint,
        out ProxyCandidate candidate)
    {
        candidate = null!;
        if (string.IsNullOrWhiteSpace(value) || value.Equals("direct", StringComparison.OrdinalIgnoreCase))
            return false;

        var normalized = value.Trim();
        if (!normalized.Contains("://", StringComparison.Ordinal))
        {
            var prefix = protocolHint?.StartsWith("socks", StringComparison.OrdinalIgnoreCase) == true
                ? "socks5://"
                : "http://";
            normalized = prefix + normalized;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host) || uri.Port is < 1 or > 65535)
            return false;

        var kind = uri.Scheme.StartsWith("socks", StringComparison.OrdinalIgnoreCase)
            ? ProxyKind.Socks5
            : ProxyKind.Http;
        if (kind == ProxyKind.Http &&
            !uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            return false;

        candidate = new ProxyCandidate(kind, uri.Host, uri.Port, source, priority, OriginalValue: normalized);
        return true;
    }

    private static void AddEnvironmentCandidate(
        string name,
        EnvironmentVariableTarget target,
        int priority,
        Dictionary<string, ProxyCandidate> candidates,
        ref bool hasExplicitConfiguration)
    {
        var value = Environment.GetEnvironmentVariable(name, target);
        if (string.IsNullOrWhiteSpace(value))
            return;

        hasExplicitConfiguration = true;
        var hint = name.Equals("ALL_PROXY", StringComparison.OrdinalIgnoreCase) ? "socks" : "http";
        if (TryParseProxy(value, $"{target} {name}", priority, hint, out var candidate))
            AddCandidate(candidates, candidate);
    }

    private static async Task AddSystemResolvedProxyCandidatesAsync(
        IReadOnlyList<TargetEndpoint> targets,
        Dictionary<string, ProxyCandidate> candidates,
        CancellationToken cancellationToken)
    {
        IWebProxy systemProxy;
        try
        {
            systemProxy = WebRequest.GetSystemWebProxy();
        }
        catch
        {
            return;
        }

        foreach (var target in targets.Take(5))
        {
            try
            {
                var destination = new Uri($"https://{target.Host}:{target.Port}/");
                var resolved = await Task.Run(() => systemProxy.GetProxy(destination), cancellationToken)
                    .WaitAsync(TimeSpan.FromSeconds(4), cancellationToken);
                if (resolved is null || resolved == destination)
                    continue;
                if (TryParseProxy(resolved.AbsoluteUri, $"系统代理/PAC 为 {target.Host} 解析", 7, null, out var candidate))
                    AddCandidate(candidates, candidate);
            }
            catch
            {
                // PAC resolution can legitimately time out while offline. Other discovery sources remain usable.
            }
        }
    }

    private static async Task AddWinHttpCandidatesAsync(
        Dictionary<string, ProxyCandidate> candidates,
        CancellationToken cancellationToken)
    {
        try
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var netsh = Path.Combine(systemRoot, "System32", "netsh.exe");
            var start = new ProcessStartInfo(netsh, "winhttp show proxy")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using var process = Process.Start(start);
            if (process is null)
                return;
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(4), cancellationToken);
            var output = await outputTask;
            foreach (Match match in HostPortRegex().Matches(output))
            {
                var value = $"{match.Groups[1].Value}:{match.Groups[2].Value}";
                if (TryParseProxy(value, "WinHTTP", 25, "http", out var candidate))
                    AddCandidate(candidates, candidate);
            }
        }
        catch
        {
            // WinHTTP is an optional signal only.
        }
    }

    private static void AddProxyProcessListenerCandidates(Dictionary<string, ProxyCandidate> candidates)
    {
        try
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var netstat = Path.Combine(systemRoot, "System32", "netstat.exe");
            var start = new ProcessStartInfo(netstat, "-ano -p tcp")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(start);
            if (process is null)
                return;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            var processNames = new Dictionary<int, string>();
            foreach (Match match in ListenerRegex().Matches(output))
            {
                if (!int.TryParse(match.Groups[3].Value, out var pid) ||
                    !int.TryParse(match.Groups[2].Value, out var port))
                    continue;

                if (!processNames.TryGetValue(pid, out var processName))
                {
                    try { processName = Process.GetProcessById(pid).ProcessName; }
                    catch { processName = string.Empty; }
                    processNames[pid] = processName;
                }
                if (string.IsNullOrWhiteSpace(processName) || !ProxyProcessRegex().IsMatch(processName))
                    continue;

                var rawAddress = match.Groups[1].Value.Trim('[', ']');
                if (!IPAddress.TryParse(rawAddress, out var address) || !NetworkHelpers.IsLoopbackOrAny(address))
                    continue;
                var host = address.Equals(IPAddress.IPv6Loopback) ? "::1" : "127.0.0.1";
                var owner = $"{processName} PID={pid}";
                AddCandidate(candidates, new ProxyCandidate(
                    ProxyKind.Http, host, port, "代理进程监听端口探测", 60, owner));
                AddCandidate(candidates, new ProxyCandidate(
                    ProxyKind.Socks5, host, port, "代理进程监听端口探测", 61, owner));
            }
        }
        catch
        {
            // Process listeners are best-effort discovery; configured proxies were already collected.
        }
    }

    private static void AddCandidate(Dictionary<string, ProxyCandidate> candidates, ProxyCandidate candidate)
    {
        if (!candidates.TryGetValue(candidate.Key, out var existing) || candidate.Priority < existing.Priority)
            candidates[candidate.Key] = candidate;
    }

    private static string SummarizeProxyValue(string value)
    {
        var parsed = ParseProxyValues(value, "summary", 0);
        return parsed.Count > 0
            ? string.Join("，", parsed.Select(candidate => candidate.Display).Distinct())
            : SafeText.Redact(value);
    }

    [GeneratedRegex(@"(?i)(?:^|\s|=)(\[?[0-9a-z_.:-]+\]?):(\d{2,5})(?:\s|$|;)")]
    private static partial Regex HostPortRegex();

    [GeneratedRegex(@"(?m)^\s*TCP\s+(\S+):(\d+)\s+\S+\s+LISTENING\s+(\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ListenerRegex();

    [GeneratedRegex("(?i)(lilisi|mihomo|clash|v2ray|xray|sing[-_ ]?box|nekoray|shadowsocks|sslocal|trojan|hysteria|tuic|surge|quantumult|proxifier|leaf|naive|brook|wireguard|openvpn|tailscale|zerotier|vpn)")]
    private static partial Regex ProxyProcessRegex();

    [GeneratedRegex("(?i)(vpn|wireguard|openvpn|tailscale|zerotier|wintun|tap|tun|clash|mihomo|v2ray|xray|sing-box)")]
    private static partial Regex TunnelAdapterRegex();
}
