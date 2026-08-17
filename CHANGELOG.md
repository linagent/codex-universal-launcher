# Changelog

All notable changes to this project are documented here.

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
