# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

BlueH is a WPF desktop application for **Windows 11** that aims to expose and control everything Windows knows about connected Bluetooth headphones from a single GUI: battery level, connection and pairing state, audio profile and default-endpoint switching, volume, media transport, and — where a vendor protocol is implemented — device-specific features such as active noise cancelling and equaliser presets. It exists because Windows surfaces almost none of this in a usable interface.

The project is at an early stage: solution layout, MVVM base, and conventions are in place; no feature code has been written yet. See the Planned work section for the intended order of implementation.

## Build and run

SDK-style project targeting **.NET 10** (LTS). Build with the `dotnet` CLI.

```
dotnet build
dotnet run --project BlueH
```

Release build, and a self-contained single file that needs no runtime installed on the target machine:

```
dotnet build -c Release
dotnet publish BlueH -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Requires the **.NET 10 SDK**. There is no test project in the solution yet.

## Architecture notes

- `BlueH.sln` contains a single project, `BlueH\BlueH.csproj`.
- `TargetFramework` is `net10.0-windows10.0.26100.0` — the Windows SDK version in the TFM is what makes the WinRT Bluetooth and audio APIs (`Windows.Devices.Bluetooth`, `Windows.Devices.Enumeration`, `Windows.Media.Control`) available. Do not reduce it to a plain `net10.0-windows`, which drops those projections.
- `SupportedOSPlatformVersion` is `10.0.22000.0` (Windows 11 RTM) — the minimum OS the app runs on, and the baseline for platform-compatibility analyzer warnings.
- `RootNamespace`/`AssemblyName` is `BlueH`; the WPF startup object is `App` (`App.xaml` with `StartupUri="MainWindow.xaml"`).
- `MainWindow.xaml` / `MainWindow.xaml.cs` is the only window and currently has an empty `Grid`.
- `ViewModels/ViewModelBase.cs` is the `INotifyPropertyChanged` base for all ViewModels.
- Assembly metadata lives in `BlueH.csproj` properties; there is no `AssemblyInfo.cs`. There is no `App.config` either — runtime configuration is `runtimeconfig.json`, generated from the project file.
- `Nullable` and `ImplicitUsings` are enabled and `TreatWarningsAsErrors` is on: a warning fails the build, so fix it rather than suppress it.
- `bin/` and `obj/` are git-ignored build output/intermediates.

## Units of measure (critical)

**Everything metric. No exceptions in the domain.** This is a hard project invariant, not a style preference: a unit mismatch is a silent, data-corrupting class of bug — nothing throws, the numbers just come out wrong — so it is cheaper to forbid the mixture outright than to audit for it later.

### The invariant

- **One canonical SI unit per quantity**, chosen once and used everywhere: metres for length, kilograms for mass, litres for volume, degrees Celsius for temperature, km/h for speed, square metres for area, seconds for duration. Dates and times use the canonical format of the next section.
- **No imperial or US customary units** — inches, feet, yards, miles, pounds, ounces, stones, gallons, pints, Fahrenheit — anywhere in the domain model, calculations, persistence, file formats, or APIs.
- **The unit is part of the name, never implicit.** `WeightInKilograms`, `DistanceInMetres`, `TemperatureInCelsius` — not `Weight`, `Distance`, `Temperature`. A bare numeric name is a defect, because it pushes the reader to guess. (This is also why the rule needs no comment to explain it — the name carries the unit.)
- **Prefer a unit-bearing type over a raw `double`** for any quantity that flows through more than one class: a small immutable `readonly record struct` that can only be constructed from an explicit unit makes the mismatch a compile error instead of a wrong answer.
- **Convert only inward, only at the boundary.** External input in imperial units is converted to SI the moment it enters — in the parser, importer, or device adapter — and never travels further in its original unit. Nothing downstream of the boundary knows a non-SI unit existed.
- **Conversion factors live in one place** as named constants, never inlined at a call site and never duplicated.

### Domain quantities in this project

Bluetooth and audio work brings its own units; the same discipline applies. Use SI and name the unit: percent for battery charge (`BatteryChargePercent`, 0-100 integer, never a 0-1 fraction without saying so), dBm for signal strength (`SignalStrengthDbm`, negative), hertz for sample rate (`SampleRateHertz`), bits per second for bitrate (`BitrateBitsPerSecond`), milliseconds for latency (`LatencyMilliseconds`), decibels for gain (`GainDecibels`).

### Display

- Displaying a value in another unit is a **presentation-layer** concern only, handled by an `IValueConverter` or the ViewModel, and it never changes what is stored or computed.
- Never persist a value in whatever unit the user happened to be viewing, never store the same quantity in two units, and never let the current culture influence what is written to disk.
- Unit symbols are user-facing text: they come from `Properties/Resources.resx` next to the formatted number, not from a literal in XAML or C#.

### Scope

- This rule is about **domain measurements**. WPF layout values are not domain measurements: XAML `Width`, `Height`, `Margin`, and `Thickness` are device-independent pixels (1/96 inch), a framework unit — leave them as they are. Likewise font sizes and `DpiX`/`DpiY`.
- Prose in the docs and in explanations also uses metric.

## Date and time format (critical)

**The one canonical format is `yyyy-MM-dd HH:mm:ss`** — for example `2026-10-10 15:48:03`. Zero-padded, 24-hour clock, biggest unit first, always in that order. Date only, where no time applies: `yyyy-MM-dd`. This holds everywhere a date or time is rendered as text: UI, logs, exception messages, file and folder names, exports, serialization, and debug output.

- **The .NET format string is exactly `"yyyy-MM-dd HH:mm:ss"`.** `MM` is months and `mm` is minutes; `HH` is 24-hour and `hh` is 12-hour. Swapping them compiles, runs, and produces wrong output — the same silent-failure class as a unit mismatch.
- **Always pass `CultureInfo.InvariantCulture`** when formatting or parsing. In a custom .NET format string `:` and `/` are *placeholders* for the culture's time and date separators, not literal characters, so `ToString("yyyy-MM-dd HH:mm:ss")` without an explicit culture produces different separators on some machines. Invariant culture is what pins the format.
- **Parse with `DateTime.TryParseExact`** (or `DateTimeOffset.TryParseExact`) against the canonical format and `InvariantCulture`. Never `DateTime.Parse` on a string whose shape you control — it guesses using the current culture.
- **Keep the format in one named constant** referenced by both the formatting and the parsing side, so the two can never drift apart.
- **In XAML**, a bound date must not fall back to the ambient culture: format it through a converter that uses the constant, or set `ConverterCulture` explicitly. A bare `StringFormat` relies on the element's `Language`, which is not the same thing.
- **This is a deliberate exception to the internationalization rule** that formatting follows `CultureInfo.CurrentCulture`. Dates and times use the canonical format regardless of culture, because one unambiguous ordering is worth more here than locale familiarity. Numbers, currency, and collation still follow `CurrentCulture`.
- **Store UTC and say so in the name** (`CreatedAtUtc`, `LastSeenUtc`), converting to local time only for display. Prefer `DateTimeOffset` over `DateTime` whenever an offset must be preserved.
- **Never call `DateTime.Now` or `DateTimeOffset.UtcNow` directly in testable code.** Inject `TimeProvider` and call `GetUtcNow()`, so tests can control the clock instead of sleeping.

## C# / WPF conventions

- **No comments in code (strict)**: do not write comments. If a piece of code seems to need one, that is a signal to refactor instead — rename the variable, method, or class so the intent is obvious, extract a well-named method for the block you were about to explain, or introduce a named constant in place of the literal. Self-explanatory names are the only allowed form of explanation. This covers `//`, `/* */`, XML doc comments, XAML `<!-- -->`, and `#region` markers. The sole exceptions are text the tooling requires (generated-file headers, license headers, a `// ReSharper disable` or `#pragma warning` suppression that must document its reason) and an inline note recording a non-obvious external constraint the code genuinely cannot express, such as a workaround for a documented framework or Windows API bug.
- **English only (code and documentation)**: all code is written in English — class, method, property, field, parameter, and local names, namespaces, file and folder names, XAML `x:Key`/`x:Name` values, enum members, constants, log and exception messages, and test names. No Spanish identifiers and no mixed-language names (`GetUsuario`, `_clienteList`). The same applies to everything written about the code: `README.md`, this file, any other Markdown or docs added to the repo, and commit messages and PR descriptions. User-facing display text is the one exception and is not hardcoded anyway: it belongs in `Properties/Resources.resx`, so both the implementation and the docs stay English whatever language the UI shows.
- **Metric units (SI) only**: see the Units of measure section above — it is a hard project invariant, and the most important of these rules to get right.
- **Naming**: `PascalCase` for classes, methods, properties, and public fields; `camelCase` for locals and parameters; private fields as `_camelCase`. One public class per file, filename matches the class name. File-scoped namespaces.
- **MVVM over code-behind**: keep `MainWindow.xaml.cs` (and any future window/`UserControl` code-behind) limited to view-only concerns (wiring up `InitializeComponent`, simple UI event glue). Put business logic, state, and commands in a `ViewModel` and bind to it via `DataContext` rather than manipulating controls directly from code-behind.
- **Data binding**: prefer XAML `{Binding}` over setting control properties imperatively in C#. Every ViewModel derives from `BlueH.ViewModels.ViewModelBase`, which implements `INotifyPropertyChanged` and exposes `SetField<T>(ref field, value)` for property setters and `OnPropertyChanged()` for manual notification. Both take the property name from `[CallerMemberName]`, so do not pass it explicitly.
- **Commands over click handlers**: for new interactive controls, prefer an `ICommand` bound via `Command="{Binding ...}"` instead of a `Click`/event handler in code-behind.
- **Async**: use `async Task` and `await`; only use `async void` for top-level UI event handlers. Never block on async code with `.Result` or `.Wait()` on the UI thread. Pass a `CancellationToken` through any operation that talks to a device, and honour it.
- **Nullability**: `Nullable` is enabled and warnings are errors. Express optionality in the type (`Headphones?`) rather than returning `null` from a non-nullable signature or silencing with `!`.
- **Resource cleanup**: wrap `IDisposable` resources (streams, sockets, device sessions) in `using`/`using var`, and implement `IAsyncDisposable` for anything holding a device connection.
- **XAML organization**: as the UI grows, factor shared styles/brushes/templates into `ResourceDictionary` files merged from `App.xaml` instead of repeating inline styles per control.
- **Avoid magic values**: pull repeated strings/numbers into constants or resources rather than inlining them across XAML/C#.

## WPF rules

Common WPF practice as it applies to this project.

### MVVM boundaries

- **The ViewModel must not know the View.** No references to `Window`, `Button`, `MessageBox`, or `Dispatcher` from a ViewModel. If a dialog or file picker is needed, put it behind an interface (`IDialogService`) and pass it in through the constructor — that is also what keeps the ViewModel unit-testable.
- **The ViewModel must not know WinRT either.** Bluetooth and audio APIs sit behind project-owned interfaces (`IBluetoothDeviceService`, `IAudioEndpointService`) that return plain domain models, so ViewModels can be tested with a fake and no hardware present. This is the single most important structural decision in the project.
- **One ViewModel per view**, deriving from `ViewModels.ViewModelBase`. Domain models live in their own folder and carry no UI behavior.
- **Never reach for a control by `x:Name` from C#.** If you are writing `myTextBox.Text = ...`, a binding is missing.

### Data binding

- **Bind, don't assign.** `Text="{Binding Name}"` instead of setting the property from code.
- **State the mode** when it isn't the default: `Mode=OneWay`, `TwoWay`, `OneTime`. For input fields, use `UpdateSourceTrigger=PropertyChanged` when the ViewModel needs to react per keystroke.
- **`ObservableCollection<T>`** for any bound collection that changes; `List<T>` raises nothing on add/remove. Sort, filter, and group with `CollectionViewSource` instead of rebuilding the collection.
- **Bound collections mutated off the UI thread** — which device watchers will do — need `BindingOperations.EnableCollectionSynchronization`, or the change must be marshalled to the UI thread first. Without one of the two, WPF throws on the cross-thread change.
- **`FallbackValue`/`TargetNullValue`** so a broken binding doesn't leave blank UI, and `StringFormat` to format in XAML rather than exposing pre-formatted strings.
- **`RelativeSource AncestorType` or `ElementName`** to walk the tree, rather than passing control references around in code.
- **`IValueConverter` implementations stay stateless**, declared once as a resource and reused. Use the built-in `BooleanToVisibilityConverter` before writing your own.

### Commands

- **`CanExecute` drives enablement** — don't set `IsEnabled` by hand.
- Commands that talk to a device are asynchronous and must not be re-entrant: disable the command while it runs rather than queueing duplicate device calls.
- Prefer the built-in `RoutedUICommand`s (`ApplicationCommands.Save`, `Copy`, …) where they fit; they come with key gestures already bound.
- Event handlers in code-behind are acceptable only where no command equivalent exists (low-level keyboard, drag & drop) — and they should delegate straight to the ViewModel.

### Threading

- WinRT device callbacks and `DeviceWatcher` events arrive on a thread pool thread. Marshal to the UI thread with `Dispatcher.InvokeAsync` before touching bound state that WPF is not synchronizing.
- Check `Dispatcher.CheckAccess()` before touching UI objects when the calling thread is uncertain.
- Never block the UI thread waiting on a device: Bluetooth calls can take seconds, or hang when a device is asleep. Always `await`, always with a timeout or cancellation.

### Layout

- **Fluid layout, not fixed coordinates.** `Grid` with `*`/`Auto` rows and columns, `StackPanel`, `DockPanel`. Avoid `Canvas` and negative `Margin` for positioning.
- **No hard-coded `Width`/`Height`** on containers; let content size them and use `MinWidth`/`MaxWidth` to constrain.
- `SharedSizeGroup` to align columns across separate `Grid`s (the usual label + field form case).
- **Keep virtualization on** for long lists: `VirtualizingStackPanel.IsVirtualizing="True"`, `VirtualizationMode="Recycling"`. `ListBox`/`ListView` virtualize by default, `ItemsControl` does **not**, and wrapping a list in a `ScrollViewer` or `StackPanel` silently disables it.

### Styles, templates, resources

- **`Style` with `TargetType`** plus `BasedOn` to inherit; implicit styles (no `x:Key`) for a control type's default look.
- **`DataTemplate` for how data looks, `ControlTemplate` for how a control looks.** Never subclass a control just to change its appearance.
- **`StaticResource` by default**; `DynamicResource` only for resources that change at runtime (theming, language) — it costs more.
- **`Freeze()` shared brushes and geometries** that never change.
- **Respect the system theme.** The app follows Windows 11 light/dark and the system accent colour; never hardcode a colour a theme should supply.

### Validation

- **`INotifyDataErrorInfo`** on the ViewModel plus `ValidatesOnNotifyDataErrors=True` on the binding. It supports per-property and asynchronous validation, which `IDataErrorInfo` does not.
- Surface errors through `Validation.ErrorTemplate`, not a `MessageBox` raised from the ViewModel.

### Memory leaks

- **Unsubscribe (`-=`) every event you subscribe to** when a view or ViewModel goes away. Event handlers are the top cause of WPF leaks, and device watchers are long-lived publishers.
- For long-lived `PropertyChanged` subscriptions, use `PropertyChangedEventManager` (weak event pattern).
- Always `Stop()` and unsubscribe a `DeviceWatcher`; a running watcher keeps the whole object graph behind its handlers alive.

### Debugging bindings

- Set `PresentationTraceSources.TraceLevel=High` on a single binding to see why it fails to resolve.
- Binding failures show up in the debug output window — **a silently broken binding is still a bug**; check that window.

## Internationalization

Supporting multiple languages is a planned implementation step. Even before it is built, new code must be written i18n-ready — retrofitting localization is far more expensive than keeping it possible.

### Write-now rules

- **No user-facing string literals anywhere** — not in XAML, not in C#. Every label, button caption, tooltip, window title, validation message, and formatted message goes into `Properties/Resources.resx`. This is the one exception to the English-only rule: resource *values* get translated, resource *keys* stay English.
- **No string concatenation for sentences.** Use a composite format string with placeholders in the `.resx` (`"Connected to {0}"`) and `string.Format`, so translators can reorder. Never build a sentence from fragments — word order differs per language.
- **No pluralization by `if (count == 1)`.** Keep separate resource keys for the forms you need; a hand-rolled singular/plural branch is wrong in most languages.
- **Never hardcode number or currency formats.** Format with `CultureInfo.CurrentCulture` rather than a literal pattern. Dates and times are the exception: they always use the canonical `yyyy-MM-dd HH:mm:ss` format with `InvariantCulture` — see the Date and time format section.
- **Units of measure are a display concern, never a storage one.** Values stay SI internally (see the metric-units rule); if a culture needs different units shown, convert in the presentation layer and pull the unit symbol from the `.resx` alongside the formatted number.
- **Use `StringComparison.Ordinal` for internal keys and identifiers** — Bluetooth device ids, MAC addresses, GUIDs, AQS filter strings — and culture-aware comparison only for text shown to the user. Never use culture-sensitive comparison on protocol strings.
- **Layout must absorb text growth.** Translations commonly run 30-40% longer than English; this is the practical reason behind the no-fixed-`Width` rule in the layout section. Let text wrap or the container grow, and verify with the longest available translation.
- **Device names come from the device, not from resources.** Never translate or normalise a name the hardware reports; display it verbatim.

### When the feature is implemented

- **Resource strategy**: satellite assemblies from `Resources.resx` plus per-culture `Resources.<culture>.resx` (e.g. `Resources.es.resx`).
- `NeutralLanguage` is already set to `en-US` in `BlueH.csproj`; keep it in sync with the neutral `.resx`.
- **Set both cultures at startup** in `App.xaml.cs`: `CultureInfo.CurrentUICulture` selects resources, `CurrentCulture` drives formatting. They are separate and both matter.
- **Fix WPF's binding-culture default.** WPF formats bindings using the `Language` property, which defaults to `en-US` regardless of `CurrentCulture` — so numbers stay English until it is overridden once at startup: `FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)))`. This is the most commonly missed step in WPF localization.
- **Expose strings for binding, not `{x:Static}`.** `{x:Static p:Resources.Key}` resolves once and will not update if the culture changes at runtime. If runtime language switching is required, bind through a localization provider (a singleton with a string indexer that raises `PropertyChanged` on culture change); if only restart-time switching is needed, `{x:Static}` is acceptable and simpler. Decide this explicitly before building the feature.
- **Mirror the UI for RTL languages** by binding `FlowDirection` to the culture rather than leaving it `LeftToRight`, if an RTL locale is in scope.
- **Test with a pseudo-locale** before real translations exist: a `Resources.qps-ploc.resx` with accented, lengthened strings surfaces hardcoded text and clipped layout immediately.

## Testing / TDD

TDD is the intended workflow for everything below the view, and the service-interface boundary described in MVVM boundaries is what makes it possible — Bluetooth hardware must never be required to run the suite.

- **What is testable**: ViewModels, commands, domain models, and the logic inside services — constructed with `new`, exercised, asserted on properties and on `PropertyChanged`. XAML and code-behind are not unit-tested.
- **What is faked**: every WinRT and Core Audio call, behind the project's own interfaces. A test double returns domain models, so no device, no driver, and no pairing is involved.
- **Clock**: inject `TimeProvider` and use `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) in tests; never `DateTime.Now` in testable code.
- **Tooling**: NUnit with `Microsoft.NET.Test.Sdk`, NSubstitute for test doubles, and NUnit's constraint model (`Assert.That`).
- **No test project exists yet.** When one is added, create `BlueH.Tests\BlueH.Tests.csproj` targeting the same TFM as the app, add it to `BlueH.sln`, and run it with `dotnet test`. Test names are English and describe behavior, not method names.
- **Hardware verification is separate** from the unit suite: anything that needs a real headset goes in a manual checklist or an explicitly opt-in integration project, never in the default `dotnet test` run.

## Planned work

Ordered by dependency, not by priority. Each step builds on the one before it.

1. **Device discovery and status** — enumerate paired and connected Bluetooth audio devices with `DeviceInformation`/`DeviceWatcher` and show connection state live. This is the foundation every other feature reads from.
2. **Battery level** — from the GATT Battery Service for BLE devices and from the Windows device property for classic ones. Coverage is device-dependent, so the UI must treat "unknown" as a first-class state, never as zero.
3. **Audio endpoint control** — per-endpoint volume and mute, and switching the default playback and communication device. Core Audio interop.
4. **Media transport** — play, pause, next, previous via `Windows.Media.Control`.
5. **Pairing management** — pair, unpair, reconnect via `DeviceInformationPairing`.
6. **Internationalization** — see the Internationalization section; its write-now rules apply from step 1 onward.
7. **Vendor-specific features** — ANC, equaliser, touch controls, codec reporting. No Windows API exposes these: each is a proprietary protocol over RFCOMM or BLE GATT and must be implemented per vendor, behind the same service interfaces. Treat each vendor as an optional plug-in, never as a dependency of the core app.

Constraints to design around:

- **Windows exposes no public API for Bluetooth audio codec selection** (SBC/AAC/aptX/LDAC). Do not promise it in the UI until a concrete per-vendor route is proven.
- **Setting the default audio endpoint has no public API either.** The established route is the undocumented `IPolicyConfig` COM interface, which can break between Windows releases. Isolate it behind `IAudioEndpointService` so a future replacement touches one file.
- **Battery reporting depends on the headset**, not on the app. Some devices report over GATT, some only over the vendor protocol, some not at all.
