# Islet — Specification

**Islet** is a Windows 11 desktop recreation of the iOS "island" HUD (an adaptive pill at the top-center of
the display that hosts live activities), plus a window-parking system. It intentionally uses **no** Apple
names, logos, SF Pro / SF Symbols, or Apple assets. Typography is **Segoe UI Variable** (fallback Inter);
iconography is **Segoe Fluent Icons** (fallback Segoe MDL2 Assets).

> Units: `dp` = device-independent pixel at 96 DPI. On a monitor at scale *s*, `px = dp * s`.
> Tags: **[measured]** = taken from Apple reference imagery / community teardowns; **[estimate]** = read off
> reference video frame-by-frame or chosen by eye — all **[estimate]** values live in `config.json` and are tunable.

## 1. Primary sources

- Apple HIG — Live Activities / the top-center island (design states: compact leading/trailing, minimal, expanded).
- ActivityKit documentation (ActivityKit live activity presentation model) and WWDC "Live Activities" sessions.
- Community teardowns of the iPhone 14 Pro cutout for rest geometry.
- iOS 27 Siri-in-the-island reporting (rumored, 2026): a "Search or Ask" prompt with a glowing caret and a
  thin glowing running border, dark color scheme. Treated as **[estimate]** — see §6.

See `README.md` → *Sources* for links.

## 2. States

| State | Trigger | Shape | Content |
|-------|---------|-------|---------|
| **Idle** | nothing active | empty pill | — |
| **Compact** | one activity | pill, slightly wider; leading slot + trailing slot | icon + indicator (ring/number) |
| **Minimal** | two activities | pill + a detached circle, joined/split by a gooey (metaball) bridge | glyph in pill; dot/glyph in blob |
| **Expanded** | click / long-press / hover | large continuous-corner card that "flows" down | leading/trailing/center/bottom regions |
| **Alert** | event (call, timer, charging, low battery) | medium banner, auto-expands 2–4 s then collapses | glyph + title + trailing |
| **Ask** | hotkey (default **Win+Alt+Space**) | search-bar pill with glowing caret + running border | "Search or Ask" field |

## 3. Geometry (defaults in `config.json`)

| State | Width dp | Height dp | Corner radius dp | Note |
|-------|---------:|----------:|-----------------:|------|
| Idle | 126 | 37 | 18.5 (= h/2) | **[measured]** iPhone 14 Pro rest ≈ 126×37, top margin ≈ 11 |
| Compact | 188 | 37 | 18.5 | **[estimate]** |
| Minimal | 126 | 37 | 18.5 | + detached blob Ø30, gap 10 **[estimate]** |
| Expanded | 372 | 160 | 44 | **[estimate]** |
| Alert | 320 | 84 | 30 | **[estimate]** |
| Ask | 460 | 48 | 24 (= h/2) | **[estimate]** |

- Top margin: **11 dp [measured]**.
- Corner shape: a **continuous rounded rectangle** — a superellipse |x|ⁿ+|y|ⁿ=1 with exponent **n ≈ 4** **[estimate]**
  (`Layout.CornerExponent`), *not* a circular RoundedRect. At radius = height/2 this yields a continuous "stadium".
- Soft glow: colored by the active activity; blur radius **20 dp [estimate]**, strength 0.9 (0.22 when idle).

## 4. Timings (ms) **[estimate]**

| What | Value |
|------|------:|
| Content out (blur+scale-down+fade) | 130 |
| Content in (scale-up+fade) | 150 |
| Content in delay | 60 |
| Alert dwell before collapse | 3000 (2000–4000 range) |
| Ask caret blink period | 560 |
| Ask running-border sweep period | 2600 |

## 5. Springs

Expressed SwiftUI-style as `response` (period, s) + `dampingFraction` (ζ). Integrated with semi-implicit Euler
at 240 Hz substeps; retargeting preserves velocity, so every animation is interruptible with continuity.

| Spring | response | dampingFraction | Use |
|--------|---------:|----------------:|-----|
| Geometry | 0.45 | 0.76 | width / height / radius / position **[estimate]** |
| Content | 0.40 | 0.82 | content transforms **[estimate]** |
| Snappy | 0.32 | 0.86 | minimal split, small indicators **[estimate]** |

Slight overshoot is intentional (ζ < 1). All **[estimate]**, tuned by eye to the reference.

## 6. Animation rules

- Size, radius and position are animated with **springs**, never with opacity.
- On a content change the old content leaves with blur + scale-down + fade (~130 ms) and the new content
  arrives with scale-up + fade after a ~60 ms delay. Content is always clipped to the island shape.
  (Blur during the transition is **approximated** by scale+fade in this build — see `ASSUMPTIONS.md`.)
- Any animation is interruptible mid-flight; the spring continues from its current velocity.
- The frame loop is paced to the compositor via `DwmFlush` (60/120/144 Hz).

## 7. Ask mode (iOS 27 "Search or Ask", **[estimate]** / rumored)

- Hotkey (default Win+Alt+Space) morphs the pill into a search bar: a leading search glyph, a "Search or Ask"
  placeholder, a **blinking glowing caret**, and a **thin running border** (a bright "comet" that travels the
  contour, period 2600 ms).
- Input is dispatched to a configurable handler: `WindowsSearch` (default), `Command` (run a program), or
  `Http` (POST the query to a local AI endpoint). See `BehaviorConfig`.

## 8. Content providers (`ILiveActivity`)

Plugins publish renderer-agnostic `ActivitySnapshot`s (title/subtitle/progress/glyph/accent/levels/artwork);
the renderer maps them to the compact / minimal / expanded templates. Planned set:

- **Media** via SMTC (`Windows.Media.Control`): works with **any** app that reports to the System Media
  Transport Controls — SoundCloud (browser or PWA), Spotify, YouTube/Chrome/Edge, Media Player, VLC, etc.
  Picks the actively-playing session across services, labels the source, and shows title/artist, artwork,
  live progress and play state.
- **Timer / stopwatch / countdown**.
- **System HUD**: volume, brightness, Caps Lock, Wi-Fi, Bluetooth/headphones, charging / low battery.
- **Notifications** (`UserNotificationListener`), download/copy progress, screen-recording / mic / camera indicators.

Priority order (`ActivityPriority`): Alert > High > Normal > Low. The highest-priority active activity owns the
compact slot; a second one splits off as the Minimal blob.

## 9. Window & system behavior

- One topmost window: `WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST`. Not activatable,
  absent from Alt+Tab and the taskbar. Clicks pass through transparent pixels (shape hit-test in WM_NCHITTEST).
- Per-Monitor-V2 DPI (manifest); live DPI changes (WM_DPICHANGED) and resolution changes (WM_DISPLAYCHANGE).
- Hidden during fullscreen apps / games (`SHQueryUserNotificationState`) and on the lock screen (session switch).
- No administrator rights.

See `ASSUMPTIONS.md` for every decision and estimate, and `README.md` for the self-check status.
