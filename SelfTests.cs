using System.Text;

namespace CodexUniversalLauncher;

internal static class SelfTests
{
    public static int Run(string? resultPath)
    {
        var lines = new List<string>();
        var failures = 0;

        Check("parse-http", () =>
        {
            var parsed = ProxyDiscovery.ParseProxyValues("127.0.0.1:7890", "test", 1);
            return parsed.Count == 1 && parsed[0].Kind == ProxyKind.Http && parsed[0].Port == 7890;
        });
        Check("parse-protocol-map", () =>
        {
            var parsed = ProxyDiscovery.ParseProxyValues(
                "http=127.0.0.1:7890;https=127.0.0.1:7891;socks=127.0.0.1:7892", "test", 1);
            return parsed.Count == 3 && parsed.Any(item => item.Kind == ProxyKind.Socks5 && item.Port == 7892);
        });
        Check("parse-socks", () =>
        {
            return ProxyDiscovery.TryParseProxy("socks5://localhost:1080", "test", 1, null, out var candidate) &&
                   candidate.Kind == ProxyKind.Socks5 && candidate.Port == 1080;
        });
        Check("merge-no-proxy", () =>
        {
            var merged = EnvironmentRepair.MergeNoProxy("example.local,localhost");
            return merged.Contains("127.0.0.1") && merged.Contains("::1") &&
                   merged.Split(',').Count(value => value.Equals("localhost", StringComparison.OrdinalIgnoreCase)) == 1;
        });
        Check("redaction", () =>
        {
            var redacted = SafeText.Redact("http://alice:secret@example.com Bearer abc.def sk-abcdefghijk");
            return !redacted.Contains("secret") && !redacted.Contains("abc.def") &&
                   !redacted.Contains("sk-abcdefghijk");
        });
        Check("start-apps-array", () =>
        {
            var apps = CodexAppLocator.ParseStartApps(
                "[{\"Name\":\"ChatGPT\",\"AppID\":\"OpenAI.Codex_x!App\"}]");
            return apps.Count == 1 && apps[0].Name == "ChatGPT";
        });
        Check("start-apps-single", () =>
        {
            var apps = CodexAppLocator.ParseStartApps(
                "{\"Name\":\"Codex\",\"AppID\":\"OpenAI.Codex_x!App\"}");
            return apps.Count == 1 && apps[0].AppId.Contains('!');
        });

        var output = new[] { $"EXIT_CODE={(failures == 0 ? 0 : 1)}" }.Concat(lines).ToList();
        if (!string.IsNullOrWhiteSpace(resultPath))
        {
            var fullPath = Path.GetFullPath(resultPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllLines(fullPath, output, new UTF8Encoding(true));
        }
        return failures == 0 ? 0 : 1;

        void Check(string name, Func<bool> test)
        {
            try
            {
                var passed = test();
                lines.Add($"{name}={(passed ? "PASS" : "FAIL")}");
                if (!passed) failures++;
            }
            catch (Exception ex)
            {
                lines.Add($"{name}=FAIL {SafeText.Redact(ex.Message)}");
                failures++;
            }
        }
    }
}
