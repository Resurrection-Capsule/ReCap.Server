# WebView Shim — kickoff brief for a fresh instance (Phase 0)

Hand this to another Claude Code instance to scaffold the shim. **Read first:** [`WEBVIEW_SHIM_DESIGN.md`](WEBVIEW_SHIM_DESIGN.md) (org + ABI + phased plan) and [`WEBVIEW_MODERNIZATION.md`](WEBVIEW_MODERNIZATION.md) (Ghidra map + seams). This brief is the actionable subset for **Phase 0 only** (the ABI smoke test). Do NOT wire any engine yet (that's Phase 1).

## Goal of Phase 0
Build a 32-bit `EAWebKit.dll` that exports `CreateEAWebkitInstance @1` and implements just enough of the `IEAWebkit`/`View`/`ISurface` vtables to make `Darkspore.exe` show a **solid-color rectangle** where the login web UI normally is. That single result proves the entire ABI + texture path. No web engine, no JS, no networking.

## CRITICAL: the ABI comes from the real headers, not from the docs
The docs *describe* the interfaces; they are NOT the source of truth. **Copy these files verbatim** from `C:\CodingProjects\Personal\eawebkit\` into the project's `abi/` folder and `#include` them — do not retype or infer signatures:
- `include/EAWebKit/EAWebKit.h` (IEAWebkit, Parameters)
- `include/EAWebKit/EAWebKitView.h` (View, ViewParameters)
- `include/EAWebKit/EAWebKitViewNotification.h` (ViewNotification, JavascriptMethodInvokedInfo)
- `include/EAWebKit/EAWebKitJavascriptValue.h` (JavascriptValue)
- `include/EARaster/EARaster.h` (ISurface, PixelFormat)
- `include/EAWebKit/EAWebKitPlatformSocketAPI.h`
- plus any headers these transitively include (EABase, the EASTL fixed-size wrappers). Pull whatever is needed so the headers compile standalone.
- the export name/ordinal is fixed by `EAWebkit_Windows.def` (root of the tree): `CreateEAWebkitInstance @1`.

The exe was compiled against these exact headers, so the vtable order + struct layouts already match — your job is to NOT diverge from them.

## Build settings (non-negotiable)
- **Architecture: x86 (Win32). 32-bit only** — the exe is x86; a 64-bit DLL will not load.
- Toolchain: modern MSVC (VS2022) is fine — VS2008 is NOT required for the shim.
- **/MT (static CRT)** so the DLL doesn't depend on the exe's ancient CRT, and so no CRT objects cross the boundary.
- Member functions default `__thiscall`; do not change calling convention.
- Reuse the headers' `EA_ALIGN(4)` and the fixed-size EASTL-wrapper constants exactly. **Do NOT link real EASTL.**
- Output: `EAWebKit.dll` with a `.def` exporting `CreateEAWebkitInstance @1`.

## Minimal implementation (Phase 0)
- `CreateEAWebkitInstance()` → return a static `EAWebkitImpl` implementing `IEAWebkit`.
- `IEAWebkit`: implement `Init`, `SetParameters`, `SetViewNotification`, `CreateView` (returns a `ViewImpl`), `DestroyView`, `Shutdown`, `Destroy`. Everything else: safe no-op stubs that return sane defaults (0/false/nullptr) — but they MUST exist at the correct vtable slots (declare every virtual the header declares, in order).
- `View` (`ViewImpl`): implement `InitView` (record w/h), `SetSize`, `Tick` (no-op), `GetSurface` (return our `SurfaceImpl`). Stub `SetURI`, `EvaluateJavaScript`, `CreateJavascriptBindings`, `RegisterJavascriptMethod`, `On*Event`, etc.
- `ISurface` (`SurfaceImpl`): own a heap ARGB32 buffer sized to the view (start 870×620). `GetData()` → buffer ptr; `GetStride()` → width*4; `GetWidth/GetHeight` → dims; `GetPixelFormat` → `kPixelFormatTypeARGB`. Fill the buffer with a bright solid color (e.g. magenta 0xFFFF00FF) or a test gradient on InitView/resize.
- Honor `ViewParameters::mpViewSurface`: if the exe passes a non-null surface, render into it; if null (expected), serve our own from `GetSurface()`.

## Acceptance test (run on the user's machine)
1. **Back up** the current `EAWebKit.dll` next to `Darkspore.exe` (the Xackery redirect DLL) — keep it safe.
2. Drop the new `EAWebKit.dll` in its place.
3. Launch the game to the login screen.
4. **PASS:** a solid-color rectangle appears where the web login UI should be (≈870×620) → ABI (vtable order, struct layout, `__thiscall`, the C export) + the texture/render path are all proven.
5. **FAIL/crash:** ABI mismatch. Bisect against the headers (most likely a wrong vtable slot count/order, a struct-size/packing mismatch, or wrong bitness). Capture the crash address; image base is 0x00400000.

> Expected side effect: replacing the redirect DLL means the game's real web content won't load in Phase 0 — that's fine, Phase 0 only paints a solid color. Networking/redirect is restored in a later phase (point the engine at `127.0.0.1` or reimplement the `PlatformSocketAPI` hook).

## Do NOT (Phase 0 scope guard)
- Do NOT integrate MiniBlink/CEF yet (Phase 1).
- Do NOT patch or inject into `Darkspore.exe` (the whole point is DLL-only).
- Do NOT implement the JS bridge or input yet (Phases 2–3).
- Do NOT invent header signatures — copy the real ones.

## If Phase 0 passes
Proceed per `WEBVIEW_SHIM_DESIGN.md` → Phase 1 (wire MiniBlink `wkeCreateWebView` + `wkeOnPaintBitUpdated` into the `SurfaceImpl` buffer).
