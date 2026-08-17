# Codex Universal Launcher 2.0.0

First public release of the universal Windows network preflight launcher.

Highlights:

- Detects standard system, PAC, WinHTTP, environment, local-listener, VPN, and TUN signals.
- Validates direct, HTTP CONNECT, and SOCKS5 paths with target TLS.
- Supports custom Codex provider host names without reading API keys.
- Backs up and changes only current-user proxy environment variables when needed.
- Dynamically locates the installed ChatGPT/Codex Windows app.
- Includes read-only, no-repair, silent, demo, and offline self-test modes.

Download:

- `CodexUniversalLauncher-v2.0.0-win-x64.zip` — most Windows PCs.
- `CodexUniversalLauncher-v2.0.0-win-arm64.zip` — Windows on Arm.

SHA-256:

```text
FB964DC4FDC02325526591D508CE3CE6482278F3524463E81CDBF7CE7650757B  CodexUniversalLauncher-v2.0.0-win-x64.zip
5EFAB0501F98360FF6BF5B7AF2AF7D9CCA3CD4FCC3DCB25C9D5173C60610E5B3  CodexUniversalLauncher-v2.0.0-win-arm64.zip
```

The binaries are self-contained and currently unsigned. Verify the SHA-256 value above, or build from source.

This is an unofficial community project and cannot guarantee that all reconnects will be eliminated. Account, service, quota, provider, and non-network streaming failures remain outside its scope.
