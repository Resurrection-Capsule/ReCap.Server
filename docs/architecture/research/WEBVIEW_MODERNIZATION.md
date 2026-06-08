# Webview Modernization — replacing EAWebKit with a modern engine (CEF / WebView2)

Feasibility map for swapping Darkspore's embedded web UI engine (EAWebKit 1.21, ~WebKit Safari 4/5 era) for a modern Chromium-based engine, so the client can render modern pages (incl. a compiled Vite/React SPA) in the in-game UI (login, lobby, store, creature/deck panels).

> **Status:** Ghidra-mapped 2026-06-01 from `Darkspore.exe` (image base `0x00400000`, x86:LE:32, D3D9). Confirmed vs inferred is marked per row. Engine is **EAWebKit** (strings `EAWebKit.dll`@0x01079e68, `EA::WebKitUtil`@0x0107a5c7, `EA::Messaging::IHandler` bridge). A sub-agent labelled it "Awesomium" — that is an **incorrect inference**; the binding-API *shape* is generic WebKit-wrapper, the `EA::*` symbols confirm EAWebKit.

## TL;DR

The integration model is unusually friendly for an engine swap:
- The web UI is **off-screen rendered into a D3D9 DYNAMIC texture**, drawn by a named render pass (`renderWebView`), sibling to `renderFlashUI`. CEF off-screen rendering (OSR) produces exactly an RGBA buffer per paint → maps 1:1 onto the existing upload path.
- The control surface is a **small, enumerable vtable** (`cUIWebWindow`) + an **explicit JS↔native bridge** of 42 named callbacks.
- **Two clean intercept points** make a surgical Detour-based swap viable without replacing all of `EAWebKit.dll`.

**Verdict:** viable. Not a weekend job, but the architecture removes the worst risks. Recommended engine for the in-game (composited) panels = **CEF OSR**; for a windowed launcher = **WebView2** is simpler.

---

## Pipeline 1 — Render / pixels (how the page reaches the screen)

| Item | Address | Notes |
|---|---|---|
| Render-pass registration | `FUN_004fecf0` | registers pass `"renderWebView"` with callback `LAB_004f3a20`, context `*(gameObj + 0x1917c)` |
| renderWebView pass callback | `LAB_004f3a20` | inline (Ghidra didn't split it); reads texture handle from `cWebView+0x14`, draws textured quad via the material pipeline. **Unchanged by a swap.** |
| cWebView object ctor | `FUN_00ca8d00` | vtable `0x01079f38`; the WebView3D object |
| cWebView accessor | `FUN_004f3790` | returns `*(DAT_01440288 + 0x1917c)` = the cWebView |
| **Pixel upload (THE intercept)** | **`FUN_00ca4e90`** | WebView3D `vtable[0x08]`. Gets EAWebKit surface via `IView->vtable[0xd0]` (GetBitmap-style), `LockRect` → `memcpy` (stride-aware, dirty-rect partial) → `UnlockRect`, then notifies via `FUN_00491f60`. |
| D3D9 LockRect wrapper | `FUN_00d17060` | requires texture type-8 (DYNAMIC); `IDirect3DTexture9::LockRect` via vtable `0x4c` |
| D3D9 UnlockRect wrapper | `FUN_00d171a0` | `UnlockRect` via vtable `0x50` |
| Texture create | `TextureManager::GetInstance()->vtable[0x10]` | `(w, h, 1, flags 0x08=DYNAMIC, fmt 0x15≈A8R8G8B8, 0)`; named `"WebView"` via `vtable[0x11]` |

**cWebView field layout** (from `FUN_00ca8d00` / `FUN_00ca7a70` / `FUN_00ca7b60`):
`+0x14` texture handle (inner `IDirect3DTexture9*` at handle+4) · `+0x38` EAWebKit IView ptr · `+0x3f..0x42` dirty rect (L/T/R/B) · `+0x40` tex w · `+0x44` tex h · `+0x61` has-alpha.

**CEF OSR fit:** in `FUN_00ca4e90`, keep the Lock/memcpy/Unlock skeleton; source `src_pixels`/`src_stride` from CEF `CefRenderHandler::OnPaint(buffer, dirtyRects, w, h)` instead of the EAWebKit `GetBitmap()` call. CEF's `dirtyRects` maps onto the existing `+0x3f..0x42` partial-update path. CEF default is BGRA → swap channels or create the texture in CEF's format. Net: **one function** is the seam.

## Pipeline 2 — Control / bridge (how the game drives the page)

**Class `cUIWebWindow`** (0x23C bytes), vtable **`0x00fd8360`**, ctor `FUN_004487f0`, setup `FUN_00448aa0`, alloc `FUN_00448290`. Singleton at `*(DAT_01440288 + 0x1917c)`. Outer wrapper `cSPUIHostBrowser` (0x30B, ctor `FUN_0042fcc0`, vtable `0x00fd6164`) just holds a ref at `+0x6fc`. Login/setup bring-up = `FUN_00428910` (loads `game:///UI/XHTML/Labs/Setup/mainwebview.html`, size 870×620).

**vtable map** (C = confirmed at a call site, I = inferred):

| Off | Impl | Method | |
|---|---|---|---|
| 0x08 | `0x00448ff0` | dtor (full teardown `FUN_00448920`) | C |
| 0x0c | `0x00448380` | Destroy/Shutdown (releases inner view) | C |
| 0x14 | `0x00448360` | Tick/Update (thunk; real pump inside engine) | I |
| 0x18 | `0x005f1570` | GetNativeView (inner view ptr) | I |
| 0x24 | `0x00448660` | SetPosition(x,y,w,h) | C |
| 0x28 | `0x00be7880` | SetSize(w,h,..) — called `(870,620,0,0)` | C |
| 0x2c | `0x00be7880` | SetVisible(bool) | C |
| 0x30 | `0x00448450` | IsReady/IsLoaded()→bool | C |
| 0x3c | `0x004484e0` | **LoadURL(const char\*)** | C |
| 0x40 | `0x00448540` | LoadURLW / NavigateTo (wide) | C |
| 0x44 | `0x004485a0` | **ExecuteJavaScript(const char\*)** | C |
| 0x48 | `0x004485e0` | ExecuteJavaScript(float arg) | I |
| 0x4c | `0x00940b70` | **ExecuteJavaScriptWithResult(script, out)→bool** | C |
| 0x50 | `0x00801d10` | **RegisterJSCallback(name, IHandler\*)** | C |
| 0x58 | `0x00448620` | **IsCreated()→bool** | C |
| 0x68/0x6c | `0x0076f560` | **no-op stubs** — input is NOT routed through this vtable | C |

**Input:** the outer vtable stubs mouse/key (no-ops). EAWebKit gets input via the inner view / OS path. → a CEF/WebView2 replacement receives OS input directly; **no input vtable slots to reimplement.**

**JS→native bridge:** `RegisterJSCallback(name, handler)` binds a name to a single shared `EA::Messaging::IHandler`-style object at `cUIWebWindow+0x1F4` (`OnHandle`/`GetId`/`GetSubId`; vtables `0x01079d80..0x01079db0`). JS call → engine invokes handler, dispatch-by-name-hash in `FUN_00910470` → routes to the SP_UI subsystem. Args arrive as JSValue/JSArray; returns via the same channel. ctor that sets the IHandler vftable: `FUN_00ca4cd0`.

**42 bound callbacks** (setup view `FUN_00428910` + community view `FUN_00448aa0`):
`callSporeNet, closeWindow, closeBRSWindow, closeSurveyWindow, editCreature, getLobbyMembersAsJSON, getFriendsAsJSON, getBlockedPlayersAsJSON, getNucleusAuthToken, logout, followPlayer, openExternalBrowser, unfollowPlayer, blockPlayer, unblockPlayer, inviteToGroup, sendTellToPlayer, setGameOption, setSelectedFriend, registrationEmail, retrieveLocaleString, retrieveAttributeString, getPlayerAccountId, getPlayerOnlineId, getLocaleId, getSnApiHost, getWebHost, retrieveAbilityPngKey, getLootName, openBrowser, getCreatureBaseStats, getCreatureAllBaseAbilityKeyvalues, getCreatureAbilityKeyvalues, openChildPage, closeChildPage, playSound, progress, install, getLootStats, getLootModifierText, playDemo, showAnnouncementWindow`.

---

## EAWebKit ABI (the shim contract) — from the local `C:\CodingProjects\Personal\eawebkit` tree

The DLL exports **exactly one C symbol**: `CreateEAWebkitInstance` (ordinal 1, `EAWebkit_Windows.def:3`) → returns `EA::WebKit::IEAWebkit*` (`EAWebKit.h:384`). The host drives everything through that vtable + the `View` vtable. So **a replacement DLL controls the entire engine with zero exe patching.**

- **Lifecycle/factory** (`IEAWebkit`): `Init`/`Shutdown`/`Destroy`, `CreateView`/`DestroyView`, `SetParameters`, `SetPlatformSocketAPI`, `SetViewNotification`.
- **View** (`EAWebKitView.h:176`): `InitView`/`Tick`/`SetURI`/`SetSize`/`EvaluateJavaScript`/`CreateJavascriptBindings`/`RegisterJavascriptMethod`/`GetSurface` + input injectors (`OnKeyboardEvent`/`OnMouse*`).
- **PIXELS:** `View::GetSurface() -> EA::Raster::ISurface*` (`EAWebKitView.h:388`); read `GetData()` (raw ptr) + `GetStride()`; default format `kPixelFormatTypeARGB` 32-bit (`EARaster.h:81`). The host may even pass its own surface via `ViewParameters::mpViewSurface`. **This is what `ClientWeb::WebView_UpdateTexture`@0x00ca4e90 consumes** (the `IView->vtable[0xd0]` call = GetSurface/GetData). Any engine that yields an ARGB CPU buffer drops in here.
- **JS bridge:** `CreateJavascriptBindings("EA")` + `RegisterJavascriptMethod(name)`; JS calls fire `ViewNotification::JavascriptMethodInvoked(info)` with `mMethodName`/`mArguments[10]`/`mReturn` (`EAWebKitViewNotification.h:427`). Matches the 42 `callSporeNet`-style names.
- **Engine era:** AppleWebKit **525.1** (Safari 3.1, ~2008), WebCore base = **OWB** (Origyn). DirtySDK transport default (`WTF_USE_DIRTYSDK`); socket injection via `PlatformSocketAPI` (`EAWebKitPlatformSocketAPI.h:121`) — the seam Xackery's redirect Detour uses. Tree builds as-is under VS2008.

Export surface to reimplement ≈ 175 vtable methods across `IEAWebkit`/`View`/`ISurface`/`IEARaster`, **but only ~15–20 are live** (Init, CreateView, SetURI, SetSize, Tick, EvaluateJavaScript, RegisterJavascriptMethod, GetSurface, input); the rest are stubs.

## Swap strategies

**A — (RECOMMENDED) Drop-in replacement `EAWebKit.dll` backed by a modern engine. NO exe patch.** Best fit for the current workflow (the user already swaps Xackery's DLL, no exe patch). Implement `CreateEAWebkitInstance` + the live `IEAWebkit`/`View`/`ISurface` methods over the chosen engine; `GetSurface()->GetData()` returns the engine's ARGB frame → the exe's `WebView_UpdateTexture`@0x00ca4e90 blits it into the D3D9 texture, and the render pass `LAB_004f3a20` stays untouched. Map `RegisterJavascriptMethod`/`JavascriptMethodInvoked` to the engine's JS bindings (keep the 42 names to run the original pages, or load your own React/Vite pages). Networking: either reimplement the `PlatformSocketAPI` redirect (Xackery's hook) or let the modern engine talk to `127.0.0.1` directly + trust the self-signed cert (simpler).

**B — Detour `cUIWebWindow` + `WebView_UpdateTexture`@0x00ca4e90 inside the exe (CEF OSR).** Surgical but requires injecting into the exe. Use only if a clean DLL-ABI shim proves impractical. Hook the ~10 `cUIWebWindow` vtable methods + feed the `cWebView+0x14` texture from CEF `OnPaint`; render pass untouched.

**Prefer A** — same seam, no exe patch, and the local source tree fully specifies the ABI.

### Engine choice — **must be 32-bit in-proc** (the exe is x86) AND hand back an ARGB CPU buffer
> **Ultralight is x64-only** (confirmed from its docs: "upstream JavaScriptCore has dropped 32-bit support"; only `lib/win/x64`). Since Darkspore is 32-bit, **Ultralight cannot be linked in-process** — it's only usable via an out-of-process x64 helper. This eliminates the earlier "light + drop-in" pick.

| Engine | 32-bit in-proc | Weight | Fits `ISurface` (ARGB CPU) | Runs React/Vite | License |
|---|---|---|---|---|---|
| **CEF** (recommended, safe) | ✅ `windows32` build | heavy (~150MB) | OSR `OnPaint`→buffer | full Chromium parity | BSD |
| **MiniBlink** (recommended, light) | ✅ single 32-bit DLL | light (~10–20MB) | `wkeOnPaintBitUpdated`→buffer | Blink ~2016 (most React builds work) | free DLL / source-available |
| Ultralight | ❌ x64 only | light | native `BitmapSurface` | yes | only via out-of-proc x64 helper |
| Sciter | ✅ 32-bit | tiny (~5MB) | native | no (own engine, not a real browser) | commercial |
| WebView2 | host can be 32-bit (runtime out-of-proc) | light (shared runtime) | window/composition — no clean CPU buffer | full Chromium | system component |

**Pick: MiniBlink** (`weolar/miniblink49`) — chosen for the light 32-bit footprint + `wkeOnPaintBitUpdated` dropping straight into `GetSurface()`; Blink-2016 runs typical Vite/React builds. **CEF win32 = fallback** if full modern-Chromium parity is ever needed. WebView2 only fits a standalone launcher *window*. See `WEBVIEW_SHIM_DESIGN.md` for the wke→ABI mapping + phased plan.

---

## WebView2 vs CEF; can it run a Vite/React build?

**Yes** — a compiled Vite/React SPA is static HTML/JS/CSS; modern Chromium (CEF or WebView2) runs it with no web-side limits (ES2023, grid, flexbox, WebGL, web fonts, fetch, animations). Serve `dist/` from ReCap's HTTP server (or local) and point the view at it (hash routing avoids host rewrite).

- **In-game composited panels (login/lobby/store):** rendered into a D3D9 texture → **CEF OSR** is the clean fit (`OnPaint` RGBA → `FUN_00ca4e90`). WebView2 can composition-host but D3D9 texture interop is awkward.
- **A standalone launcher window:** **WebView2** is trivial (it owns a real HWND; just point it at the page). ReCap already redirects the launcher via `getConfigs`/remote-UI.

**The limit is integration cost, not page capability:** (1) Chromium runtime weight (WebView2 Evergreen usually present, or ~150MB fixed; CEF bundle ~150–250MB) bloats the installer; (2) multi-process Chromium = hundreds of MB RAM vs a 2011 game; (3) the bridge must be rewired to your modern pages; (4) 32-bit: in-proc CEF needs 32-bit CEF (exists), or run CEF/WebView2 in a separate helper process to sidestep bitness. **Bonus:** Chromium's modern TLS removes the old DirtySDK cipher problem for web content.

---

## Ghidra annotation status (2026-06-01)
All confirmed functions renamed + plate-commented in the project, namespaces `ClientWeb`/`ClientRender`: `WebView_UpdateTexture`@0x00ca4e90, `GetWebView`@0x004f3790, `WebView_Ctor`@0x00ca8d00, `RegisterRenderPasses`@0x004fecf0, `DynamicTexture_LockRect`@0x00d17060 / `_UnlockRect`@0x00d171a0, and the `UIWebWindow_*` set (Ctor/Setup/Alloc/Dtor/Destroy/LoadUrl(W)/ExecuteJavaScript(WithResult)/RegisterJsCallback/IsCreated/IsReady/SetPosition), `OpenSetupWebView`@0x00428910, `SPUIHostBrowser_Ctor`@0x0042fcc0, `JsBridge_DispatchByName`@0x00910470 (medium-confidence). Label `ClientWeb_renderWebView_PassCallback`@0x004f3a20. Structs `cWebView`/`cUIWebWindow` created. `IView->vtable[0xd0]` confirmed = `View::GetSurface` (ARGB32 CPU buffer).

## Next steps (optional, if pursuing the build)
- Pull the exact `IEAWebkit`/`View`/`ISurface` vtable layouts from the local source headers and lay them out as Ghidra structs (the precise shim ABI).
- Prototype the shim with Ultralight (`BitmapSurface` → `ISurface::GetData`) first; CEF if full Chromium parity is needed.

> This is a large side-quest relative to the D-010 dungeon-crash critical path. Recorded for when the UI-modernization track is picked up.
