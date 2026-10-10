# BlueH — Implementation Plan

Last updated: 2026-10-10

## 1. Goal

A single Windows 11 GUI for everything Windows knows about connected Bluetooth headphones:
status, battery, volume and default-endpoint switching, media transport, pairing, and — per
vendor — device features such as active noise cancelling and equaliser presets. It exists
because Windows exposes almost none of this where a user can act on it.

## 2. Current state

- WPF app on .NET 10 (`net10.0-windows10.0.26100.0`), SDK-style project, builds clean with
  `TreatWarningsAsErrors`.
- Fluent dark theme enabled (`ThemeMode="Dark"` in `App.xaml`).
- `ViewModels/ViewModelBase.cs` is the only non-template code.
- `MainWindow` is an empty `Grid`. No feature code, no test project, no dependency injection.
- Conventions, critical invariants, and WPF rules are recorded in `CLAUDE.md`.

## 3. Data: everything is read live, almost nothing is stored

**No database.** This is the single most important scoping decision and it falls out of what
the app actually does: Windows is the source of truth for every value the UI shows, and
re-reading it is both cheap and correct. Storing it would mean maintaining a cache that can
only ever be wrong.

Read live from the platform, never persisted:

| Data | Source |
|---|---|
| Paired and connected devices | `DeviceInformation` / `DeviceWatcher` |
| Connection state | `DeviceWatcher` events |
| Battery charge | GATT Battery Service, or the Windows device property |
| Volume, mute, default endpoint | Core Audio |
| Now-playing track, transport state | `Windows.Media.Control` |

Persisted, because Windows does not remember it for us — this is **user intent**, not device
state:

| Data | Size |
|---|---|
| App preferences (language, window geometry, selected device) | a few fields |
| Per-device nickname | one string per device |
| Per-device desired ANC / equaliser preset, re-applied on connect | a few fields per device |

That is a handful of keys. It belongs in **one JSON file** under
`%LOCALAPPDATA%\BlueH\settings.json`, behind an `ISettingsStore` interface — the equivalent of
a properties file, not a database.

### When a database would become justified

Exactly one planned feature is database-shaped: **battery history**, to chart charge over days
or weeks. If it is built, use **SQLite** via `Microsoft.Data.Sqlite` (or EF Core, which is the
Spring Data equivalent, if the query surface grows). SQLite is an embedded single file — no
server, no installer — which keeps the self-contained single-executable publish intact. A
server database (SQL Server, Postgres) is never appropriate for a desktop utility: requiring a
database server to see a headset's battery level would be absurd.

Note for whenever that arrives: SQLite has no native date type. Store timestamps as UTC TEXT in
the project's canonical `yyyy-MM-dd HH:mm:ss` format, which sorts lexicographically in
chronological order — this is a reason the canonical format earns its keep, not an accident.

No credentials, no personal data, and no network calls are involved anywhere in the app.

## 4. Architecture

```
BlueH/
  Models/          domain types, no UI and no WinRT
  Services/        interfaces + their platform implementations
  ViewModels/      one per view, derived from ViewModelBase
  Views/           XAML windows and user controls
  Converters/      IValueConverter implementations
  Properties/      Resources.resx — all user-facing text
BlueH.Tests/       NUnit, no hardware required
```

### The one structural rule

**No ViewModel touches WinRT, COM, or the file system directly.** Every platform call sits
behind a project-owned interface returning plain domain models:

```
IBluetoothDeviceService   -> WinRtBluetoothDeviceService
IBatteryService           -> GattBatteryService, DevicePropertyBatteryService
IAudioEndpointService     -> CoreAudioEndpointService
IMediaTransportService    -> WindowsMediaTransportService
ISettingsStore            -> JsonSettingsStore
```

This boundary is what makes the whole app testable with no headset attached, and it is also
what contains the risk in the undocumented APIs: when `IPolicyConfig` breaks in a future
Windows release, one file changes.

### Dependency injection

Start by wiring services by hand in `App.xaml.cs` — five services, five `new` calls, zero
dependencies. Move to `Microsoft.Extensions.DependencyInjection` with the Generic Host when the
second vendor plug-in appears, since plug-ins register into a container without touching
startup. Revisit at phase 7, not before.

## 5. Phases

Ordered by dependency. Each phase is only started when the previous one builds, passes its
tests, and has been verified against real hardware.

### Phase 0 — Foundation

Folder structure, `BlueH.Tests` project added to the solution, hand-wired services in
`App.xaml.cs`, `ISettingsStore` with its JSON implementation, and the shared constants
(canonical date format). First test written before first implementation.

Deliverable: `dotnet test` runs and passes with a trivial `ViewModelBase` test.

### Phase 1 — Device discovery and status

Enumerate paired and connected Bluetooth audio devices and show live connection state. This is
the foundation every later phase reads from.

- APIs: `DeviceInformation.FindAllAsync` with an AQS filter, `DeviceWatcher` for live changes.
- Threading: watcher callbacks arrive on a thread pool thread. Marshal to the UI thread, or
  register the bound collection with `BindingOperations.EnableCollectionSynchronization`.
- Lifetime: always `Stop()` and unsubscribe the watcher — a live watcher holds the whole object
  graph behind its handlers.
- Tests: ViewModel driven by a fake `IBluetoothDeviceService`; added, removed, and updated
  devices; empty list; a device that disappears while selected.

Deliverable: a list of devices in the main window, updating as devices connect and disconnect.

### Phase 2 — Battery level

- APIs: GATT Battery Service (`0x180F`) for BLE devices; the Windows Bluetooth battery device
  property for classic ones.
- Coverage is decided by the headset, not by the app. Some report over GATT, some only over the
  vendor protocol, some not at all.
- **"Unknown" is a first-class state**, never rendered as zero. `BatteryChargePercent` is a
  nullable 0-100 integer.
- Tests: known value, unknown value, a value that arrives late, a device that stops reporting.

### Phase 3 — Audio endpoint control

Per-endpoint volume and mute, and switching the default playback and communication device. This
is where the real everyday annoyance lives: the A2DP (stereo) versus Hands-Free (mono) split,
and the two endpoints every headset creates.

- Volume and mute: Core Audio `IAudioEndpointVolume` — public, stable.
- Setting the default endpoint: **no public API exists.** The established route is the
  undocumented `IPolicyConfig` COM interface, which can break between Windows releases. It must
  live in exactly one file behind `IAudioEndpointService`.
- Tests: all against the interface; the COM implementation is verified by hand.

### Phase 4 — Media transport

Play, pause, next, previous, and now-playing metadata through
`Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager`. Public API, low risk.

### Phase 5 — Pairing management

Pair, unpair, and reconnect via `DeviceInformationPairing`. Needs care around the consent
callback and around operations that take seconds or hang when a device is asleep: every device
call is awaited with a timeout and a `CancellationToken`.

### Phase 6 — Internationalization

Spanish and English. The write-now rules in `CLAUDE.md` apply from phase 1, so this phase is
mostly moving already-externalised strings into per-culture `.resx` files and wiring culture
selection.

One decision to settle before starting: **runtime language switching or restart-only.** Runtime
switching needs a localization provider bound through `PropertyChanged`; restart-only allows
the simpler `{x:Static}`. This changes how every XAML file is written, so it cannot be deferred
past the first localized view.

### Phase 7 — Vendor-specific features

ANC, equaliser, touch controls, codec reporting. **No Windows API exposes any of this.** Each
vendor runs a proprietary protocol over RFCOMM (`Windows.Devices.Bluetooth.Rfcomm`) or BLE
GATT, implemented per vendor behind the same service interfaces, each one an optional plug-in
and never a dependency of the core app.

Scope depends entirely on which headphones are available to test against — currently unknown.
Sony, Jabra, and Bose have community-documented protocols.

## 6. Platform feasibility

What the plan can promise, and what it cannot:

| Tier | Features | Risk |
|---|---|---|
| Public API | discovery, status, pairing, volume, mute, media transport, GATT battery | low |
| Undocumented | setting the default audio endpoint (`IPolicyConfig`) | may break between Windows releases |
| Per vendor | ANC, equaliser, touch controls | needs the protocol for each brand |
| **Not possible** | **choosing the Bluetooth audio codec** (SBC/AAC/aptX/LDAC) | no API, public or otherwise |

Do not show a codec selector in the UI. Reporting the codec in use may be reachable through a
vendor protocol; choosing it is not.

## 7. Testing

TDD for everything below the view. The service-interface boundary is what makes it possible:
**the suite must never require a headset.**

- Unit-tested: ViewModels, commands, domain models, service logic.
- Faked: every WinRT, Core Audio, and file-system call.
- Clock: inject `TimeProvider`, use `FakeTimeProvider` in tests, never `DateTime.Now`.
- Tooling: NUnit with `Microsoft.NET.Test.Sdk`, NSubstitute for doubles.
- Hardware verification is a separate manual checklist per phase, never part of `dotnet test`.

## 8. Open decisions

1. **Which headphones are available for testing.** Decides whether phase 7 is viable at all and
   provides the only real verification target for phases 1-5.
2. **Runtime language switching versus restart-only.** Must be settled before the first
   localized view (phase 6, but it constrains phase 1 onward).
3. **Battery history.** If wanted, it is the one feature that brings SQLite in. Default
   assumption: not in scope, so no database.
4. **Theme as a user setting.** Currently hardcoded to dark. `ThemeMode` is settable at
   runtime, so this stays cheap to add later.

## 9. Definition of done, per phase

- `dotnet build` clean with `TreatWarningsAsErrors` on — no warnings, no suppressions.
- `dotnet test` green, with the new behavior covered by tests written before the code.
- No comments in the code; no user-facing string literals; no hardcoded colours.
- Verified by hand against a real headset, with the result written down.
- `CLAUDE.md` updated if a convention or a platform constraint changed.
