<div align="center">
  <img src="assets/brand/icon-primary-256.png" width="128" alt="Codex Universal Launcher icon">
  <h1>Codex Universal Launcher</h1>
  <p>Discover the Windows network path that actually works, validate TLS, and then launch ChatGPT/Codex.</p>

  [简体中文](README.zh-CN.md) · [Download](https://github.com/linagent/codex-universal-launcher/releases/latest) · [Report a bug](https://github.com/linagent/codex-universal-launcher/issues/new?template=bug_report.yml)

  [![Build](https://github.com/linagent/codex-universal-launcher/actions/workflows/build.yml/badge.svg)](https://github.com/linagent/codex-universal-launcher/actions/workflows/build.yml)
  [![Release](https://img.shields.io/github/v/release/linagent/codex-universal-launcher)](https://github.com/linagent/codex-universal-launcher/releases)
  [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
</div>

> [!IMPORTANT]
> This is an unofficial community project. It is not affiliated with, sponsored by, or endorsed by OpenAI. It can reduce startup failures caused by stale or uninherited proxy settings, but it cannot guarantee zero reconnects: account, service, provider, quota, and streaming-runtime failures are outside its control.

## Why this exists

Windows users often have more than one network signal at the same time: a system proxy, PAC, WinHTTP proxy, environment variables, a local HTTP/SOCKS listener, or a VPN/TUN adapter. A desktop app may inherit a stale value even while the browser works.

Codex Universal Launcher performs a bounded preflight before launch:

1. Reads only the target host names needed for the installed ChatGPT/Codex app and any custom `base_url` host in Codex configuration.
2. Discovers configured HTTP/SOCKS routes and active VPN/TUN signals without assuming a specific proxy product or port.
3. Tests direct/TUN and candidate proxy routes with a real TCP tunnel and TLS handshake.
4. If necessary, backs up and adjusts only the current user's proxy environment variables.
5. Locates the installed Windows app dynamically and launches it only after the preflight passes.

It does **not** install, configure, restart, or control your proxy/VPN software. It does not reset DNS, Winsock, routes, firewall rules, or Codex data.

## Screenshots

| Preflight | Validated route |
| --- | --- |
| ![Preflight in progress](assets/screenshots/preflight-in-progress.png) | ![Validated route](assets/screenshots/preflight-success.png) |

The screenshots use `--demo`; they contain representative data and do not expose a real proxy port, AppID, account, or local path.

## Quick start

1. Download the correct ZIP from [Releases](https://github.com/linagent/codex-universal-launcher/releases): `win-x64` for most Windows PCs, or `win-arm64` for Windows on Arm.
2. Extract the ZIP to a normal folder. Optionally compare its SHA-256 value with the release notes.
3. Run `CodexUniversalLauncher.exe`. Keep your proxy/VPN running; the launcher will test available routes and open ChatGPT/Codex after validation.

The binaries are self-contained and do not require a separate .NET installation. They are currently unsigned, so Windows SmartScreen may show an unknown-publisher warning. If that is not acceptable, build from source.

## Modes

```text
CodexUniversalLauncher.exe --check-only
CodexUniversalLauncher.exe --no-repair
CodexUniversalLauncher.exe --silent --result result.txt
CodexUniversalLauncher.exe --demo
CodexUniversalLauncher.exe --self-test --result self-test.txt
```

| Option | Behavior |
| --- | --- |
| `--check-only` | Read-only diagnosis. Does not write environment variables and does not launch the app. |
| `--no-repair` | Tests the routes and launches the app, but only previews environment-variable changes. |
| `--silent` | Runs without a visible window and writes the requested result file. |
| `--result <path>` | Writes a small machine-readable result summary. |
| `--demo` | Displays sanitized representative results; does not inspect or modify the network. |
| `--self-test` | Runs offline parser, redaction, and safety-boundary tests. |

For the safest first run, start with `--check-only`, inspect the local report, and then run normally.

## What it can discover

- Windows system proxy and PAC-resolved routes
- User, process, and machine `HTTP_PROXY`, `HTTPS_PROXY`, and `ALL_PROXY`
- WinHTTP proxy configuration
- Local listeners owned by common proxy/VPN engines, with the port discovered at runtime
- HTTP, HTTPS-proxy CONNECT, and SOCKS5 tunnels
- Direct, transparent VPN, and TUN paths
- Custom Codex provider host names from `base_url` in `config.toml`
- Installed ChatGPT/Codex Start menu AppID, without hard-coding a package identifier

Discovery is intentionally best-effort. Authenticated enterprise PAC scripts, products that expose no standard Windows signal, non-SOCKS/HTTP proprietary tunnels, or policies that block TLS probing may require manual configuration.

## Changes and rollback

Normal launch mode may change only these **per-user** variables when a tested route requires it:

- `HTTP_PROXY`
- `HTTPS_PROXY`
- `ALL_PROXY`
- `NO_PROXY`

Before writing, the launcher saves a JSON backup under `%LOCALAPPDATA%\CodexUniversalLauncher\backups`. It refuses to copy proxy URLs containing embedded credentials into a new environment configuration. If ChatGPT/Codex is already running after a repair, the launcher asks before closing it and asks again before any forced termination.

No registry proxy settings, VPN settings, DNS, Winsock, firewall rules, credentials, cookies, conversations, or project files are modified.

## Privacy

- No telemetry, analytics, advertising, or remote log upload.
- Reports and rotating logs stay under `%LOCALAPPDATA%\CodexUniversalLauncher`.
- Reports contain route diagnostics and local listener metadata, so review them before sharing.
- Secret-like values, URL credentials, bearer tokens, and API-key patterns are redacted.
- Codex configuration is inspected only for `base_url` host names; API keys and conversation content are not read.

See [SECURITY.md](SECURITY.md) for reporting vulnerabilities and the trust boundary.

## Build from source

Requirements: Windows 10/11 and the .NET 8 SDK.

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release -- --self-test --result self-test.txt
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The project has no third-party NuGet dependencies. CI builds and runs the offline self-tests on Windows.

## Project status

Version 2.0.0 is the first public release. See [ROADMAP.md](ROADMAP.md) for planned work and [CHANGELOG.md](CHANGELOG.md) for shipped changes.

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request. Small compatibility reports for a specific proxy/VPN product are particularly useful, provided secrets and real account data are removed.

## Trademark notice

“OpenAI”, “ChatGPT”, and “Codex” are trademarks of their respective owner. This project's original icon is used only to identify this independent compatibility utility and does not reproduce the OpenAI logo. For official Codex information, use the [official OpenAI developer documentation](https://developers.openai.com/).

## License

[MIT](LICENSE) © 2026 linagent
