using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexUniversalLauncher;

internal static partial class SafeText
{
    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var result = BearerRegex().Replace(value, "$1<REDACTED>");
        result = ApiKeyRegex().Replace(result, "$1...<REDACTED>");
        result = NamedSecretRegex().Replace(result, "$1$2<REDACTED>");
        result = UrlCredentialRegex().Replace(result, "://<REDACTED>@");
        return result;
    }

    public static string ShortHash(string? value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes)[..12];
    }

    [GeneratedRegex("(?i)(Bearer\\s+)[A-Za-z0-9._~+\\-/=]+")]
    private static partial Regex BearerRegex();

    [GeneratedRegex("(?i)(sk-[A-Za-z0-9_-]{6})[A-Za-z0-9_-]+")]
    private static partial Regex ApiKeyRegex();

    [GeneratedRegex("(?i)(api[_-]?key|token|secret|password)(\\s*[:=]\\s*)[^\\s,;]+")]
    private static partial Regex NamedSecretRegex();

    [GeneratedRegex("://([^/@:\\s]+):([^/@\\s]+)@")]
    private static partial Regex UrlCredentialRegex();
}
