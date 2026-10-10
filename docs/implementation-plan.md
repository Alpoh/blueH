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

**No database.** Windows is the source of truth for every value the UI shows, and re-reading it
is both cheap and correct. Storing it would mean maintaining a cache that can only ever be
wrong.

Read live from the platform, never persisted:

| Data | Source |
|---|---|
| Paired and connected devices | `DeviceInformation` / `DeviceWatcher` |
| Connection state | `DeviceWatcher` events |
| Battery charge | GATT Battery Service, or the Windows device property |
| Volume, mute, default endpoint | Core Audio |
| Now-playing track, transport state | `Windows.Media.Control` |

Persisted, because Windows does not remember it for us — this is **user intent**, not device
state: app preferences (language, window geometry, selected device), a per-device nickname, and
the per-device desired ANC / equaliser preset to re-apply on connect. A handful of keys, in
**one JSON file** under `%LOCALAPPDATA%\BlueH\settings.json` behind an `ISettingsStore` — the
equivalent of a properties file, not a database.

### When a database would become justified

Exactly one possible feature is database-shaped: **battery history**, to chart charge over days
or weeks. If it is built, use **SQLite** via `Microsoft.Data.Sqlite` (or EF Core, the Spring
Data equivalent, if the query surface grows). SQLite is an embedded single file — no server, no
installer — which keeps the self-contained single-executable publish intact.

Note for whenever that arrives: SQLite has no native date type. Store timestamps as UTC TEXT in
the canonical `yyyy-MM-dd HH:mm:ss` format, which sorts lexicographically in chronological
order — a reason that format earns its keep, not an accident.

No credentials, no personal data, and no network calls are involved anywhere in the app.

## 4. Architecture

```
BlueH/
  Models/          domain types, no UI and no WinRT
  Services/        interfaces (ports) + their platform implementations (adapters)
  ViewModels/      one per view, derived from ViewModelBase
  Views/           XAML windows and user controls
  Converters/      IValueConverter implementations
  Diagnostics/     logging and the in-app diagnostics panel
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

This is ports and adapters: the domain at the centre, WinRT and the view both adapters at the
edges. It is what makes the app testable with no headset attached, and what contains the risk
in the undocumented APIs — when `IPolicyConfig` breaks in a future Windows release, one adapter
changes and the domain does not.

### Dependency injection

Wire services by hand in `App.xaml.cs` to begin with — a handful of `new` calls, zero
dependencies. Move to `Microsoft.Extensions.DependencyInjection` with the Generic Host when the
second vendor plug-in appears, since plug-ins register into a container without touching
startup. Revisit at phase 7, not before.

## 5. Making failure visible

The main risk in this project is not writing wrong code; it is **building a lot and then not
knowing which part fails**. The app wraps platform APIs with uneven, partly undocumented
coverage, so "it doesn't work" is the default failure mode unless the design prevents it. Four
rules exist for that reason alone:

1. **Diagnostics come early, not last.** Increment 0.4 adds structured logging and an in-app
   diagnostics panel showing each platform call, its arguments, and its raw result. Every later
   increment logs through it. This is what turns "the battery doesn't show" into "the GATT read
   returned no characteristic".
2. **Never swallow an exception from a device call.** Map it to an explicit error state the UI
   displays and the log records, with the operation named. An empty UI with a silent `catch` is
   the worst possible outcome.
3. **One increment, one commit.** `git bisect` then answers "when did this break?" mechanically,
   and every commit is a point where the app ran and the suite was green.
4. **Absence is modelled, not implied.** "Unknown battery", "no endpoint", "no media session"
   are first-class states, never a zero, an empty string, or a blank panel that looks like a
   bug.

## 6. How every increment is built

Build order is **inside-out**: the domain first, the view last. The view is an adapter, not a
starting point.

```
spike            throwaway console probe: what does the API actually return?
  domain model   the types the tests assert on
  port           the interface, so a fake can exist
  failing test   red, written before any implementation
  ViewModel      green
  adapter        the real platform call; verified by hand, not by unit test
  view           XAML bound to the ViewModel
```

The spike exists because models-first has one failure mode: designing a model the platform
cannot fill. Codec is the cautionary example — model it as a first-class property, write the
tests, then discover Windows will not tell you. Twenty throwaway lines that print what the API
really returns prevents modelling on assumptions. A spike is never committed; its only product
is knowing what to model.

### Two rules that keep it incremental

1. **Every increment ends with the app running and the suite green.** Never a half-wired layer,
   never a commit that leaves the build red. If an increment cannot end that way, it is too big
   and must be split.
2. **Explore risk first, implement it last.** Spike the uncertain API early, because it is cheap
   and tells you whether the thing is possible at all. Then build the safe public-API parts
   before the risky one, so that if the risky part fails you have already shipped something that
   works.

## 7. Increments

33 increments, each one commit, each ending with a green suite and a running app. The
deliverable column says what is observable when it is done — and says so honestly when the
answer is "a test" rather than something on screen.

### Phase 0 — Foundation

| # | Increment | Deliverable | Tests |
|---|---|---|---|
| 0.1 | `BlueH.Tests` project (NUnit, NSubstitute) in the solution | `dotnet test` runs | `SetField` raises `PropertyChanged`; does not raise when the value is unchanged |
| 0.2 | Canonical date-format constant | a test | format round-trips through `TryParseExact`; still correct under a non-English culture; `MM` vs `mm` guarded by an explicit assertion |
| 0.3 | `ISettingsStore` + `JsonSettingsStore` | preferences survive a restart | round-trip; missing file; corrupt JSON; unwritable directory |
| 0.4 | **Structured logging + in-app diagnostics panel** | a window showing every platform call and its raw result | entries recorded in order; a failed call records the error; the panel renders an empty log |

### Phase 1 — Device discovery and status

Spike first: does the AQS filter actually match Bluetooth audio devices?

| # | Increment | Deliverable | Tests |
|---|---|---|---|
| 1.1 | `BluetoothHeadphones` model (`Id`, `Name`) + `IBluetoothDeviceService` + one-shot enumeration | the window lists your headphones by name | ViewModel maps results; duplicate ids; a device with a blank name |
| 1.2 | Empty and error states | "no devices found", and a visible error if enumeration fails | empty list renders the empty state; a throwing service renders the error, and logs it |
| 1.3 | Connection state per device | each row shows connected or disconnected | state mapping; unknown state stays unknown |
| 1.4 | Watcher: devices appearing and disappearing | the list reacts when you turn the headset on or off | added and removed events; cross-thread marshalling; watcher stopped and unsubscribed on close |
| 1.5 | Watcher: property changes on a device already listed | a rename or a state change updates in place, without the row jumping | updated event changes the existing item rather than re-adding; an update for an unknown id is ignored |
| 1.6 | Device selection, persisted | the selected device is remembered across restarts | selection survives a refresh; the selected device disappears; nothing selected; a persisted id that no longer exists |

### Phase 2 — Battery level

Spike first, both paths: GATT `0x180F` and the Windows battery device property. Coverage is
decided by the headset, not by the app.

| # | Increment | Deliverable | Tests |
|---|---|---|---|
| 2.1 | `BatteryCharge` model with **unknown as a first-class state** | a percentage, or an explicit "unknown" — never a misleading 0 | known value; unknown; 0 distinguished from unknown; out-of-range value rejected |
| 2.2 | GATT adapter | battery for a BLE headset | adapter logic against a faked GATT surface; no battery characteristic present |
| 2.3 | Device-property adapter, as a fallback chain behind the GATT one | battery for a classic headset | the chain tries GATT, falls back, and reports unknown when both fail — each step logged |
| 2.4 | Refresh on connect and on an interval | the number updates without reopening the app | driven by `FakeTimeProvider`: nothing before the interval, a refresh after it, no overlapping refreshes |

### Phase 3 — Audio endpoint control

Safe parts first; the undocumented call last and alone.

| # | Increment | Deliverable | Tests |
|---|---|---|---|
| 3.1 | Enumerate the audio endpoints belonging to the selected device | the headset's endpoints are listed, including the A2DP / Hands-Free pair | mapping; a device with no endpoint; an endpoint with no device |
| 3.2 | Read volume and mute | current volume and mute shown per endpoint | mapping; clamping to 0-100; a value out of range |
| 3.3 | Set volume | a working slider | command not re-entrant; value clamped before the call; a failing call surfaces an error state and logs it |
| 3.4 | Set mute | a working mute button | toggle semantics; failure handled |
| 3.5 | Switch the default playback / communication device (`IPolicyConfig`) | one click makes the headset the default output | command enablement and state; the adapter itself verified by hand |

Risk: **3.5 has no public API.** The route is the undocumented `IPolicyConfig` COM interface,
which can break between Windows releases. It lives in exactly one file behind
`IAudioEndpointService`, and it is built last precisely because 3.1-3.4 already deliver value
without it.

### Phase 4 — Media transport

| # | Increment | Deliverable | Tests |
|---|---|---|---|
| 4.1 | Now-playing title and artist | the current track is displayed | metadata mapping; no session; a session with empty metadata |
| 4.2 | Play and pause | working play/pause | command enablement per transport state |
| 4.3 | Next and previous | working skip buttons | enablement when the session forbids skipping |

### Phase 5 — Pairing management

Every device call is awaited with a timeout and a `CancellationToken`: pairing takes seconds and
hangs when a device is asleep.

| # | Increment | Deliverable | Tests |
|---|---|---|---|
| 5.1 | Reconnect an already-paired device | one click to reconnect | state machine; timeout; cancellation |
| 5.2 | Pair a new device | discover and pair from the app | the consent callback path; failure codes; cancelling mid-pair |
| 5.3 | Unpair | remove a device | confirmation required; failure handled and logged |

### Phase 6 — Internationalization

Decision to settle before 6.1: **runtime language switching or restart-only.** Runtime switching
needs a localization provider bound through `PropertyChanged`; restart-only allows the simpler
`{x:Static}`. It changes how every XAML file is written, so it cannot be deferred past the first
localized view.

| # | Increment | Deliverable | Tests |
|---|---|---|---|
| 6.1 | Culture set at startup + the WPF `Language` override | numbers format per culture | the override is applied; the canonical date format stays invariant regardless of culture |
| 6.2 | Key-coverage enforcement | a test, not a checklist | **every key in the neutral `.resx` exists in every culture `.resx`** |
| 6.3 | `Resources.es.resx` | the app runs in Spanish | 6.2 passes with the new file |
| 6.4 | Pseudo-locale pass (`qps-ploc`) | lengthened accented strings reveal clipping | a test that fails on any user-facing string literal found in XAML, turning the convention into something the build enforces |

### Phase 7 — Vendor-specific features

Counter-intuitively **the most unit-testable phase of all**: a proprietary protocol is bytes in
and bytes out, pure functions over arrays. Capture real traffic once, then test offline forever
with no headset attached.

| # | Increment | Deliverable | Tests |
|---|---|---|---|
| 7.1 | Frame encoding and decoding for one vendor | a test | build a command frame; parse a response; truncated frame; bad checksum; unknown opcode |
| 7.2 | RFCOMM or GATT transport for that vendor | the app talks to the headset, visible in the diagnostics panel | transport faked; reconnect; the device refuses the connection |
| 7.3 | One feature end to end: ANC on/off | a working ANC toggle | ViewModel state; a failed command does not leave the toggle lying |
| 7.4 | Desired state persisted and re-applied on reconnect | ANC preference survives turning the headset off and on | re-applied on connect; not re-applied to a different device; conflicting stored state resolved |

Scope depends entirely on which headphones are available to test against — currently unknown.
Sony, Jabra, and Bose have community-documented protocols. Each vendor is an optional plug-in,
never a dependency of the core app.

## 8. Platform feasibility

| Tier | Features | Risk |
|---|---|---|
| Public API | discovery, status, pairing, volume, mute, media transport, GATT battery | low |
| Undocumented | setting the default audio endpoint (`IPolicyConfig`) | may break between Windows releases |
| Per vendor | ANC, equaliser, touch controls | needs the protocol for each brand |
| **Not possible** | **choosing the Bluetooth audio codec** (SBC/AAC/aptX/LDAC) | no API, public or otherwise |

Do not show a codec selector in the UI. Reporting the codec in use may be reachable through a
vendor protocol; choosing it is not.

## 9. Testing

TDD for everything below the view. The port boundary is what makes it possible: **the suite must
never require a headset.**

- Unit-tested: ViewModels, commands, domain models, service logic, protocol codecs.
- Faked: every WinRT, Core Audio, and file-system call.
- Clock: inject `TimeProvider`, use `FakeTimeProvider` in tests, never `DateTime.Now`.
- Tooling: NUnit with `Microsoft.NET.Test.Sdk`, NSubstitute for doubles.

### What tests cannot tell us

A test against our own fake proves the code is consistent with **our assumption** about the
platform API. If the assumption is wrong, the test is green and the app is broken. This is the
real limit of mocking at a boundary, and it is why three things in this plan are not decoration:
the spike that validates the assumption before the fake is written, the diagnostics panel that
shows what the platform actually returned, and the named manual verification in every
increment's definition of done.

Anything that needs real hardware goes in an opt-in integration project that never runs as part
of `dotnet test`. Green means "the logic is correct", not "the app works".

## 10. Open decisions

1. **Which headphones are available for testing.** Gates phase 7 entirely and provides the only
   real verification target for phases 1-5.
2. **Runtime language switching versus restart-only.** Must be settled before increment 6.1, and
   it constrains every XAML file written from phase 1 onward.
3. **Battery history.** The one feature that would introduce SQLite. Default assumption: out of
   scope, so no database.
4. **Theme as a user setting.** Currently hardcoded to dark. `ThemeMode` is settable at runtime,
   so this stays cheap to add later.

## 11. Definition of done, per increment

- `dotnet build` clean with `TreatWarningsAsErrors` on — no warnings, no suppressions.
- `dotnet test` green, with the new behavior covered by tests written before the code.
- **The app still runs**, and the increment's deliverable is observable in it.
- No comments in the code; no user-facing string literals; no hardcoded colours.
- Every platform call the increment adds logs through the diagnostics channel.
- No exception from a device call is swallowed; failures reach an error state the UI shows.
- Verified by hand against a real headset, with the result written down.
- One increment, one commit.
- `CLAUDE.md` updated if a convention or a platform constraint changed.
