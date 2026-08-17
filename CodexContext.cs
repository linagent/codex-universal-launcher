using System.Text.RegularExpressions;

namespace CodexUniversalLauncher;

internal sealed record CodexContext(
    string ConfigPath,
    IReadOnlyList<TargetEndpoint> Targets,
    IReadOnlyList<string> CustomProviderHosts);

internal static partial class CodexContextInspector
{
    public static CodexContext Inspect()
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME") ??
                        Environment.GetEnvironmentVariable("CODEX_HOME", EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(codexHome))
        {
            codexHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        }

        var configPath = Path.Combine(codexHome, "config.toml");
        var customHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(configPath))
        {
            foreach (var line in File.ReadLines(configPath))
            {
                var match = BaseUrlRegex().Match(line);
                if (!match.Success)
                    continue;

                var raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
                    customHosts.Add(uri.Host);
            }
        }

        var targets = new List<TargetEndpoint>();
        targets.AddRange(customHosts.Select(host => new TargetEndpoint(host, 443, "自定义 provider")));
        targets.Add(new TargetEndpoint("chatgpt.com", 443, "Codex/ChatGPT 官方服务"));
        targets.Add(new TargetEndpoint(
            "api.openai.com", 443, "Codex/ChatGPT 官方服务",
            Required: customHosts.Count == 0));
        targets.Add(new TargetEndpoint("auth.openai.com", 443, "Codex/ChatGPT 官方服务"));

        return new CodexContext(
            configPath,
            targets.DistinctBy(target => $"{target.Host}:{target.Port}", StringComparer.OrdinalIgnoreCase).ToList(),
            customHosts.OrderBy(host => host, StringComparer.OrdinalIgnoreCase).ToList());
    }

    [GeneratedRegex("^\\s*base_url\\s*=\\s*(?:\"([^\"]+)\"|'([^']+)')", RegexOptions.IgnoreCase)]
    private static partial Regex BaseUrlRegex();
}
