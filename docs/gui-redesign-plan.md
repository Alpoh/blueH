# BlueH — GUI Redesign Plan

The visual and interaction design of the app. Platform and domain work (devices, battery, audio, pairing, vendor protocols) is planned separately in `implementation-plan.md`; this file covers only how the app looks and is navigated.

Last updated: 2026-10-11

## 1. Why

The window works but reads as a developer tool. Measured against a screenshot of the running app:

1. The diagnostics log takes half the window — the largest thing on screen is internal information.
2. Everything is a text table. Status, kind and battery are words ("Disconnected", "Unknown") where a user expects a status dot, a device silhouette and a battery glyph.
3. One long page with no hierarchy: devices, pairing, settings, playback and outputs are stacked with equal weight.
4. Technical endpoint names appear with no device selected, and the default-device actions look like form buttons.
5. Empty states are loose sentences rather than designed states.

## 2. Direction

Model the app on **Windows 11 Settings → Bluetooth & devices**: a navigation rail, cards, Segoe Fluent Icons, generous spacing. Native, consistent with the Fluent dark theme the app already uses, and free of third-party UI libraries.

Decisions taken (2026-10-11):

- **Navigation is the foundation of the redesign.** The side menu and a device page with Back, deferred during phase 5, return as increment D1.
- **Diagnostics hide behind a developer-mode setting.** Logging never stops; only the panel is hidden.
- **Playback becomes a fixed mini-player bar** at the bottom of every page, with album art.

## 3. Rules that carry over

- Colours come from the Fluent theme's resources, never a literal. Spacing, type sizes and corner radii come from one tokens dictionary.
- Icon glyphs live in one C# class and are referenced with `{x:Static}`, so the no-literal-text test keeps passing and every glyph has a name.
- Every visual increment is verified by a before/after screenshot of the running window. Unit tests cannot see clipped text or broken spacing.
- Presentation logic (which glyph for which battery level, navigation state) is ViewModel or converter code and is unit-tested like everything else.

## 4. D0 findings (2026-10-11)

Spike against the running Fluent theme, by enumerating the application resources and querying DWM:

- **The window already has Mica.** `DWMWA_SYSTEMBACKDROP_TYPE` reads 2 on a plain WPF window under `ThemeMode="Dark"`. Nothing to implement; pages only need to stop painting opaque backgrounds over it.
- **The theme ships the WinUI type ramp**: `CaptionTextBlockStyle`, `BodyTextBlockStyle`, `BodyStrongTextBlockStyle`, `SubtitleTextBlockStyle`, `TitleTextBlockStyle`, `TitleLargeTextBlockStyle`, `DisplayTextBlockStyle`. BlueH defines no font sizes of its own.
- **The theme ships the semantic brushes**: `TextFillColorPrimary/Secondary/TertiaryBrush`, `SystemFillColorSuccess/Caution/CriticalBrush` with their background variants, `CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`, `AccentFillColorDefaultBrush`. Status and battery colours come from these.
- **The theme ships `ControlCornerRadius`, `OverlayCornerRadius` and `SymbolThemeFontFamily`** (the Segoe Fluent Icons font).

What D0 added: `Themes/Layout.xaml` with the spacing scale, named margins, `CardBorderStyle` and `SecondaryTextStyle`. Every view now uses the theme's text styles. `XamlDesignTokenTests` fails the build on a literal font size, weight, family, opacity or colour in a view. Spacing tokens are adopted by convention rather than enforced, since D1 to D3 rewrite the views anyway.

D0 is nearly invisible on screen by design: headers moved from a hand-set 18 to the theme's 20, and secondary text uses the theme's brush instead of opacity.

## 5. Increments

| # | Increment | Deliverable | Verification |
|---|---|---|---|
| D0 | Design foundations | Tokens dictionary (spacing scale, type ramp, corner radii), `Glyphs` class, existing views moved onto the tokens. Spike: which Fluent brush keys WPF ships, and whether the window gets the Mica backdrop | Tests enforcing no colour or font-size literals in views; screenshot |
| D1 | Shell and navigation | Navigation rail (Devices, Audio, Settings), content frame, Back | Navigation ViewModel tests; screenshot |
| D2 | Device cards | Card per device: kind glyph, name, status dot, battery glyph and percentage, unknown shown as a dash with a tooltip; quick Connect; designed empty state | Glyph and status selection tests; screenshot |
| D3 | Device page | Header with large glyph, status and battery; connection; noise-control segmented selector (phase 7.3 lands here); the device's outputs; "use for music / calls" toggles; unpair in a danger zone | ViewModel tests; screenshot |
| D4 | Mini-player | Fixed bottom bar with icon buttons and album art | Thumbnail mapping tests; screenshot |
| D5 | Add-device flow | Dedicated flow: search with progress, results, PIN challenge as a card | Screenshot |
| D6 | Settings page | Language, developer mode, About (version, Gadgetbridge credit and licence) | Tests |
| D7 | Diagnostics page | Visible only in developer mode | Visibility test |
| D8 | Polish | Automation names on icon buttons, keyboard navigation, 150 % DPI and pseudo-locale passes | Screenshots at several DPI settings and in `qps-ploc` |
