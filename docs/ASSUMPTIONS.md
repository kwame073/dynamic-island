# Islet — Assumptions & Decisions

Everything here is a deliberate engineering decision or an estimate, recorded as the project evolved.
Estimate values are also surfaced in `config.json` so they can be tuned.

## Stack decisions

- **.NET 9 / WPF.** The installed SDK is 10.0.400 but the projects target `net9.0`
  (`net9.0-windows10.0.19041.0` for the app, to get WinRT projections). The `net9.0` reference packs
  restore fine under SDK 10.
- **Rendering = layered window + SkiaSharp + `UpdateLayeredWindow`.** Rather than a WPF
  `AllowsTransparency` window, the island is a borderless Win32 layered window; each frame is drawn by
  Skia into a top-down 32-bpp premultiplied-BGRA DIB section and blitted with `UpdateLayeredWindow`.
  This gives true per-pixel alpha (soft glow + antialiased squircle over any desktop content), full
  control for the gooey metaball, and avoids WPF transparency's software-rendering penalty. The frame
  loop runs on a dedicated STA thread, paced to the compositor with `DwmFlush` (60/120/144 Hz).
- **Interop:** `CsWin32` is referenced and used for leaf calls (`SHQueryUserNotificationState`,
  `DwmFlush`, `GetDpiForWindow`). The window/GDI/`UpdateLayeredWindow` core is **hand-written P/Invoke**
  in `Interop/Native.cs` for predictability — a deliberate reliability tradeoff, easy to migrate later.
- **WinForms** is enabled solely for the tray `NotifyIcon` (simplest reliable tray under WPF).

## Geometry / timing / springs

- Rest size **126×37 dp**, top margin **11 dp** — iPhone 14 Pro reference **[measured]**.
- All other sizes (compact/expanded/alert/ask) are **[estimate]** read off reference imagery; tunable.
- Corner is a **superellipse** (continuous), exponent **4.0** chosen by eye — rounder than iOS's ~5 so the
  pill ends read as a stadium while the expanded card keeps continuous corners. Tunable `CornerExponent`.
- Springs: Geometry `response 0.45 / damping 0.76`, Content `0.40 / 0.82`, Snappy `0.32 / 0.86` — all
  **[estimate]** in a natural range, slight overshoot. Integrated semi-implicit at 240 Hz
  substeps; retargeting preserves velocity (interruptible with continuity).
- Timings (content 130/150 ms, alert dwell 3 s, caret 560 ms, border sweep 2600 ms) are **[estimate]**.

## Content-transition blur

The design calls for old content to leave *with blur* and new content to arrive *from blur*. True per-layer
Gaussian blur each frame is expensive on a CPU raster target, so the transition is **approximated** by
scale + fade (scale-down-out / scale-up-in). The hook (`ContentLayer.BlurPx`) exists to add real blur later.

## Ask mode

- iOS 27 "Search or Ask" is **rumored** (2026 reporting); treated as **[estimate]** for visuals (glowing
  caret, thin running border, dark scheme).
- Typing is captured by briefly letting the window take focus (clearing `WS_EX_NOACTIVATE` +
  `SetForegroundWindow`) while Ask is open, then restoring. This can be refused by the OS foreground lock
  in rare cases; a global keyboard hook is the fallback if that proves unreliable in the field.
- Default handler is **WindowsSearch** (`search-ms:` protocol, falling back to a web search). `Command`
  and `Http` (POST `{"query":…}` to a local AI endpoint) are also wired.

## Not implemented yet

- **Window parking** (stashing windows to the screen edge / into the island): the detailed interaction
  design is still being nailed down, so it is not implemented yet — only listed in the roadmap. No behavior
  was invented. `docs/SPEC.md` covers the island itself.
- **Content providers implemented:** Media (SMTC), Battery (charging/low), Volume HUD, Mic/Camera privacy
  indicator, Timer, Caps-Lock HUD. **Not yet:** brightness, Wi-Fi/Bluetooth/headphones, notification
  mirroring (needs packaged identity / MSIX for `UserNotificationListener`), download/copy progress.
  The `ILiveActivity` plugin surface is ready for them.
- **ARM64**: configured for publish; validated by build on x64 so far.
- **Settings UI**: config is JSON only (`%APPDATA%/Islet/config.json`); no GUI yet.
