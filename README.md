# TailscaleClient (Avalonia)

A cross-platform C# / Avalonia client for Tailscale that drives the locally-
installed `tailscaled` daemon via its LocalAPI. No re-implementation of the
WireGuard data plane — just a GUI shell that talks to the official daemon.

Runs on **Windows**, **macOS**, and **Linux** from a single net9.0 build.

```
┌──────────────────────────┐  HTTP over per-platform transport  ┌────────────────────┐
│  TailscaleClient.UI      │ ─────────────────────────────────▶ │  tailscaled        │
│  Avalonia 11, .NET 9     │  Win: named pipe + impersonation   │  (system service / │
│                          │  macOS: TCP + sameuserproof token  │   App Store app)   │
│                          │  Linux: Unix domain socket         │                    │
└──────────────────────────┘                                    └────────────────────┘
```

## Requirements

- .NET 9 SDK
- Official Tailscale installed and running:
  - **Windows** — `tailscaled.exe` Windows service
  - **macOS** — Tailscale.app from the App Store (preferred) or self-built `tailscaled`
  - **Linux** — `tailscaled` started via systemd (`sudo systemctl start tailscaled`)

## Project layout

| Project                                  | Purpose                                       |
|------------------------------------------|-----------------------------------------------|
| `src/TailscaleClient.Core`               | LocalAPI client, DTOs, IPN bus event stream   |
| `src/TailscaleClient.UI`                 | Avalonia UI, MVVM, tray, services             |
| `tests/TailscaleClient.Smoke`            | Console end-to-end probe against tailscaled   |

## Build & run

```bash
dotnet build TailscaleClient.slnx
dotnet run --project src/TailscaleClient.UI         # GUI
dotnet run --project tests/TailscaleClient.Smoke    # smoke
dotnet run --project tests/TailscaleClient.Smoke -- --watch        # stream IPN bus
dotnet run --project tests/TailscaleClient.Smoke -- --ping 100.x   # diagnose ping
```

### macOS build

```bash
dotnet publish src/TailscaleClient.UI -r osx-arm64 -c Release \
  -p:PublishSingleFile=true --self-contained true
```

Apple Silicon only — Intel Macs aren't built in CI. If you need an Intel build,
swap `osx-arm64` for `osx-x64` and build from source.

For a proper double-clickable `.app` bundle, wrap the published binary with
[`dotnet-bundle`](https://github.com/egramtel/dotnet-bundle) or hand-author
`Contents/Info.plist`.

### Linux build

```bash
dotnet publish src/TailscaleClient.UI -r linux-x64 -c Release
```

You'll likely need to be a member of the group that owns
`/var/run/tailscale/tailscaled.sock` (usually `tailscale`), or run the binary
with `sudo`.

## Features

- Connection state (Connected / Disconnected / NeedsLogin) with live IPN-bus updates
- Sign in / log out (opens auth URL in browser)
- Connect / disconnect (toggles `WantRunning` via `MaskedPrefs`)
- Tailnet device list with online state, IP, OS, last-seen, ping, copy IP
- Exit node picker + "Allow LAN access" toggle
- MagicDNS / Accept-routes / Shields-up / Tailscale-SSH toggles
- Advertise subnet routes
- Taildrop: list / save / delete received files, send to a peer (via `StorageProvider`)
- System-tray icon with state color + context menu, close-to-tray

## Key implementation notes

### Transport layer ([LocalApiHttpFactory.cs](src/TailscaleClient.Core/LocalApi/LocalApiHttpFactory.cs))

A single factory picks the right transport per OS:

| Platform | Path | Authentication |
|----------|------|----------------|
| Windows  | named pipe `\\.\pipe\ProtectedPrefix\Administrators\Tailscale\tailscaled` | `TokenImpersonationLevel.Impersonation` on the client pipe |
| macOS (App Store / sys-extension) | `/Library/Tailscale/sameuserproof-{port}-{token}` → `127.0.0.1:{port}` | HTTP Basic with empty user + token as password |
| macOS (standalone) | Unix socket `/var/run/tailscaled.socket` | OS-level peer creds |
| Linux | Unix socket `/var/run/tailscale/tailscaled.sock` | OS-level peer creds |

The non-obvious Windows detail: opening the pipe with the default `Anonymous`
impersonation level causes tailscaled's `ImpersonateNamedPipeClient` to fail
with `401 authentication failed: Unable to impersonate using a named pipe…`.
Use `TokenImpersonationLevel.Impersonation`. The pipe ACL itself is permissive —
no Administrator elevation needed.

### Background IPN bus

[`LocalApiClient.WatchIpnBusAsync`](src/TailscaleClient.Core/LocalApi/LocalApiClient.cs)
returns an `IAsyncEnumerable<IpnNotify>` reading line-delimited JSON from
`/localapi/v0/watch-ipn-bus`. `TailscaleService` runs this on a background task,
reconnects on failure, and raises `INotifyPropertyChanged` for the UI to bind to.

### MaskedPrefs

`PATCH /localapi/v0/prefs` requires a `MaskedPrefs` body with a `*Set` mask
bit beside every value. Strongly-typed factories in
[`MaskedPrefs.cs`](src/TailscaleClient.Core/Models/MaskedPrefs.cs) (`SetWantRunning`,
`SetExitNode`, …) prevent the easy mistake of changing a value without flipping
its mask.

## CI / releases

[`.github/workflows/build.yml`](.github/workflows/build.yml) runs on every
push and PR to `main` and on `v*` tags:

- **`windows-latest`** → `TailscaleClient-{ver}-win-x64.zip`
- **`macos-latest`** (Apple Silicon) → `TailscaleClient-{ver}-osx-arm64.zip` (a `.app` bundle)

All builds are self-contained single-file (no .NET runtime install needed on the
target machine). The macOS bundle is ad-hoc-signed so it launches without the
right-click-Open Gatekeeper dance, but it's not notarized — push it through
`codesign --force --deep --sign 'Developer ID Application: …'` and
`xcrun notarytool submit` for real distribution.

Cut a release with:

```bash
git tag v0.1.0 && git push origin v0.1.0
```

The workflow attaches both zips to a GitHub release tagged that version.

## Known limitations

- Single profile only — no multi-user `Switch profile` UI
- No Tailnet Lock / TKA UI
- No Taildrive `DriveShares` editor
- No traffic chart; we only display `Engine.RxBytes/TxBytes` aggregates
- macOS tray icon is a colored circle (no template-image variant for menu-bar dark/light)
