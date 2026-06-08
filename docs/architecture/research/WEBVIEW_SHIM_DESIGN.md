# WebView Shim — implementation & project design (modern engine behind EAWebKit's ABI)

How to actually build the modern-webview replacement, and how the project is organized. Companion to [`WEBVIEW_MODERNIZATION.md`](WEBVIEW_MODERNIZATION.md) (the Ghidra map + feasibility) — read that first. For the live mb-shim **parity gaps & implementation TODO** (what's built vs stubbed vs missing, prioritized), see [`WEBVIEW_PARITY_GAPS.md`](WEBVIEW_PARITY_GAPS.md).

> **Status:** design, 2026-06-01. Build-viability of the local EAWebKit tree audited with evidence (see "Build reality" below). The shim itself is unbuilt — this is the plan.

## The core idea

Darkspore loads `EAWebKit.dll` and calls **one C export** — `CreateEAWebkitInstance @1` — then drives the whole engine through the returned `IEAWebkit*` vtable + the `View`/`ISurface` vtables (confirmed in the local tree: `EAWebkit_Windows.def`, `EAWebKit.h:384`). So:

**Replace the DLL with a new one that exports the same symbol and implements the same vtables, but internally hosts a modern engine (Ultralight or CEF). Zero changes to `Darkspore.exe`.**

The exe's pixel-upload path (`ClientWeb::WebView_UpdateTexture`@0x00ca4e90) reads pixels via `View::GetSurface()->GetData()` (ARGB32 CPU buffer) and blits them into a D3D9 texture drawn by the `renderWebView` pass — **all of that stays untouched**. The shim only has to (a) hand back an ARGB surface filled by the modern engine, and (b) bridge JS↔native.

## Build reality (audited)

| Question | Answer (evidence) |
|---|---|
| Is the local `eawebkit` tree complete? | **Yes.** Full WebKit-owb source (WebCore 1050 `.cpp`, JSCore 116, BAL 180), generated parser sources **pre-committed** (338 files → no perl/bison/gperf), all EA support packages + FreeType/libjpeg/libpng/zlib populated. (`projects/VS2008/EAWebKit/1.21.00.darkspore/EAWebKit.sln`) |
| Toolchain to rebuild the original DLL | **VS2008 (VC9) + Windows SDK v6.0A** (`doc/README-BUILD.txt`); Win7 SDK reportedly also works (`README.md`). No ICU/curl/openssl needed (EA's BALI18N + DirtySDK). |
| External libs missing? | None significant. Only Windows system libs linked. Minor unknown: `libiconv/include/` may be empty (low risk; EA replaced i18n). |
| Prebuilt output present? | No — never built (`.gitignore` excludes `.dll`/`.lib`). |
| Difficulty to rebuild the *original* DLL | **Moderate** — the friction is the 15-yr-old toolchain + `WarnAsError=TRUE` over 953 files on a modern host, not missing code. |
| **Do we need VS2008 for the SHIM?** | **No.** The shim is a *new* project on a *modern* toolchain; it only consumes the tree's public **headers** as the ABI contract. VS2008 is only needed if reproducing the original EAWebKit (e.g. the Xackery redirect-only DLL) as a fallback. |

> Note: the local tree is **stock EAWebKit 1.21** (the base). Xackery's redirect Detours are private and **not** in the tree — the base compiles to a working engine DLL on its own; his redirect is an add-on at the `PlatformSocketAPI` seam.

## Is it a "module"? Where it lives

It is a **standalone native (C++) client-side module**, separate from the .NET server. It does not link against `ReCap.Server` and is not part of the `dotnet build`. Proposed home: a sibling project, e.g. `client/ReCap.WebShim/` (or its own repo). Its only ship artifact is `EAWebKit.dll`, dropped in next to `Darkspore.exe` exactly like Xackery's DLL today.

### Project layout
```
ReCap.WebShim/
├─ abi/                      # COPIED verbatim from the eawebkit tree — the contract
│   ├─ EAWebKit.h            # IEAWebkit, Parameters
│   ├─ EAWebKitView.h        # View, ViewParameters
│   ├─ EAWebKitViewNotification.h  # ViewNotification, JavascriptMethodInvokedInfo
│   ├─ EAWebKitJavascriptValue.h
│   ├─ EARaster.h            # ISurface, IEARaster, PixelFormat
│   └─ EAWebKitPlatformSocketAPI.h
├─ src/
│   ├─ Exports.cpp           # CreateEAWebkitInstance @1  (+ EAWebKit.def)
│   ├─ EAWebkitImpl.{h,cpp}   # implements IEAWebkit (Init/CreateView/SetParameters/…)
│   ├─ ViewImpl.{h,cpp}       # implements View (SetURI/SetSize/Tick/EvaluateJavaScript/
│   │                         #   CreateJavascriptBindings/RegisterJavascriptMethod/GetSurface/On*Event)
│   ├─ SurfaceImpl.{h,cpp}    # implements ISurface over the engine's pixel buffer (ARGB32)
│   ├─ backend/
│   │   ├─ IWebBackend.h         # engine-agnostic seam (Load/Resize/Paint→buffer/Eval/Bind/Input)
│   │   ├─ MiniBlinkBackend.cpp  # CHOSEN engine (wke C API)
│   │   └─ CefBackend.cpp        # fallback (full Chromium parity)
│   ├─ JsBridge.{h,cpp}       # name↔callback; forwards engine JS calls to ViewNotification
│   └─ Net.cpp                # optional: PlatformSocketAPI redirect (or noop if engine nets itself)
├─ third_party/              # Ultralight SDK (x86) or CEF (x86) binaries
├─ EAWebKit.def              # LIBRARY EAWebKit.dll / EXPORTS CreateEAWebkitInstance @1
└─ CMakeLists.txt            # modern toolchain, x86 target
```

## End-to-end flow
1. `Darkspore.exe` → `LoadLibrary("EAWebKit.dll")` → `CreateEAWebkitInstance()` → returns our `EAWebkitImpl*`.
2. Exe → `Init(allocator)`, `SetParameters(...)`, `SetViewNotification(sink)`, `CreateView()` → our `ViewImpl`.
3. Exe → `View::InitView(params)` → we boot the backend (Ultralight `Renderer`+`View`, or a CEF browser) at the requested size (870×620 for the setup view).
4. Exe → `View::SetURI("game:///UI/XHTML/Labs/Setup/mainwebview.html")`. We map `game://` to our own content (e.g. a React/Vite build served from ReCap's HTTP on `127.0.0.1`, or from disk). **You can keep the original pages or load new ones.**
5. Exe → `View::CreateJavascriptBindings("EA")` + `RegisterJavascriptMethod(name)` ×42. We register the names with the backend's JS engine.
6. Per frame: exe → `View::Tick()`. We pump the backend; it paints into the surface buffer.
7. Per frame: exe → `View::GetSurface()->GetData()/GetStride()` and blits ARGB into the D3D9 texture (`WebView_UpdateTexture`@0x00ca4e90, unchanged). The `renderWebView` pass draws it.
8. JS calls a bound name → backend fires our `JsBridge` → we fill a `JavascriptMethodInvokedInfo` (`mMethodName`/`mArguments`/`mReturn`) → call the exe's `ViewNotification::JavascriptMethodInvoked` → the game handles it and returns to JS.
9. Input: exe → `View::OnMouseMoveEvent/OnMouseButtonEvent/OnKeyboardEvent/...` → forward to the backend.

## ABI compatibility — the make-or-break
The exe expects a binary layout. The shim MUST match it:
- **x86 / 32-bit**, MSVC, member functions `__thiscall` (default) → vtable slot order == method declaration order. **Copy the headers verbatim** so the vtables and POD structs are identical.
- **POD struct layouts** (`Parameters`, `ViewParameters`, `JavascriptValue`, `PixelFormat`, `JavascriptMethodInvokedInfo`) must match field-for-field + packing. Same headers ⇒ same layout (verify no toolchain `#pragma pack` drift).
- **EASTL across the boundary is OK *if* you reuse the header constants (audited).** The live path (`CreateView`/`InitView`/`SetURI`/`SetSize`/`Tick`/`GetSurface`/`On*Event`) is pure POD/pointer/`const char*` — ABI-clean. The only fragile types are `JavascriptValue` and structs embedding it (`JavascriptMethodInvokedInfo`, `LoadInfo`, `LinkNotificationInfo`, `UriHistoryChangedInfo`, `ToolTipInfo`, `ClipboardEventInfo`). These use **opaque fixed-size EASTL wrappers with hardcoded size constants** (`kMaxFixedStringSize=276`, vector-wrapper 16, hashmap-wrapper 32) + `EA_ALIGN(4)` — NOT recomputed templates. So they are binary-predictable: compile the shim against the **same headers/constants + 4-byte alignment and do NOT link real EASTL**, and the layout matches VC9 exactly. No `#pragma pack` is used anywhere — natural MSVC alignment.
- **Single C export** `CreateEAWebkitInstance @1` via a `.def` (matches the original).
- **Ownership:** honor `Init(Allocator*)` and `Destroy*` lifecycles; if the exe supplies `ViewParameters::mpViewSurface` (host-owned ARGB surface), render INTO it; otherwise return our own from `GetSurface()`. Support both.

## Engine backend — CHOSEN: MiniBlink (32-bit, light)
Hide the engine behind `IWebBackend`. **Decision: MiniBlink** (`weolar/miniblink49`) — single 32-bit DLL (~10–20MB), Blink ~2016 (runs typical Vite/React builds), pure-offscreen pixel callback that drops straight into `ISurface::GetData()`. Constraint that drove it: the shim is 32-bit in-proc and Ultralight is x64-only. **CEF win32 is the fallback** if a page ever needs full modern-Chromium parity.

MiniBlink (`wke` C API) → EAWebKit ABI mapping the shim implements:
| EAWebKit ABI method | MiniBlink call |
|---|---|
| `IEAWebkit::Init` | `wkeInitialize()` |
| `CreateView` / `View::InitView` | `wkeCreateWebView()` + `wkeResize(w,h)` + `wkeSetTransparent` + `wkeOnPaintBitUpdated(cb)` |
| `View::SetURI(url)` | `wkeLoadURL` (point at `127.0.0.1` / `game://`-mapped content) |
| `View::SetSize(w,h)` | `wkeResize` |
| `View::Tick()` | pump MiniBlink (its timer/message pump) |
| `View::EvaluateJavaScript(js)` | `wkeRunJS` |
| `View::RegisterJavascriptMethod(name)` | `wkeJsBindFunction(name, thunk)`; thunk fills a `JavascriptMethodInvokedInfo` and calls host `ViewNotification::JavascriptMethodInvoked` |
| `View::GetSurface()->GetData()` | the ARGB buffer captured in the `wkeOnPaintBitUpdated` callback (shim-owned; BGRA→ARGB if needed) |
| `View::On*Event` (Phase 3) | `wkeFireMouseEvent` / `wkeFireKeyUp/Down/Press` / `wkeFireMouseWheelEvent` / `wkeSetFocus` |

**Pixel flow:** `wkeOnPaintBitUpdated` fires on repaint → memcpy its buffer into the shim's persistent ARGB surface → the exe's `WebView_UpdateTexture`@0x00ca4e90 reads it via `GetSurface()->GetData()` the next frame. Pure offscreen — no engine-side window.

*Rejected:* Ultralight (x64-only → in-proc impossible); WebView2 (window/composition, no clean CPU buffer — launcher-window only).

## Networking
The shim's engine does its own HTTP/TLS. Point content at `127.0.0.1` (ReCap's HTTP) and trust the self-signed cert in the engine config → the old DirtySDK cipher problem and Xackery's `gethostbyname` Detour become unnecessary **for web content**. If anything still relies on the exe-injected `PlatformSocketAPI`, reimplement the redirect there (it's a flat function-pointer table). Game/Blaze/RakNet networking is entirely separate and untouched.

## Mapping coverage & scope (why the Ghidra map is "done enough")
The **EAWebKit ABI source of truth is the header tree** (`abi/`), not the disassembly — the headers fix the exact vtable order and struct layouts the shim must match. So **100% static coverage of the exe's web path is not required to build the shim.** What's mapped is at design-altitude: the two seams (`WebView_UpdateTexture`@0x00ca4e90 / `cUIWebWindow` vtable), the surface model, and the ABI confirmations. Deliberately deferred (resolve only when a phase needs it, mostly faster at runtime):
- **Input caller** (who feeds `View::On*Event`) → runtime trace in Phase 3.
- **One view or two** (`cWebView`/WebView3D composited vs `cUIWebWindow` login) → only if the login surface misbehaves in Phase 1/2.
- **`CreateView`/`InitView` chain** (`FUN_00ca6720`→…) → only if the Phase-0 ABI test fails and the exact `ViewParameters` fill is needed.
- **Structs:** `cWebView`/`cUIWebWindow` are **exe-internal and NOT reimplemented** — only their high-confidence fields are recorded; do not chase the fuzzy dirty-rect/dims offsets. If building, the worthwhile datatype work is importing the **ABI structs from the headers** into Ghidra, not deeper exe spelunking.

## Implementation plan (phased, with gates)
Each phase has a clear pass/fail gate; stop and fix before advancing. Engine = **MiniBlink** (chosen; 32-bit, light); CEF win32 fallback.

**Phase 0 — ABI smoke test** *(highest-risk assumption, cheapest test; no engine yet).*
Build the 32-bit shim DLL: export `CreateEAWebkitInstance @1`; implement minimal `IEAWebkit` (Init/CreateView/SetParameters/SetViewNotification/Destroy) + `View` (InitView/Tick/SetURI/SetSize/GetSurface; stub the rest) + `ISurface`. `GetSurface()` returns a shim-owned ARGB buffer filled with a solid color / test pattern. Drop in place of `EAWebKit.dll`, launch.
**Gate:** the colored rectangle appears where the login web UI should be → vtable order + struct layout + calling convention + texture path all proven. (Crash ⇒ ABI mismatch; bisect against the headers.)

**Phase 1 — Static page render** *(wire MiniBlink).*
Add `IWebBackend` + MiniBlink (`wkeCreateWebView` + `wkeOnPaintBitUpdated`). On `Tick()`, pump it and copy the `wkeOnPaintBitUpdated` buffer into the `GetSurface()` buffer (BGRA→ARGB if needed). `wkeLoadURL` a static test HTML.
**Gate:** a real HTML page renders in-game.

**Phase 2 — JS bridge (run the stock pages)** *(proves the round-trip).*
Implement `RegisterJavascriptMethod` + map engine JS calls → `ViewNotification::JavascriptMethodInvoked`, using the exact header size-constants + `EA_ALIGN(4)` for the JSValue-bearing structs. Keep the original 42 names; point `SetURI` at the stock `mainwebview.html`.
**Gate:** the original login/lobby pages work end-to-end (`callSporeNet`/`getNucleusAuthToken` round-trip). Risk: JSValue struct ABI — verify sizes match.

**Phase 3 — Input (interactive)** *(needs the one runtime trace).*
Resolve the input-caller unknown at runtime (attach, click the login, see who calls into the View). Implement `View::On*Event` forwarding to the engine.
**Gate:** clicking/typing in the login works.

**Phase 4 — Modern UI** *(the payoff).*
Point `game://` at a Vite/React build served from ReCap's HTTP on `127.0.0.1`; design a clean modern bridge (keep or replace the 42 names).
**Gate:** the React UI runs in-game.

> **Decision gate before Phase 0:** this is a large client-side native side-quest, independent of the D-010 dungeon-crash critical path. Greenlight only when the single-player path is healthy enough to spare the effort.

## Open items — resolved 2026-06-01 (Ghidra + headers)
- **Surface model — RESOLVED:** the exe leaves `ViewParameters::mpViewSurface = NULL` (`EAWebKitView.h:95,110`) and calls `View::GetSurface()` (IView vtable[0xd0]) every frame in `WebView_UpdateTexture`@0x00ca4e90, then reads `ISurface::GetData()`. **The shim must implement `GetSurface()` returning a shim-owned ARGB32 RAM buffer.**
- **ABI hazard — RESOLVED:** core path is ABI-clean; the JS-value-bearing structs are safe if the shim reuses the hardcoded EASTL-wrapper size constants + `EA_ALIGN(4)` (see ABI section). Not a blocker.
- **Input path — PARTIALLY RESOLVED (one unknown):** the five `View::On*Event` input slots (vtable 0x30–0x40) and the `cUIWebWindow` input slots (0x68/0x6c) are **not called through the cWebView/cUIWebWindow chain** — dead in the traced path. A login screen obviously needs input, so it must arrive via another route (window-proc → EAWebKit DLL directly, or a separate view object). **Not a blocker:** the shim implements `On*Event` regardless; whoever calls them, it handles them. For a display-only prototype they can be no-ops. → one targeted runtime/Ghidra trace later to find the actual input caller.
- **Two view objects? — TO CONFIRM:** `cWebView`/WebView3D (vtable 0x01079f38, texture-composited, IView@+0x38) vs the login `cUIWebWindow` (vtable 0x00fd8360 via `OpenSetupWebView`). Confirm whether they wrap the same EAWebKit View or two separate views (affects which surface the login renders into). Likely related to the input-path unknown.

## Engine bitness — RESOLVED 2026-06-01
**Ultralight is x64-only** (its own docs: "upstream JavaScriptCore has dropped 32-bit support"; only `lib/win/x64`). The shim is 32-bit, so Ultralight is **out for in-proc**. Use **CEF win32** (32-bit `windows32` build, OSR `OnPaint`) or **MiniBlink** (32-bit single DLL, `wkeOnPaintBitUpdated`). Ultralight only via an out-of-process x64 helper sharing the buffer back. Note: the shim still links a **prebuilt** engine and is built with **modern MSVC targeting x86 + static CRT** — VS2008 is never needed for the shim.
