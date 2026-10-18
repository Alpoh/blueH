# BlueH — Desktop Widget Plan

A small always-visible window showing the headphones' state at a glance, in the spirit of the
Windows 7 desktop gadgets but without their security model. The main window's design is planned
in `gui-redesign-plan.md` and platform work in `implementation-plan.md`; this file covers only
the widget.

Last updated: 2026-10-18

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
2. ~~Closing the main window closes everything, and the widget is opened from Settings.~~
   Replaced the same day after the user tried it; see below.

### Revised: the widget is a display mode (2026-10-17)

- **The widget replaces the main window rather than sitting beside it.** A button at the top
  right of the main window hides it and shows the widget; double-clicking the widget, or its
  expand button, brings the main window back. `DisplayModeViewModel` owns the switch, so only
  one of the two is ever visible.
- **The widget's close button exits the app.** The widget shows in the taskbar, so there is
  always a way back to it, and the tray icon (W5) is still not needed.
- **BlueH always starts in the full window.** The Settings toggle for the widget was removed.
- **Contents:** device name and battery; noise control as three icon radio buttons (off,
  transparency, noise cancelling); the current track with previous, play/pause and next; and a
  volume row for the followed device's default output.
- **Text colour is set on the widget's own style.** The widget's window style is not based on
  the theme's `Window` style, so it inherited black text until `WidgetWindowStyle` set the
  foreground.

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

W0 to W4 are built; W5 is not, since the widget shows in the taskbar.

## 6. Hand verification (2026-10-17 and 2026-10-18)

Against the OPPO Enco Air5 and the Sony SRS-XE300, all confirmed working:

- The switch button hides the main window and shows the widget; double-clicking the widget
  returns to the main window.
- Text is readable, the three noise control icons show, and transparency set from the widget
  reaches the earbuds.
- Dragging moves the widget, and it comes back to the same place after switching modes and
  after restarting BlueH.
- Always on top keeps it above other windows, and turning the setting off lets them cover it.
- After changing the noise control mode from the widget, Disconnect on the device page really
  drops the link, and Connect brings back noise control and the volume row.
- The close button exits the app.
- The volume row appears when the earbuds connect while BlueH is open, and follows the keyboard
  volume and mute keys; dragging the slider does not jump back.

Not checked: other DPI settings or a second monitor (this machine has one monitor at 100 %).

## 7. Bugs found while verifying (2026-10-18)

Both were platform timing issues rather than widget code, and both are fixed for every page,
not only the widget. Details and measurements are in `implementation-plan.md`, increments 3.10
and 3.11.

- **The volume row was missing when the earbuds connected after BlueH started.** Windows creates
  the audio endpoint about 1.2 s after Bluetooth reports the connection, and the outputs were
  read only at the connection. They now refresh whenever Windows adds or removes an endpoint.
- **Volume changed outside BlueH was not shown.** The levels now follow Core Audio's volume
  notifications.
