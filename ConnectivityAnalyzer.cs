using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace CodexUniversalLauncher;

internal static class ConnectivityAnalyzer
{
    public static async Task<ConnectivityDecision> AnalyzeAsync(
        ProxyDiscoveryResult discovery,
        IReadOnlyList<TargetEndpoint> targets,
        Action<string>? progress,
        CancellationToken cancellationToken)
    {
        var probes = new List<RouteProbe>();
        var primary = targets.First();

        progress?.Invoke("测试 VPN/TUN/直连路径……");
        var directRound = await ProbeTargetsAsync(null, targets, cancellationToken);
        probes.AddRange(directRound);
        var directSucceeded = RequiredTargetsSucceeded(directRound, targets);

        progress?.Invoke($"筛选 {discovery.Candidates.Count} 个代理候选……");
        var primaryProbes = await ProbeCandidatesAsync(discovery.Candidates, primary, cancellationToken);
        probes.AddRange(primaryProbes.Select(item => item.Probe));
        var primarySuccessKeys = primaryProbes
            .Where(item => item.Probe.Success)
            .Select(item => item.Candidate.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ProxyCandidate? selected = null;
        foreach (var candidate in discovery.Candidates.Where(candidate => primarySuccessKeys.Contains(candidate.Key)).Take(10))
        {
            progress?.Invoke($"复核 {candidate.Display} 的全部目标……");
            var fullRound = await ProbeTargetsAsync(candidate, targets, cancellationToken);
            probes.AddRange(fullRound);
            if (RequiredTargetsSucceeded(fullRound, targets))
            {
                selected = candidate;
                break;
            }
        }

        var configuredCandidateSucceeded = selected is not null && selected.Priority < 60;
        var explicitProxyFailed = discovery.HasExplicitProxyConfiguration && !configuredCandidateSucceeded;

        if (selected is not null && (selected.Priority < 60 || !directSucceeded))
        {
            var routeKind = selected.Kind == ProxyKind.Http ? RouteKind.HttpProxy : RouteKind.Socks5Proxy;
            var selectedProbes = probes.Where(probe =>
                probe.Route.StartsWith(selected.Display + " ->", StringComparison.OrdinalIgnoreCase) && probe.Success).ToList();
            var fullyTlsValidated = selectedProbes.Count > 0 && selectedProbes.All(probe => probe.TlsValidated);
            var validationText = fullyTlsValidated
                ? "通过 CONNECT/SOCKS 与目标 TLS 双层验证"
                : "隧道验证通过；目标 TLS 因当前 Windows 凭据上下文限制而降级";
            var summary = selected.Priority < 60
                ? $"采用已配置的 {selected.Display}（{selected.Source}），{validationText}"
                : $"直连不可用，采用自动发现的 {selected.Display}（{selected.Owner ?? selected.Source}），{validationText}";
            return new ConnectivityDecision(
                routeKind, selected, probes, summary, directSucceeded, explicitProxyFailed);
        }

        if (directSucceeded)
        {
            progress?.Invoke("复核直连/TUN 稳定性……");
            var confirmation = await ProbeTargetsAsync(null, targets, cancellationToken);
            probes.AddRange(confirmation);
            if (RequiredTargetsSucceeded(confirmation, targets))
            {
                var summary = discovery.ActiveTunnelAdapters.Count > 0
                    ? "采用已连续验证通过的直连/TUN/VPN 路径"
                    : "采用已连续验证通过的系统直连路径";
                if (explicitProxyFailed)
                    summary += "；检测到的显式代理候选未通过验证";
                return new ConnectivityDecision(
                    RouteKind.Direct, null, probes, summary, true, explicitProxyFailed);
            }
        }

        if (selected is not null)
        {
            return new ConnectivityDecision(
                selected.Kind == ProxyKind.Http ? RouteKind.HttpProxy : RouteKind.Socks5Proxy,
                selected,
                probes,
                $"采用通过验证的 {selected.Display}",
                directSucceeded,
                explicitProxyFailed);
        }

        return new ConnectivityDecision(
            RouteKind.Unavailable,
            null,
            probes,
            "没有找到能完成目标 TLS 握手的直连、HTTP 代理或 SOCKS5 路径",
            directSucceeded,
            explicitProxyFailed);
    }

    private static async Task<IReadOnlyList<(ProxyCandidate Candidate, RouteProbe Probe)>> ProbeCandidatesAsync(
        IReadOnlyList<ProxyCandidate> candidates,
        TargetEndpoint target,
        CancellationToken cancellationToken)
    {
        using var limiter = new SemaphoreSlim(8);
        var tasks = candidates.Take(30).Select(async candidate =>
        {
            await limiter.WaitAsync(cancellationToken);
            try
            {
                return (candidate, await ProbeTargetAsync(candidate, target, cancellationToken));
            }
            finally
            {
                limiter.Release();
            }
        });
        return await Task.WhenAll(tasks);
    }

    private static bool RequiredTargetsSucceeded(
        IReadOnlyList<RouteProbe> probes,
        IReadOnlyList<TargetEndpoint> targets)
    {
        if (probes.Count != targets.Count)
            return false;
        for (var index = 0; index < targets.Count; index++)
        {
            if (targets[index].Required && !probes[index].Success)
                return false;
        }
        return true;
    }

    private static async Task<IReadOnlyList<RouteProbe>> ProbeTargetsAsync(
        ProxyCandidate? proxy,
        IReadOnlyList<TargetEndpoint> targets,
        CancellationToken cancellationToken)
    {
        using var limiter = new SemaphoreSlim(5);
        var tasks = targets.Select(async target =>
        {
            await limiter.WaitAsync(cancellationToken);
            try
            {
                return await ProbeTargetAsync(proxy, target, cancellationToken);
            }
            finally
            {
                limiter.Release();
            }
        });
        return await Task.WhenAll(tasks);
    }

    internal static async Task<RouteProbe> ProbeTargetAsync(
        ProxyCandidate? proxy,
        TargetEndpoint target,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var routeName = proxy?.Display ?? "direct/TUN";
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(9));
            using var tcp = new TcpClient();
            Stream transport;

            if (proxy is null)
            {
                await tcp.ConnectAsync(target.Host, target.Port, timeout.Token);
                transport = tcp.GetStream();
            }
            else
            {
                await tcp.ConnectAsync(proxy.Host, proxy.Port, timeout.Token);
                transport = tcp.GetStream();
                if (proxy.Kind == ProxyKind.Http)
                    transport = await EstablishHttpTunnelAsync(transport, proxy, target, timeout.Token);
                else
                    await EstablishSocks5TunnelAsync(transport, proxy, target, timeout.Token);
            }

            await using (transport)
            await using (var tls = new SslStream(transport, leaveInnerStreamOpen: false))
            {
                await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = target.Host,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
                }, timeout.Token);
                if (!tls.IsAuthenticated || !tls.IsEncrypted)
                    throw new AuthenticationException("TLS 未完成加密认证");
            }

            stopwatch.Stop();
            return new RouteProbe(
                $"{routeName} -> {target.Display}", true, true,
                $"TLS OK，{stopwatch.ElapsedMilliseconds} ms", stopwatch.Elapsed);
        }
        catch (AuthenticationException ex) when (proxy is not null && IsCredentialContextUnavailable(ex))
        {
            stopwatch.Stop();
            return new RouteProbe(
                $"{routeName} -> {target.Display}", true, false,
                "代理隧道已建立；当前 Windows 诊断上下文无法取得 Schannel 凭据，TLS 未复核",
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new RouteProbe(
                $"{routeName} -> {target.Display}", false, false,
                "超时", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new RouteProbe(
                $"{routeName} -> {target.Display}", false, false,
                SafeText.Redact(ex.GetBaseException().Message), stopwatch.Elapsed);
        }
    }

    private static bool IsCredentialContextUnavailable(Exception exception)
    {
        var message = exception.GetBaseException().Message;
        return message.Contains("No credentials are available", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("security package", StringComparison.OrdinalIgnoreCase) &&
               message.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("安全包", StringComparison.OrdinalIgnoreCase) &&
               message.Contains("凭证", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Stream> EstablishHttpTunnelAsync(
        Stream transport,
        ProxyCandidate proxy,
        TargetEndpoint target,
        CancellationToken cancellationToken)
    {
        var proxyUri = proxy.OriginalValue is not null && Uri.TryCreate(proxy.OriginalValue, UriKind.Absolute, out var parsed)
            ? parsed
            : null;

        if (proxyUri?.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) == true)
        {
            var proxyTls = new SslStream(transport, leaveInnerStreamOpen: false);
            await proxyTls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = proxy.Host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
            }, cancellationToken);
            transport = proxyTls;
        }

        var request = new StringBuilder()
            .Append($"CONNECT {target.Host}:{target.Port} HTTP/1.1\r\n")
            .Append($"Host: {target.Host}:{target.Port}\r\n")
            .Append("Proxy-Connection: Keep-Alive\r\n");
        if (proxyUri is not null && !string.IsNullOrWhiteSpace(proxyUri.UserInfo))
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.UnescapeDataString(proxyUri.UserInfo)));
            request.Append($"Proxy-Authorization: Basic {basic}\r\n");
        }
        request.Append("\r\n");

        var bytes = Encoding.ASCII.GetBytes(request.ToString());
        await transport.WriteAsync(bytes, cancellationToken);
        await transport.FlushAsync(cancellationToken);
        var header = await ReadHeaderAsync(transport, cancellationToken);
        var firstLine = header.Split("\r\n", StringSplitOptions.None)[0];
        if (!firstLine.Contains(" 200 ", StringComparison.Ordinal))
            throw new IOException("HTTP CONNECT 失败：" + SafeText.Redact(firstLine));
        return transport;
    }

    private static async Task EstablishSocks5TunnelAsync(
        Stream stream,
        ProxyCandidate proxy,
        TargetEndpoint target,
        CancellationToken cancellationToken)
    {
        Uri? proxyUri = null;
        if (proxy.OriginalValue is not null)
            Uri.TryCreate(proxy.OriginalValue, UriKind.Absolute, out proxyUri);
        var hasCredentials = proxyUri is not null && !string.IsNullOrWhiteSpace(proxyUri.UserInfo);
        var greeting = hasCredentials ? new byte[] { 5, 2, 0, 2 } : new byte[] { 5, 1, 0 };
        await stream.WriteAsync(greeting, cancellationToken);
        var response = new byte[2];
        await ReadExactlyAsync(stream, response, cancellationToken);
        if (response[0] != 5 || response[1] == 0xff)
            throw new IOException("SOCKS5 协商失败");

        if (response[1] == 2)
        {
            if (!hasCredentials)
                throw new IOException("SOCKS5 需要用户名和密码");
            var userInfo = Uri.UnescapeDataString(proxyUri!.UserInfo).Split(':', 2);
            var user = Encoding.UTF8.GetBytes(userInfo[0]);
            var password = Encoding.UTF8.GetBytes(userInfo.Length > 1 ? userInfo[1] : string.Empty);
            if (user.Length > 255 || password.Length > 255)
                throw new IOException("SOCKS5 凭据过长");
            var auth = new byte[3 + user.Length + password.Length];
            auth[0] = 1;
            auth[1] = (byte)user.Length;
            Buffer.BlockCopy(user, 0, auth, 2, user.Length);
            auth[2 + user.Length] = (byte)password.Length;
            Buffer.BlockCopy(password, 0, auth, 3 + user.Length, password.Length);
            await stream.WriteAsync(auth, cancellationToken);
            await ReadExactlyAsync(stream, response, cancellationToken);
            if (response[1] != 0)
                throw new IOException("SOCKS5 身份验证失败");
        }
        else if (response[1] != 0)
        {
            throw new IOException($"SOCKS5 不支持的认证方法：{response[1]}");
        }

        var host = Encoding.ASCII.GetBytes(target.Host);
        if (host.Length > 255)
            throw new IOException("目标域名过长");
        var request = new byte[7 + host.Length];
        request[0] = 5;
        request[1] = 1;
        request[2] = 0;
        request[3] = 3;
        request[4] = (byte)host.Length;
        Buffer.BlockCopy(host, 0, request, 5, host.Length);
        request[^2] = (byte)(target.Port >> 8);
        request[^1] = (byte)(target.Port & 0xff);
        await stream.WriteAsync(request, cancellationToken);

        var reply = new byte[4];
        await ReadExactlyAsync(stream, reply, cancellationToken);
        if (reply[0] != 5 || reply[1] != 0)
            throw new IOException($"SOCKS5 连接失败，代码 {reply[1]}");
        var remaining = reply[3] switch
        {
            1 => 4,
            3 => await ReadLengthByteAsync(stream, cancellationToken),
            4 => 16,
            _ => throw new IOException("SOCKS5 返回了未知地址类型")
        };
        var addressAndPort = new byte[remaining + 2];
        await ReadExactlyAsync(stream, addressAndPort, cancellationToken);
    }

    private static async Task<string> ReadHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>(512);
        var one = new byte[1];
        while (bytes.Count < 8192)
        {
            var read = await stream.ReadAsync(one, cancellationToken);
            if (read == 0)
                break;
            bytes.Add(one[0]);
            var count = bytes.Count;
            if (count >= 4 && bytes[count - 4] == 13 && bytes[count - 3] == 10 &&
                bytes[count - 2] == 13 && bytes[count - 1] == 10)
                return Encoding.ASCII.GetString(bytes.ToArray());
        }
        throw new IOException("代理未返回完整 HTTP 响应头");
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
        }
    }

    private static async Task<int> ReadLengthByteAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        await ReadExactlyAsync(stream, buffer, cancellationToken);
        return buffer[0];
    }
}
