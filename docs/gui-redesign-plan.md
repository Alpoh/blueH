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

## 5. Landing page (D1 with the core of D2), 2026-10-12

At the user's request the landing page came first: what the app shows when it opens.

- **Shell**: navigation rail (Devices, Audio outputs, Settings, Diagnostics), page title with Back, and the mini-player fixed at the bottom. `PageNavigator` holds the history: a menu section starts a fresh history, opening a device pushes onto it. Pages are ViewModels shown through typed `DataTemplate`s.
- **Devices as cards**: kind glyph, name, status dot (theme success colour when connected), battery glyph and percentage, a dash with a tooltip when the battery is not reported. A card is a Fluent `Button`, so hover and press states come from the theme.
- **Device page**: opening a card shows the device's actions and its own outputs; the Audio outputs page lists every output. Two `EndpointsViewModel` instances, one filtered and one not.
- **The app always opens on the list.** The persisted selection was removed: with navigation it would have left a device selected that a click could no longer open. An older `settings.json` carrying `selectedDeviceId` still loads.
- **Mini-player** reduced to title, artist and icon buttons; the play/pause glyph follows the playback state.
- **Verification**: screenshots taken by launching the build and, for the device page, invoking the card through UI Automation by its accessible name.

Not yet done from D2: an empty state with an "Add a device" action, and the quick Connect button on the card. The selected menu item still uses the theme's solid accent block rather than Windows Settings' subtle pill (D8).

## 6. Device page (D3), 2026-10-12

- **Header card**: large kind glyph, status dot, battery glyph and percentage or "Battery level not available", and only the action that applies — Connect (accent button) when disconnected, Disconnect when connected, Cancel while an attempt runs. The status message appears only while there is one.
- **Outputs as cards**: mute is an icon toggle whose glyph and checked state follow the mute state; slider and the double-click-to-type volume from increment 3.8 kept unchanged; being the default for playback or calls is a check badge, and the "use for" button only appears while the output is not already the default.
- **Removal card** at the bottom: "Remove this device", an explanation, and Unpair in the theme's critical colour, with the confirmation inline.
- The old `SelectedDeviceView` was split into `DeviceHeaderView` and `DeviceRemovalView`. Output cards are shared with the Audio outputs page, so that page got the same design.
- Wording: "This device does not report its battery level" was wrong for earbuds that are merely disconnected; it now reads "Battery level not available".
- The noise-control selector belongs in this page and lands with phase 7.3.

## 7. Increments

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
