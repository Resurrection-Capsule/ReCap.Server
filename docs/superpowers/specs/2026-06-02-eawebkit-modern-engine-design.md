# EAWebKit modern engine (MiniBlink behind the real View) — design

> Status: design, 2026-06-02. Replaces EAWebKit's ~2008 WebCore renderer with a modern
> engine (MiniBlink, Blink ~2016) so the in-game web UI accepts modern CSS/HTML/JS — while
> keeping EA's real `View`/`ISurface` ABI (no exe patch, no clean-room ABI reimplementation).
> Enabled by the in-source EAWebKit build we now own (see the redirect work,
> `2026-06-02-eawebkit-redirect-reimpl-design.md`).

## Goal

A modern web rendering engine drawing into Darkspore's in-game web surface, so newer CSS
(grid, flexbox, web fonts), HTML, and JS (ES2016+, fetch) render correctly. The page
*content* is flexible — stock Darkspore pages first (to validate), modern HTML later. Not
tied to React/Vite specifically; the deliverable is "modern web standards render in-game."

> Side-quest note: this is independent of the D-010 single-player/Dungeon critical path.
> A deliberate, opt-in effort.

## Why "inside the real EAWebKit.dll" (chosen architecture)

The exe drives the web UI entirely through EAWebKit's `IEAWebkit`/`View`/`ISurface` vtables.
We now **build EAWebKit.dll from source**, so instead of reimplementing that ABI from
scratch (the `ReCap.WebShim` clean-room route — risky: vtable order, EASTL struct layout),
we keep EA's **real** `View` object (whose ABI matches the exe by construction) and swap only
its *renderer*.

Key enabler discovered in source: `View` cleanly separates
- `mpSurface` — the ARGB `EA::Raster::ISurface` the exe reads via `GetSurface()` → **kept**;
- `mpWebView` — the WebCore engine that paints `mpSurface` → **replaced by MiniBlink**.

So we swap the painter, not the shell. Two risks the clean-room shim carried vanish:
1. **vtable/ABI matching** — we *are* the real View.
2. **JS-value EASTL structs** (`JavascriptMethodInvokedInfo` et al.) — we use the *real*
   types compiled by the real toolchain, not a hand-mirrored layout.

Rejected: clean-room `ReCap.WebShim` (ABI risk, redundant now); exe-Detour of
`cWebView`/`WebView_UpdateTexture` (more exe hooking, no gain over editing the DLL we ship).

## Engine — MiniBlink

`weolar/miniblink49` — Blink ~2016, 32-bit single DLL (matches the x86 EAWebKit.dll),
pure-offscreen paint via `wkeOnPaintBitUpdated`. **Prebuilt** (`node.dll` + `wke.h`) — we
consume it, no VC9 recompile of the engine. Runs typical modern CSS/HTML. CEF rejected
(heavy ~150MB, dated 32-bit builds); Ultralight rejected (x64-only).

## Build toggle & loading (decided)

- **Compile-time flag `RECAP_MINIBLINK`**: when defined, the View routes to MiniBlink and
  WebCore (`mpWebView`) is bypassed. A non-flagged build is stock WebCore. (Simpler/cleaner
  than a runtime switch; revisit a runtime `engine=` cfg later if both paths are wanted.)
- **Dynamic loading via `GetProcAddress`**: `LoadLibrary("node.dll")` + resolve the ~12 `wke*`
  functions we use. No import lib. If the DLL/symbols are missing, the backend reports
  unavailable and the View falls back to stock WebCore (graceful).

## Components

### `source/recapwke.{h,cpp}` (new) — the MiniBlink backend wrapper
Isolates ALL MiniBlink glue. Engine-agnostic-ish C++ API consumed by the View:
- `bool RecapWke::Available()` — node.dll loaded + symbols resolved.
- `Handle Create(int w, int h, PaintCb cb, void* user)` — `wkeCreateWebView` + `wkeResize` +
  `wkeSetTransparent` + `wkeOnPaintBitUpdated(cb)`.
- `LoadURL(h, const char* url)` → `wkeLoadURL`.
- `Resize(h, w, h2)` → `wkeResize`.
- `Tick(h)` → pump MiniBlink's message/timer loop.
- `RunJS(h, const char* js)` → `wkeRunJS`.
- `BindJs(h, const char* name, thunk, user)` → `wkeJsBindFunction`.
- `FireMouse/FireKey*(...)` → `wkeFireMouseEvent`/`wkeFireKeyUp/Down/Press`/`wkeFireMouseWheelEvent`/`wkeSetFocus`.
- Resolves wke entry points into a struct of function pointers on first use.

### `source/EAWebKitView.cpp` (edit) — route the live View methods, behind `#ifdef RECAP_MINIBLINK`
- `InitView`: keep creating `mpSurface` (ARGB). Instead of using `mpWebView`, `RecapWke::Create`
  a view at the size with a paint callback that copies the engine buffer into
  `mpSurface->GetData()` (see data flow). Skip WebCore bring-up.
- `SetURI` → `RecapWke::LoadURL`. `SetSize` → resize surface + `RecapWke::Resize`.
- `Tick` → `RecapWke::Tick` (the paint callback has already written the surface).
- `GetSurface` → unchanged (`mpSurface`).
- `CreateJavascriptBindings`/`RegisterJavascriptMethod` → `RecapWke::BindJs`; the thunk builds a
  **real** `JavascriptMethodInvokedInfo` (`mMethodName`/`mArguments`/`mReturn`) and calls the
  stored `ViewNotification::JavascriptMethodInvoked`.
- `EvaluateJavaScript` → `RecapWke::RunJS`.
- `On*Event` → `RecapWke::FireMouse/FireKey*`.

## Data flow (per frame)

```
exe Tick() -> View::Tick() -> RecapWke::Tick() -> MiniBlink renders
   -> wkeOnPaintBitUpdated(buffer,w,h,dirty) -> memcpy into mpSurface->GetData()
exe -> View::GetSurface()->GetData()/GetStride() -> blits ARGB into the D3D9 "WebView" texture
   (ClientWeb::WebView_UpdateTexture@0x00ca4e90, unchanged) -> renderWebView pass draws it
```
Pixel format: EA `kPixelFormatTypeARGB` is `0xAARRGGBB` = BGRA byte order on little-endian;
MiniBlink's `wkeOnPaintBitUpdated` buffer is also BGRA (Windows DIB) → expect a direct
`memcpy` (verify; add a channel swap only if colors are off). Honor `GetStride()` when widths
differ.

## JS bridge

`RegisterJavascriptMethod(name)` registers each of the 42 names (`callSporeNet`,
`getNucleusAuthToken`, …) with `wkeJsBindFunction`. On a JS call, our thunk maps wke args →
the real `JavascriptMethodInvokedInfo` and invokes the host's `ViewNotification`. The game
handles it and returns to JS via the same struct. Because we compile against the real
EAWebKit headers/EASTL, these structs are binary-correct with no manual layout work.

## Networking (synergy with the redirect work)

MiniBlink uses its own modern HTTP/TLS stack → modern web content loads correctly and the
old DirtySDK cipher problem disappears for web traffic. The process-wide Detours redirect
(today's work) still routes MiniBlink's sockets to ReCap (`recap.cfg` host/port), and
MiniBlink's own cert handling supersedes the DirtySDK cert bypass for web content.

## Phasing (each phase has a runtime gate)

1. **Pixel path + modern CSS** — MiniBlink renders a static modern test HTML (grid/flex/web
   font) into the in-game surface. **Gate:** the modern page appears in-game.
2. **JS bridge** — wire the 42 callbacks; point `SetURI` at the stock pages. **Gate:** login/
   lobby pages work end-to-end (`callSporeNet`/`getNucleusAuthToken` round-trip).
3. **Input** — forward `On*Event` to wke. **Gate:** clicking/typing works.
4. **Modern content** — author modern HTML/CSS (or a built SPA) and point the view at it
   (served from ReCap on `127.0.0.1`, or local). **Gate:** the modern UI runs in-game.

## Dependency

MiniBlink 32-bit SDK (`node.dll` + `wke.h`) from a `weolar/miniblink49` release. Place under
e.g. `third_party/miniblink/` (header for include; `node.dll` shipped next to `EAWebKit.dll`).
Acquired like Detours — the user downloads the release.

## Risks / open items

- **Paint format/stride** — verify BGRA↔ARGB and stride; fix with a swap/row-copy if needed.
- **MiniBlink in-proc with the game's D3D9/CRT** — MiniBlink is self-contained (own Chromium
  process model in `node.dll`); confirm it coexists with the game (memory, threads). Validate
  in Phase 1.
- **wke API surface** — confirm the exact `wke*` names/signatures against the chosen release's
  `wke.h` before coding the resolver.
- **JS arg types** — map wke `jsValue` ↔ the host's `JavascriptValue` for the bridge args.
- **Toolchain** — `recapwke.cpp` compiles in VC9 with the EAWebKit project; `node.dll` is
  prebuilt, only loaded at runtime (no link dependency).
