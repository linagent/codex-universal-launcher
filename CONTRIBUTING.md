# Contributing

Thanks for helping make the launcher work across more Windows network setups.

## Before opening an issue

- Reproduce with the latest release.
- Run `--check-only` first.
- Remove tokens, credentials, private provider hosts, usernames, real public IP addresses, and unrelated machine details.
- State the Windows version, CPU architecture, proxy/VPN product and version, operating mode (system proxy, TUN, PAC, etc.), and whether direct browser access works.

## Development

1. Install the .NET 8 SDK on Windows 10 or 11.
2. Run `dotnet restore` and `dotnet build -c Release`.
3. Run `dotnet run -c Release -- --self-test --result self-test.txt`.
4. For network behavior changes, also run `--check-only` and verify that no settings are modified.

Keep changes bounded. New discovery signals should be read-only. Any new write must be reversible, documented, scoped to the current user, and protected against secret leakage.

## Pull requests

- Explain the user-visible problem and the safety impact.
- Add or update offline self-tests where possible.
- Do not commit reports, logs, build outputs, real proxy ports, private host names, AppIDs from a personal machine, or generated backups.
- Keep UI text usable in both English-oriented documentation and the current Chinese interface.

By contributing, you agree that your contribution is licensed under the MIT License.
