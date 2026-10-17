# BlueH — Desktop Widget Plan

A small always-visible window showing the headphones' state at a glance, in the spirit of the
Windows 7 desktop gadgets but without their security model. The main window's design is planned
in `gui-redesign-plan.md` and platform work in `implementation-plan.md`; this file covers only
the widget.

Last updated: 2026-10-17

## 1. Why

Battery, noise control mode and the current track are the things a user checks most often, and
today each check means opening the full window and the device page. A widget keeps them in view.

## 2. Decisions (2026-10-17)

- **A native WPF window inside BlueH, not a web page.** No local web server, no browser engine,
  no network access, and no third-party code. Windows 7 gadgets were withdrawn in 2012 because
  each one was web code running with the user's full rights; a WPF window in the same process
  adds no new attack surface.
- **Not a Windows 11 Widgets board provider.** That needs MSIX packaging, Adaptive Card
  templates instead of XAML, and is only visible while the board is open.
- **Translucent at rest, solid on hover.** Opacity 0.85 at rest, 1.0 while the mouse is over the
  widget. Both values are named tokens in `Themes/Layout.xaml`, never literals in the view, which
  `XamlDesignTokenTests` already enforces.
- **Solid while it has keyboard focus too**, so a keyboard user is never reading a translucent
  widget. Opacity is a presentation concern and is driven by a style trigger, not the ViewModel.
- **It reuses the existing ViewModels and services.** No second Bluetooth session: the widget
  and the device page must share one noise control session, since the earbuds and the platform
  findings show that every open RFCOMM channel keeps the link up.

## 3. Questions settled (2026-10-17)

The user asked for the whole plan in one go, so the simplest choice was taken for each, and each
is cheap to change:

1. **The widget follows the first connected device.** Pinning a device can come later.
2. **Closing the main window closes everything** (`ShutdownMode="OnMainWindowClose"`). This makes
   the tray icon unnecessary, so W5 was not built.
3. **It is opened from Settings** ("Desktop widget", remembered across sessions) and hidden
   with its own close button, which turns the same setting off.

## 4. Risks to spike first (W0)

- **`AllowsTransparency="True"` requires `WindowStyle="None"`**, so the widget draws its own
  frame and must handle dragging itself.
- **Layered windows and the Fluent theme**: whether the theme's brushes and the Mica backdrop
  still render correctly in a transparent window, or whether the widget needs its own opaque
  card background.
- **Rendering cost** of a layered window on this machine, and behaviour at 150 % DPI and across
  two monitors.
- **`Topmost`** against full-screen applications and the taskbar.

### W0 findings (2026-10-17)

A throwaway WPF window under `ThemeMode="Dark"`, transparent, topmost, at opacity 0.85, captured
from the screen:

- **The card brush cannot be the widget's background.** `CardBackgroundFillColorDefaultBrush`
  is `#0DFFFFFF`, 5 % white, so on a transparent window the desktop shows straight through, and
  `CardStrokeColorDefaultBrush` (`#19000000`) is invisible on a dark background. The widget uses
  `SolidBackgroundFillColorBaseBrush` (`#FF202020`, opaque) and `SurfaceStrokeColorDefaultBrush`
  (`#66757575`), and the window's opacity does the translucency.
- `AcrylicBackgroundFillColorDefaultBrush` does not exist in WPF's Fluent theme.
- Text, radio buttons and the theme's text styles render correctly in a layered window.
- Only 100 % scaling could be checked; this machine has a single monitor at 100 %.

### Built (2026-10-17)

- **`SharedNoiseControlService`** wraps the OPPO service and hands out one shared connection per
  device to every user (the device page and the widget), closing it when the last user leaves.
  A change made by one user is reported to the others; a lost connection closes every user and
  is not reused; one user giving up while the connection opens does not drop it for the rest.
- **`WidgetViewModel`** follows the first connected device, shows its battery, noise control and
  the current track, and suspends its noise control when the same device is being disconnected
  or unpaired from its page, since any open channel keeps the link up.
- **`WidgetPlacement`** keeps at least 48 DIP of the widget on the virtual screen, so a widget
  remembered on a monitor that is gone comes back within reach.

## 5. Increments

| # | Increment | Deliverable | Tests |
|---|---|---|---|
| W0 | Spike | A throwaway transparent topmost window with a theme card, checked at two DPI settings | none; findings written here |
| W1 | Read-only widget | Borderless window: device name, battery, noise control mode, current track; drag to move | ViewModel tests; screenshot |
| W2 | Opacity | 0.85 at rest, 1.0 on hover or keyboard focus, values as tokens | Token test; screenshot at rest and on hover |
| W3 | Position and always on top | Remembers its position per monitor, always-on-top toggle, returns on screen if its monitor is gone | Position clamping tests; persistence round trip |
| W4 | Noise control from the widget | The three modes, sharing the device page's session | Shared-session test: one open per device |
| W5 | Tray icon (optional) | Show or hide the widget and the main window | Screenshot |
