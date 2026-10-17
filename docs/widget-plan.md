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

## 3. Open questions

To settle before or during W1:

1. **Which device the widget follows** when more than one is connected: the first connected
   one, or one the user pins from its device page.
2. **App lifetime**: whether closing the main window closes the widget too, or whether the
   widget keeps BlueH running. The second makes the tray icon (W5) necessary, since there would
   otherwise be no way back to the main window.
3. **How it is opened**: a button on the device page, a setting, or both.

## 4. Risks to spike first (W0)

- **`AllowsTransparency="True"` requires `WindowStyle="None"`**, so the widget draws its own
  frame and must handle dragging itself.
- **Layered windows and the Fluent theme**: whether the theme's brushes and the Mica backdrop
  still render correctly in a transparent window, or whether the widget needs its own opaque
  card background.
- **Rendering cost** of a layered window on this machine, and behaviour at 150 % DPI and across
  two monitors.
- **`Topmost`** against full-screen applications and the taskbar.

## 5. Increments

| # | Increment | Deliverable | Tests |
|---|---|---|---|
| W0 | Spike | A throwaway transparent topmost window with a theme card, checked at two DPI settings | none; findings written here |
| W1 | Read-only widget | Borderless window: device name, battery, noise control mode, current track; drag to move | ViewModel tests; screenshot |
| W2 | Opacity | 0.85 at rest, 1.0 on hover or keyboard focus, values as tokens | Token test; screenshot at rest and on hover |
| W3 | Position and always on top | Remembers its position per monitor, always-on-top toggle, returns on screen if its monitor is gone | Position clamping tests; persistence round trip |
| W4 | Noise control from the widget | The three modes, sharing the device page's session | Shared-session test: one open per device |
| W5 | Tray icon (optional) | Show or hide the widget and the main window | Screenshot |
