# Changelog

All notable changes to this project are documented here.

## [2.0.1] - 2026-08-20

### Added

- Automatically closes an already-running ChatGPT/Codex instance after the network preflight passes and before launching a fresh instance.
- Requests a graceful close first, waits up to eight seconds, and then terminates any remaining background processes without requiring manual confirmation.
- Adds `--keep-existing` to preserve the current app process when an automatic restart is not desired.
- Adopts the Chinese product name `Codex启动器，不再5次重连` in the app and project documentation.

### Distribution

- Adds recommended framework-dependent single-file packages that reduce the ZIP download from roughly 60–70 MB to about 170–180 KB without removing launcher features.
- Keeps compressed self-contained portable packages as dependency-free fallbacks.
- Replaces the oversized executable icon payload with a reproducible compact 32/64/128-pixel icon bundle.

### Safety

- Read-only, demo, and offline self-test modes never close ChatGPT/Codex.
- Automatic shutdown happens only after route validation, environment handling, and installed-app discovery have succeeded.
- Process-name selection is covered by offline tests and is limited to `ChatGPT`, excluding Codex CLI and the launcher itself.

## [2.0.0] - 2026-08-17

### Added

- Product-agnostic Windows proxy and VPN/TUN discovery.
- Dynamic discovery of system proxy, PAC, WinHTTP, environment variables, and local proxy listeners.
- Direct, HTTP CONNECT, HTTPS-proxy, and SOCKS5 tunnel validation with target TLS handshake.
- Custom Codex provider host discovery from `base_url`.
- Dynamic Start menu discovery for the installed ChatGPT/Codex Windows app.
- Read-only, no-repair, silent, demo, and offline self-test modes.
- Redacted local reports, rotating logs, environment backup, and guarded restart prompts.
- Original icon family, redesigned WinForms interface, bilingual documentation, and Windows CI.

### Safety

- No administrator requirement or system-wide networking reset.
- Proxy URLs containing credentials are not copied into a repaired environment.
- Existing ChatGPT/Codex processes are never force-terminated without a second confirmation.
