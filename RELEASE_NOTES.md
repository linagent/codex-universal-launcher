# Codex启动器，不再5次重连 v2.0.1

This patch removes the manual “close ChatGPT/Codex first” step from normal launcher use.

Highlights:

- After the preflight passes, automatically requests a normal close of the running ChatGPT desktop process before relaunching ChatGPT/Codex.
- Waits up to eight seconds, then automatically terminates remaining background processes before relaunching.
- Adds `--keep-existing` for users who intentionally want to preserve the current process.
- Keeps `--check-only`, `--demo`, and `--self-test` completely free of process-closing behavior.
- Does not target generic `Codex.exe` processes, avoiding Codex CLI and unrelated tools.
- Continues to use product-agnostic proxy/VPN discovery, TLS validation, per-user environment backup, and dynamic app discovery.
- Adds recommended lightweight single-file downloads without removing launcher features.

Recommended lightweight downloads:

- `CodexUniversalLauncher-v2.0.1-lite-win-x64.zip` — most Windows PCs; 179.5 KB ZIP.
- `CodexUniversalLauncher-v2.0.1-lite-win-arm64.zip` — Windows on Arm; 169.6 KB ZIP.

The lightweight build requires the free [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0). If it is not installed and installing it is undesirable, use the larger dependency-free fallback:

- `CodexUniversalLauncher-v2.0.1-portable-win-x64.zip` — 63.34 MB ZIP.
- `CodexUniversalLauncher-v2.0.1-portable-win-arm64.zip` — 59.31 MB ZIP.

SHA-256:

```text
1A927A0F2EDF0FA946FCDD3003E3CEBE0B11A33541743E6C3070E9754619BBBC  CodexUniversalLauncher-v2.0.1-lite-win-x64.zip
D56B95B6FCE7C675EBA2657DDEFCC41937ADEEBA8BCC89155596F083B6B7FB91  CodexUniversalLauncher-v2.0.1-lite-win-arm64.zip
E07583B4548B0EB3E773C8C8BBBBF176898CC2FFC1F34330FE24AC392825BE8A  CodexUniversalLauncher-v2.0.1-portable-win-x64.zip
9BDEA2B8EA8C58138B9FB6C0454C03F9DF5A61E26E91A50FA5945824665B28A3  CodexUniversalLauncher-v2.0.1-portable-win-arm64.zip
```

All binaries are currently unsigned. Verify the SHA-256 value above, or build from source. The portable downloads are self-contained; the lightweight downloads use the installed .NET 8 Desktop Runtime.

This is an unofficial community project and cannot guarantee that all reconnects will be eliminated. Account, service, quota, provider, and non-network streaming failures remain outside its scope.
