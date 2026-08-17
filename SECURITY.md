# Security policy

## Supported versions

Security fixes are provided for the latest release only.

## Reporting a vulnerability

Please do not publish credentials, real proxy URLs, account identifiers, private provider hosts, full diagnostic reports, or screenshots containing personal data in a public issue.

For a suspected vulnerability, use GitHub's private vulnerability reporting feature for this repository. Include the affected version, a minimal reproduction, impact, and a redacted log if relevant. If private reporting is unavailable, open a public issue containing no exploit details or sensitive data and ask the maintainer for a private contact channel.

## Trust boundary

The launcher is a local diagnostic utility. It can inspect standard Windows proxy signals, active adapter metadata, local TCP listeners, the host portion of Codex `base_url`, and the installed Start menu AppID. In normal mode it may write current-user proxy environment variables after creating a local backup.

It does not require administrator privileges, install a service, change firewall/DNS/Winsock/routes, modify proxy/VPN applications, read API keys, read conversations, or upload telemetry.

Downloaded release binaries are currently unsigned. Verify the SHA-256 values in the release notes or build from source before running in a sensitive environment.
