using System.Net;

namespace CodexUniversalLauncher;

internal enum ProxyKind
{
    Http,
    Socks5
}

internal enum RouteKind
{
    Direct,
    HttpProxy,
    Socks5Proxy,
    Unavailable
}

internal sealed record LauncherOptions(
    bool CheckOnly,
    bool Silent,
    bool NoRepair,
    bool SelfTest,
    bool Demo,
    string? ResultPath)
{
    public static LauncherOptions Parse(string[] args)
    {
        string? result = null;
        var resultIndex = Array.FindIndex(args, value =>
            value.Equals("--result", StringComparison.OrdinalIgnoreCase));
        if (resultIndex >= 0 && resultIndex + 1 < args.Length)
            result = args[resultIndex + 1];

        return new LauncherOptions(
            args.Contains("--check-only", StringComparer.OrdinalIgnoreCase),
            args.Contains("--silent", StringComparer.OrdinalIgnoreCase),
            args.Contains("--no-repair", StringComparer.OrdinalIgnoreCase),
            args.Contains("--self-test", StringComparer.OrdinalIgnoreCase),
            args.Contains("--demo", StringComparer.OrdinalIgnoreCase),
            result);
    }
}

internal sealed record TargetEndpoint(string Host, int Port, string Reason, bool Required = true)
{
    public string Display => Port == 443 ? Host : $"{Host}:{Port}";
}

internal sealed record ProxyCandidate(
    ProxyKind Kind,
    string Host,
    int Port,
    string Source,
    int Priority,
    string? Owner = null,
    string? OriginalValue = null)
{
    public string Key => $"{Kind}|{Host.ToLowerInvariant()}|{Port}";
    public string Display => $"{(Kind == ProxyKind.Http ? "http" : "socks5")}://{Host}:{Port}";
    public bool HasCredentials => !string.IsNullOrWhiteSpace(OriginalValue) &&
                                  Uri.TryCreate(OriginalValue, UriKind.Absolute, out var uri) &&
                                  !string.IsNullOrWhiteSpace(uri.UserInfo);

    public string ValueForEnvironment => OriginalValue is not null &&
                                         Uri.TryCreate(OriginalValue, UriKind.Absolute, out _)
        ? OriginalValue
        : Display;
}

internal sealed record ProxyDiscoveryResult(
    IReadOnlyList<ProxyCandidate> Candidates,
    IReadOnlyDictionary<string, string?> UserEnvironment,
    string SystemProxySummary,
    string PacSummary,
    IReadOnlyList<string> ActiveTunnelAdapters,
    bool HasExplicitProxyConfiguration);

internal sealed record RouteProbe(
    string Route,
    bool Success,
    bool TlsValidated,
    string Detail,
    TimeSpan Elapsed);

internal sealed record ConnectivityDecision(
    RouteKind Kind,
    ProxyCandidate? Proxy,
    IReadOnlyList<RouteProbe> Probes,
    string Summary,
    bool DirectSucceeded,
    bool ExplicitProxyFailed)
{
    public bool IsUsable => Kind != RouteKind.Unavailable;
}

internal sealed record EnvironmentChange(string Name, string? Before, string? After);

internal sealed record EnvironmentRepairResult(
    IReadOnlyList<EnvironmentChange> Changes,
    string Summary,
    string? BackupPath)
{
    public bool Changed => Changes.Count > 0;
}

internal sealed record InstalledCodexApp(string Name, string AppId);

internal static class NetworkHelpers
{
    public static bool IsLoopbackOrAny(IPAddress address) =>
        IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);
}
