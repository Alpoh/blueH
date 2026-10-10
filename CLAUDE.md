# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

BlueH is a WPF desktop application (`BlueH.csproj`) targeting **.NET Framework 3.5**, built with the classic (non-SDK-style) MSBuild project format. It is currently just the default WPF project template — `App.xaml`/`App.xaml.cs` as the application entry point and `MainWindow.xaml`/`MainWindow.xaml.cs` as the single window — with no custom logic added yet.

## Build and run

This is a legacy .NET Framework project (not .NET Core/5+), so it is built with `msbuild`/Visual Studio tooling, not the `dotnet` CLI.

```
msbuild BlueH.sln /p:Configuration=Debug
msbuild BlueH.sln /p:Configuration=Release
```

Run the built executable directly:

```
BlueH/bin/Debug/BlueH.exe
```

There is no test project in the solution.

## Architecture notes

- `BlueH.sln` contains a single project, `BlueH\BlueH.csproj`.
- `RootNamespace`/`AssemblyName` is `BlueH`; the WPF startup object is `App` (`App.xaml` → `StartupUri="MainWindow.xaml"` convention via `App.xaml.cs` partial class).
- `MainWindow.xaml` / `MainWindow.xaml.cs` is the only window and currently has an empty `Grid`.
- `Properties/AssemblyInfo.cs` and `Properties/Resources.Designer.cs` (generated from `Resources.resx`) follow the standard WPF project template layout.
- `bin/` and `obj/` are git-ignored build output/intermediates.

## C# / WPF conventions

- **Naming**: `PascalCase` for classes, methods, properties, and public fields; `camelCase` for locals and parameters; private fields as `_camelCase`. One public class per file, filename matches the class name.
- **MVVM over code-behind**: keep `MainWindow.xaml.cs` (and any future window/`UserControl` code-behind) limited to view-only concerns (wiring up `InitializeComponent`, simple UI event glue). Put business logic, state, and commands in a `ViewModel` and bind to it via `DataContext` rather than manipulating controls directly from code-behind.
- **Data binding**: prefer XAML `{Binding}` over setting control properties imperatively in C#. Every ViewModel should derive from `BlueH.ViewModels.ViewModelBase` (`BlueH/ViewModels/ViewModelBase.cs`), which implements `INotifyPropertyChanged` and exposes `SetField<T>(ref field, value, propertyName)` for property setters and `OnPropertyChanged(...)` for manual notification. Note: the project targets .NET Framework 3.5, so `CallerMemberName` is unavailable — property name must be passed explicitly (string or `Expression<Func<T>>` overload).
- **Commands over click handlers**: for new interactive controls, prefer an `ICommand` bound via `Command="{Binding ...}"` instead of a `Click`/event handler in code-behind, once a ViewModel layer exists.
- **Async**: use `async Task` and `await`; only use `async void` for top-level UI event handlers. Never block on async code with `.Result` or `.Wait()` on the UI thread.
- **Resource cleanup**: wrap `IDisposable` resources (streams, file handles, etc.) in `using`/`using var`.
- **XAML organization**: as the UI grows, factor shared styles/brushes/templates into `ResourceDictionary` files (e.g. under an `App.xaml` merged dictionary) instead of repeating inline styles per control.
- **Avoid magic values**: pull repeated strings/numbers into constants or resources rather than inlining them across XAML/C#.
