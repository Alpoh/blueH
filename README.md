# BlueH

A **WPF** desktop application for **Windows 11** that puts everything about your Bluetooth
headphones in one place.

Windows knows a surprising amount about a connected headset — battery level, connection state,
which audio endpoint it owns — and exposes almost none of it where you can act on it. BlueH is
a single GUI for the whole set: status and battery, volume and default-device switching, media
transport, pairing, and, per vendor, device features such as active noise cancelling and
equaliser presets.

It reads paired Bluetooth audio devices live, tells headphones from speakers, shows battery where
the headset reports it, and lets you set the volume, mute, and choose which audio output Windows
uses for playback and for calls. A diagnostics panel shows every platform call and its raw
result, so a failure names itself instead of showing an empty screen.

## Requirements

- Windows 11 (build 22000 or later)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build
- JetBrains Rider or Visual Studio, optionally

## Build

```bash
dotnet build
dotnet run --project BlueH
```

Self-contained single executable, no runtime needed on the target machine:

```bash
dotnet publish BlueH -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Layout

```
BlueH.sln                           Solution (single project)
CLAUDE.md                           Coding conventions and guidance for Claude Code
BlueH/
  BlueH.csproj                      WPF project, net10.0-windows10.0.26100.0
  App.xaml / App.xaml.cs            Entry point; StartupUri -> MainWindow.xaml
  MainWindow.xaml / .xaml.cs        Main window (the only one so far)
  ViewModels/
    ViewModelBase.cs                INotifyPropertyChanged base for ViewModels
  Properties/
    Resources.resx / .Designer.cs   Localizable strings
  Services/                         Ports and their WinRT, Core Audio and file-system adapters
  Models/ ViewModels/ Views/        Domain, presentation and XAML
  Diagnostics/                      Structured log and the in-app diagnostics panel
```

`bin/` and `obj/` are build output and are git-ignored.

## Architecture

- **MVVM over code-behind.** View code-behind is limited to view-only concerns
  (`InitializeComponent`, simple UI event glue). Logic, state, and commands live in ViewModels
  bound through `DataContext`.
- **Nothing talks to WinRT directly except a service.** The Bluetooth and audio APIs sit behind
  project-owned interfaces (`IBluetoothDeviceService`, `IAudioEndpointService`) that return
  plain domain models. That boundary is what lets the whole app be unit-tested with no headset
  attached, and it is the most important structural decision in the project.
- **`ViewModels.ViewModelBase`** implements `INotifyPropertyChanged` and exposes
  `SetField<T>(ref field, value)` and `OnPropertyChanged()`; both read the property name from
  `[CallerMemberName]`.
- **Declarative data binding.** Prefer `{Binding}` in XAML over assigning control properties
  from C#, and `ICommand` over `Click` handlers.
- **No comments in code.** If something seems to need one, rename or extract instead. All
  identifiers are in English; user-facing text goes in `Properties/Resources.resx`.
- **Metric units (SI) only.** One canonical SI unit per quantity, with the unit in the member
  name (`BatteryChargePercent`, `SignalStrengthDbm`, not `Battery`, `Signal`). Other units are
  a display-time conversion and never reach storage. WPF layout values (`Width`, `Margin`) are
  device-independent pixels, not domain measurements, and are exempt.
- **One date-time format: `yyyy-MM-dd HH:mm:ss`**, formatted and parsed with
  `CultureInfo.InvariantCulture`, everywhere text dates appear. Parse with `TryParseExact`,
  never `DateTime.Parse`. Store UTC.

The full C#, XAML, WPF, i18n, and testing conventions are in [CLAUDE.md](CLAUDE.md).

## Tests

```bash
dotnet test
```

220 tests. ViewModels, commands, domain models, and the pure logic inside services are covered,
with every WinRT, Core Audio and file-system call faked behind the ports, so the suite never
needs a headset. If a running instance of the app locks `BlueH.exe`, add `-p:UseAppHost=false`.

Every increment that adds logic is also mutation-checked: the implementation is broken on purpose
and the new tests must go red. A test that has never failed proves nothing. See the Testing / TDD
section of [CLAUDE.md](CLAUDE.md).

## Roadmap

Done:

1. **Device discovery and status** — paired devices keyed on their container id so a headset
   appears once rather than twice, headphones told from speakers, live connection state.
2. **Battery level** — read over Hands-Free Profile, where it turns out to live. "Unknown" is a
   real state, not a zero: a speaker with no Hands-Free service genuinely has no battery to
   report.
3. **Audio endpoint control** — per-endpoint volume and mute, and choosing the default output for
   playback and for calls separately.

Next:

4. **Media transport** — play, pause, next, previous.
5. **Pairing management** — pair, unpair, reconnect.
6. **Internationalization** — multi-language support. Until it lands, code is written i18n-ready:
   no user-facing string literals, no sentence concatenation, no fixed widths that would clip
   longer translations.
7. **Vendor-specific features** — ANC, equaliser, touch controls. No Windows API exposes these;
   each is a proprietary protocol over RFCOMM or BLE GATT, implemented per vendor as an optional
   plug-in.

Known limits of the platform, worth knowing before filing a feature request: Windows exposes no
public API to **choose the Bluetooth audio codec** (SBC/AAC/aptX/LDAC), and none to **set the
default audio endpoint** — the latter is reachable only through an undocumented COM interface.
Battery reporting depends on what the headset itself supports.
