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
- `BlueH.Tests` (NUnit 4, NSubstitute, FakeTimeProvider) on the same TFM as the app.
- Phases 0, 1, 2 and 3 complete. 220 tests green.
- The window lists paired Bluetooth audio devices with their type, connection state and battery, above the diagnostics panel. Verified against real hardware: OPPO Enco Air5 as connected headphones at 80%, Sony SRS-XE300 as a disconnected speaker with battery unknown.
- Battery is polled every minute and whenever a device becomes connected, through `FirstKnownBatteryService` over the Hands-Free device property, with GATT behind it as an unverified fallback.
- Selecting a device lists its audio outputs with a volume slider, a mute toggle, and buttons to make an output the default for playback or for calls. Volume writes are coalesced so dragging the slider issues one call per completed write.
- Services are hand-wired in `App.xaml.cs`; there is no container yet.
- **Not yet verified by hand: everything in phase 3.** Reads were verified by spike against Core Audio, but the UI and `IPolicyConfig.SetDefaultEndpoint` have never been exercised. Reconnect the earbuds, run the app, and click the default buttons deliberately — that call is the one undocumented thing in the project.
- Next: phase 4, media transport. It is public API and low risk, so no spike is strictly needed; a short one confirming `GlobalSystemMediaTransportControlsSessionManager` sees a session is still worth ten minutes.
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
  Services/Interop/ COM declarations for Core Audio and the undocumented IPolicyConfig
  Time/            CanonicalTimestamp: the one date-time format, with its parser
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

38 increments, each one commit, each ending with a green suite and a running app. The
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

Spike findings, 2026-10-10, run against this machine's own devices (OPPO Enco Air5 earbuds
connected, Sony SRS-XE300 speaker paired, an iPhone paired, and a Sony INZONE H5 headset present
as an audio endpoint but not over Bluetooth). These replace the assumptions the increments below
were written on.

- **Identity is `System.Devices.Aep.ContainerId`, not `DeviceInformation.Id`.** The same earbuds
  appear twice, once as a classic endpoint (`Bluetooth#...`) and once as a Bluetooth LE endpoint
  (`BluetoothLE#...`), with different ids, different signal strengths, and even different
  `IsConnected` values — classic `True` while LE reads `False`. Both carry the same
  `ContainerId`. Keying the list on `Id` would show every headset twice and let the two rows
  disagree about whether it is connected.
- **Connection state is `System.Devices.Aep.IsConnected`.** `DeviceInformation.IsEnabled` came
  back `False` for every association endpoint, connected ones included; it does not mean what
  its name suggests. `BluetoothDevice.ConnectionStatus` agrees with the AEP property but costs a
  `FromIdAsync` call per device and cannot be watched, so the property is the one to use.
- **Filter on `Cod.Major:=4` (AudioVideo).** Filtering on
  `System.Devices.Aep.Bluetooth.Cod.Services.Audio` does not work: the iPhone reports it `True`,
  and the AQS filter returned all three devices. `Cod.Major` cleanly separates the phone (2) from
  the earbuds and the speaker (4), and it can go in the AQS string rather than being filtered in
  our own code.
- **Use the raw `Cod.Minor` number, never WinRT's `ClassOfDevice.MinorClass`.** The projected
  enum is wrong: the earbuds report `ComputerDesktop` and the speaker reports `PhoneIsdn`. The
  raw values are correct and useful — 1 is a wearable headset, 5 a loudspeaker — which is how the
  app can tell headphones from a speaker. Minor-class values only have meaning inside a major
  class, and the projection ignores that.
- **No battery anywhere in device properties.** Both known battery property keys came back absent
  on all three devices, including the connected earbuds. Battery must therefore be modelled as
  genuinely unknown rather than as a value waiting to be read, and phase 2 has to go through
  GATT. A warning for that phase: the earbuds' LE endpoint reads `IsConnected = False` while the
  classic endpoint is connected and playing audio, so the GATT route may be unreachable exactly
  when the battery matters most.
- **Device property types are not what you would guess.** `Cod.Minor` comes back as `UInt16`, not `UInt32`; `ContainerId` as `Guid`; `IsConnected` as `Boolean`. A pattern match on the wrong numeric type fails silently and leaves the value absent, which is how the first build of this phase shipped with every device classified as unknown. `DevicePropertyReader` therefore accepts any plausible integral type and is unit-tested against each one.
- **`SignalStrength` is in dBm and is not comparable across transports.** The same earbuds read
  -8 over classic and -52 over LE. Never put the two on one scale.
- **The audio-endpoint join key is `System.Devices.ContainerId`** — a different property name
  from the association endpoint's `System.Devices.Aep.ContainerId`, holding the same value. The
  endpoint named `Headphones (OPPO Enco Air5)` carries the earbuds' container id, which is what
  phase 3 will join on. Audio endpoints are `DeviceInformationKind.DeviceInterface`, Bluetooth
  devices are `AssociationEndpoint`, and the two kinds expose different property sets.
- **Not every audio endpoint has a Bluetooth device behind it.** The INZONE H5 appears as two
  endpoints with a shared container id and never appears in any Bluetooth enumeration, because it
  connects over its own 2.4 GHz dongle. The join must tolerate endpoints with no Bluetooth device
  and Bluetooth devices with no endpoint — a paired but disconnected headset has none.
- **The A2DP and Hands-Free split is real but not universal.** The INZONE H5 shows two endpoints,
  `Headphones (… - Game)` and `Headset Earphone (… - Chat)`; the connected earbuds currently show
  only one. Phase 3 cannot assume a pair.
- **Windows supplies an icon per device** in `System.Devices.Icon` and
  `System.Devices.GlyphIcon`, as `DDORes.dll` resource ids that differ by device type. Free
  iconography if we ever want to extract them, and a fallback is still needed.
- **`GetDeviceSelectorFromPairingState(true)` is not strictly "paired only".** The selector it
  generates is `IsPaired OR IssueInquiry:=False`. It returned 3 devices where the raw protocol
  AQS returned 10, so it is a useful filter, but read it as "paired or already known" rather than
  a guarantee.
| # | Increment | Deliverable | Tests |
|---|---|---|---|
| 1.1 | `BluetoothAudioDevice` model keyed on `ContainerId`, `IBluetoothDeviceService`, and one-shot enumeration filtered with `Cod.Major:=4` | the window lists the paired audio devices by name, each appearing once | ViewModel maps results; the same container arriving twice from the classic and LE endpoints collapses to one row; a device with a blank name |
| 1.2 | Headphones told apart from speakers by the raw `Cod.Minor` value | the speaker is distinguishable from the earbuds in the list | 1, 2 and 6 read as headphones, 5 as a loudspeaker, an unknown value stays unknown rather than defaulting |
| 1.3 | Empty and error states | "no devices found", and a visible error if enumeration fails | empty list renders the empty state; a throwing service renders the error and records it in diagnostics |
| 1.4 | Connection state from `Aep.IsConnected` | each row shows connected or disconnected | state mapping; the two endpoints of one container disagreeing resolves to connected; a device whose state is absent stays unknown |
| 1.5 | Watcher: devices appearing and disappearing | the list reacts when you turn the headset on or off | added and removed events; marshalling through `IUiScheduler`; watcher stopped and unsubscribed on close |
| 1.6 | Watcher: property changes on a device already listed | a rename or a connection change updates in place, without the row jumping or duplicating | updated event changes the existing container's row rather than adding one; an update for an unknown container is ignored |
| 1.7 | Device selection, persisted through `ISettingsStore` | the selected device is remembered across restarts | selection survives a refresh; the selected device disappears; nothing selected; a persisted container id that no longer exists |

### Phase 2 — Battery level

Spike findings, 2026-10-10. Battery **is** reachable, but through none of the routes this plan
originally assumed. Four were probed; one works.

| Route | Result |
|---|---|
| Property on the association endpoint | absent on every device, connected ones included |
| **Property on the PnP `Device` object** | **the value, as a `Byte`** |
| GATT Battery Service `0x180F` over LE | `Unreachable`: the LE endpoint stays disconnected while the classic endpoint carries audio |
| `Windows.Devices.Power.Battery` | zero devices; it covers system batteries, not peripherals |

- **The battery lives on one PnP `Device` object out of twenty-four** sharing the container. For
  the earbuds it is the object whose id carries the Hands-Free Profile UUID
  `0000111e-0000-1000-8000-00805f9b34fb`, named `OPPO Enco Air5 Hands-Free AG`, and the value
  arrives on `{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2`.
- **Enumerate with `DeviceInformationKind.Device`** and the AQS
  `System.Devices.ContainerId:="{<container>}"`, then take whichever object carries the property.
  **Never match on the device name** — it is vendor text and may be localised. AQS cannot express
  "this property exists", so the scan happens in our code.
- **The value is a `Byte`**, not a `UInt16` like `Cod.Minor` and not a `UInt32`. This is the
  second property in two phases whose type is not the obvious one; read it through
  `DevicePropertyReader`.
- **Battery arrives over Hands-Free Profile**, which explains the coverage pattern precisely: the
  speaker is A2DP-only, has no HFP service object, and therefore has no battery — not a bug, and
  not something a different API would fix. A headset reports battery only while HFP-connected.
- **`System.Devices.BatteryLifePercent` is not a valid property key** at all (the enumeration
  throws a syntax error), and `System.Devices.BatteryPlusCharging`, while a valid key, carried no
  value. Neither is a route.
- **GATT is not worth building against this hardware.** It cannot be verified here, so it is
  deferred to the last increment of the phase rather than being the first as originally planned.

| # | Increment | Deliverable | Tests |
|---|---|---|---|
| 2.1 | `BatteryCharge` model with **unknown as a first-class state** | a percentage, or an explicit "unknown" — never a misleading 0 | known value; unknown; 0 distinguished from unknown; a value above 100 rejected |
| 2.2 | `DevicePropertyReader` reads the battery byte | a test | read from `Byte`; from the other plausible integral types; absent stays unknown; a value out of range rejected |
| 2.3 | `IBatteryService` and the HFP device-property adapter | the earbuds show a charge percentage, the speaker shows unknown | ViewModel maps a value, an unknown, and a failure; the container with no battery-bearing object reports unknown and logs why |
| 2.4 | Battery shown per device in the list | a battery column that reads "unknown" rather than blank or zero | the column renders all three states; unknown is not styled as an error |
| 2.5 | Refresh on connect and on an interval | the number updates without reopening the app | driven by `FakeTimeProvider`: nothing before the interval, a refresh after it, no overlapping refreshes, a refresh when a device becomes connected |
| 2.6 | GATT `0x180F` as a secondary route, tried only when no HFP battery exists | battery for an LE-only headset, if one is ever available | adapter logic against a faked GATT surface; `Unreachable` falls through to unknown and is logged; the chain never masks a working HFP value |

Increment 2.6 cannot be verified against the hardware available today, so it ships last and
behind the route that works. Do not let it block the phase.

### Phase 3 — Audio endpoint control

Spike findings, 2026-10-10. Core Audio reads work, the join to Bluetooth works, and
`IPolicyConfig` is reachable on this Windows build.

- **The join between the two worlds is the MMDevice id**, embedded in the WinRT endpoint id:
  `\\?\SWD#MMDEVAPI#{0.0.0.00000000}.{<guid>}#{e6327cad-…}`. Extract the part between
  `MMDEVAPI#` and the next `#` and it matches `IMMDevice.GetId` exactly. That is how a name and
  a container id from WinRT get attached to a volume and a mute flag from Core Audio. Extraction
  is pure string work, so it is unit-testable and must be tested.
- **The Bluetooth join is `System.Devices.ContainerId`** on the render endpoint, confirmed: the
  INZONE H5's two endpoints both carry container `4969f11a-…`.
- **An audio endpoint exists only while the device is connected.** Observed live: the earbuds had
  `Headphones (OPPO Enco Air5)` while connected, and once they disconnected the endpoint was gone
  from both the WinRT selector and Core Audio. So "connected device with no endpoint yet" and
  "disconnected device with no endpoint" are both real states, and neither is an error. Volume,
  mute and default-switching are only meaningful for a connected device.
- **Volume and mute are per endpoint, not per device.** At one moment the INZONE's
  `Headset Earphone (… - Chat)` read 100% while its `Headphones (… - Game)` read 76%. A single
  per-device volume would be wrong; the UI shows one row per endpoint.
- **Core Audio reports volume as a scalar from 0 to 1**, not a percentage.
  `GetMasterVolumeLevelScalar` returned 0.86 for an endpoint sitting at 86%. Convert at the
  adapter boundary and name the member for its unit — `VolumePercent` in the domain, never a bare
  `Volume` carrying a scalar. This is exactly the mismatch the units rule exists to prevent.
- **Playback and communications are separate defaults.** `GetDefaultAudioEndpoint` takes a role,
  and `eConsole` and `eCommunications` can point at different endpoints. On this machine both
  currently point at the INZONE's *Chat* endpoint, which is precisely the annoyance the app
  exists to fix: the communications endpoint has taken over playback.
- **`IPolicyConfig` is reachable here.** CLSID `870af99c-171d-4f9e-af0d-e63df40c2bc9` creates,
  and `QueryInterface` for IID `f8679f50-850a-41cf-9c72-430f290290c8` returns `S_OK`. The spike
  only created and queried it; nothing was changed. It remains undocumented and may break between
  Windows releases, so it stays behind `IAudioEndpointService` in one file.
- **Core Audio and WinRT agree on which endpoints exist**: `EnumAudioEndpoints(eRender,
  DEVICE_STATE_ACTIVE)` and `MediaDevice.GetAudioRenderSelector()` both returned the same four.
- **An endpoint may have no Bluetooth device behind it.** The INZONE H5 connects over its own
  2.4 GHz dongle; it has a container and two endpoints and appears in no Bluetooth enumeration.
  The join must leave such endpoints unattached rather than treating them as a failure.

Verification note: the earbuds disconnected during the spike, so the two-endpoint case was
confirmed against the INZONE H5 rather than against them. Reconnect the earbuds before verifying
increments 3.2 onward by hand.

| # | Increment | Deliverable | Tests |
|---|---|---|---|
| 3.1 | `AudioEndpoint` model and MMDevice-id extraction | a test | the id is extracted from a real `SWD#MMDEVAPI#…` string; an id in another shape is left alone; an empty string is handled |
| 3.2 | `IAudioEndpointService` and enumeration joined by container | the selected device's endpoints are listed, including the A2DP and Hands-Free pair | mapping; a device with no endpoint while disconnected; an endpoint whose container matches no device |
| 3.3 | Read volume and mute | current volume and mute per endpoint | the scalar is converted to a percentage at the boundary; 0 and 1 map to 0% and 100%; a value outside the range is rejected rather than clamped silently |
| 3.4 | Set volume | a working slider | the percentage is converted back to a scalar; out-of-range input refused; the command is not re-entrant; a failure surfaces and is logged |
| 3.5 | Set mute | a working mute button | toggle semantics; failure handled |
| 3.6 | Show which endpoint is default, per role | the playback and communications defaults are visible and distinguishable | both roles mapped; the same endpoint holding both roles; neither role resolving |
| 3.7 | Make an endpoint the default, per role (`IPolicyConfig`) | one click makes the headset the default output | command enablement and state; the adapter itself verified by hand |

Increment 3.7 is the only place in the project that depends on an undocumented API. It is built
last, lives in one file behind the port, and 3.1-3.6 deliver value without it.

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
- Mutation-checked whenever the increment adds logic: break the implementation deliberately and confirm the new tests go red. Check that the tests actually ran: a mutation that fails to compile, or one that makes a test hang instead of fail, proves nothing. Both have happened — the second is a signal to rewrite the test so it fails fast. A test that has never failed proves nothing — this is how the string-interning gap in increment 0.1 was found.
- Verified by hand against a real headset, with the result written down.
- One increment, one commit.
- Run the suite with `-p:UseAppHost=false` when a running instance of the app locks `BlueH.exe` and the build cannot replace it. This happens often during mutation checks and silently turns them into no-ops.
- `CLAUDE.md` updated if a convention or a platform constraint changed.
