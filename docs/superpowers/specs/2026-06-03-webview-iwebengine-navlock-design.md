# Webview: IWebEngine abstraction + navigation lock — Design

Date: 2026-06-03
Scope: `eawebkit/source/` (the ReCap EAWebKit→MiniBlink shim) + a small touch to `EAWebKitView.cpp`.
Status: design approved, pending implementation plan.

## Background

The in-game web UI now runs on MiniBlink (mb132 / Chromium 132) inside a shim that exports the real
EAWebKit ABI but routes rendering/JS/input to mb (see `memory/eawebkit-modern-engine-phase1.md`,
`memory/webview-game-package-serving.md`). It is functionally complete: launcher, announcement,
register, and the social/account hub (`mainwebview.html` + iframes, served from `Web.package`) all
render and interact; locale strings and synchronous getters are bridged. Instrumentation has been
trimmed.

Two follow-ups remain that are worth doing together because they reinforce each other:

1. **IWebEngine abstraction** — put the mb backend behind a neutral interface so the engine can be
   swapped later (e.g. an out-of-process/sandboxed engine) without rewriting the consumer.
2. **Navigation lock** — confine what the engine *loads* to first-party origins, sending external
   top-level navigations to the system browser.

### Threat model (why this matters)

The third-party-content threat becomes real once ReCap supports connecting to **remote servers**: a
player could connect to a modified/malicious ReCap server, and an outdated renderer would be an
attack vector. The two layers cover different cases:

- **nav-lock** confines what the engine *loads* to the active server's origin + `game://`. It stops
  in-page content (a link, a redirect) from pointing the engine at some *other* host.
- It does **not** protect against the *connected server itself* being malicious — that server is the
  trusted origin, so it can serve hostile HTML directly. That case is purely the renderer's problem,
  and the mitigation is the **modern engine** (Chromium 132, maintained) vs EAWebKit (2008, ancient
  CVEs). The engine swap — already done — is the real mitigation here.
- **Mods** (DBPF asset swap / Detours injection) are out of scope: they already imply code execution;
  engine choice is moot. Separate problem (mod signing/sandbox).

The **IWebEngine** abstraction keeps the door open to a harder isolation model (out-of-process /
sandboxed engine) later, without touching the consumer, if remote servers make us want it.

Network note: ReCapHooks redirects host resolution at the socket level, so the engine always sees
`localhost` in the URL regardless of whether the active server is local or remote. The nav-lock
allowlist is therefore `localhost` + `game://` and behaves identically for local and remote servers.

## Part 1 — IWebEngine abstraction (approach: thin delegation)

Chosen approach: a pure C++ interface with a MiniBlink implementation that **delegates** to the
existing `recap::Mb*` functions. The tested mb core (`ReCapMiniBlink`, `ReCapJsBridge`) is NOT
rewritten — it becomes the backend's interior. This is the lowest-churn option that still yields a
real swap point (a future engine implements the same interface). Rejected: (A) moving the mb code
*into* a rewritten backend — needless risk to just-stabilized code; (C) rename-only without a vtable
— no real swap capability.

### New files (`eawebkit/source/`)

- **`IWebEngine.h`** — pure interface, no mb/EAWebKit types. Boundary types reuse the existing neutral
  types from `ReCapJsBridge.h`/`ReCapMiniBlink.h` (`recap::JsVal`, `MbJsSink`, `MbPaintSink`,
  `MbLoadSink`).
  - `struct IWebView` (abstract):
    - load: `LoadURL(const char*)`, `LoadHTML(const char* html, const char* baseUrl)`
    - frame: `Resize(int,int)`, `Wake()`, `int ConsumeDirty()`
    - input: `FireMouseMove(x,y,shift,ctrl)`, `FireMouseButton(button,down,x,y,shift,ctrl)`,
      `FireWheel(x,y,delta)`, `FireKey(id,isChar,down)`, `SetFocus(int)`
    - props/nav: `SetUserAgent(const char*)`, `SetTransparent(int)`, `Reload()`, `StopLoading()`,
      `int GoBack()`, `int GoForward()`, `int GetURL(char*,unsigned)`
    - js: `RunJs(const char*)`, `int RunJsSync(const char*, JsVal*, char*, unsigned)`,
      `CreateJsObject(const char*)`, `BindJsMethod(const char* obj, const char* method, MbJsSink, void*)`
    - lifetime: `Destroy()` (and a virtual dtor)
    - load sink: `SetLoadSink(MbLoadSink, void*)`
  - `struct IWebEngine` (abstract): `bool Available()`, `IWebView* CreateView(int w, int h, MbPaintSink, void* user)`
  - `IWebEngine* GetWebEngine()` — returns the process-wide MiniBlink engine singleton.

- **`MiniBlinkBackend.{h,cpp}`** — `class MiniBlinkWebView : public IWebView` holding a
  `recap::MbView*`; every method delegates to the matching `recap::Mb*` call. `class MiniBlinkEngine
  : public IWebEngine` whose `CreateView` calls `recap::MbCreate` and wraps the result;
  `Available()` → `recap::MbAvailable()`. `GetWebEngine()` returns a static `MiniBlinkEngine`.

### Unchanged

`ReCapMiniBlink.{h,cpp}` and `ReCapJsBridge.{h,cpp}` are untouched — the mb/JS core stays as the
backend interior. `recap::MbView` remains the concrete handle used inside the backend.

### `EAWebKitView.cpp` changes (mechanical)

- `mpRecapMb` (already `void*`) now holds an `IWebView*`.
- `InitView`: `recap::GetWebEngine()->Available()` gate; `mpRecapMb = engine->CreateView(w,h,RecapMbPaintSink,this)`;
  then `view->SetLoadSink(...)`, `view->SetUserAgent(...)`, `view->SetTransparent(...)`, `view->SetFocus(1)`.
- Replace the ~20 call sites `recap::MbX((recap::MbView*)mpRecapMb, ...)` with
  `((recap::IWebView*)mpRecapMb)->X(...)` (SetURI/SetContent/Refresh/CancelLoad/GoBack/GoForward/
  GetURI/EvaluateJavaScript/Tick/OnMouse*/OnKey*/OnFocus/Destroy/CreateJavascriptBindings/
  RegisterJavascriptMethod).
- The thunks `RecapMbPaintSink` / `RecapMbLoadThunk` / `RecapMbJsSink` are unchanged (already neutral
  types).

`EAWebkit.vcproj` compiles the new `MiniBlinkBackend.cpp`.

## Part 2 — Navigation lock

### Policy

`isFirstParty(const char* url)` (in `ReCapMiniBlink.cpp`) returns true for:
- `http://` or `https://` with host `localhost` or `127.0.0.1` (optionally any port), or
- `game://` (any path), or
- `about:blank` (used by `LoadHTML`).

Everything else is third-party.

### Enforcement points (both callbacks already registered; today they only log)

1. **`navigationThunk`** (`mbOnNavigation`, top-level navigation): `isFirstParty(url)` → return `TRUE`
   (allow). Else → `ShellExecuteW(NULL, L"open", wurl, NULL, NULL, SW_SHOWNORMAL)` and return `FALSE`
   (cancel in-engine; opens in the OS default browser).
2. **`createViewThunk`** (`mbOnCreateView`, `window.open` / `target=_blank`): if the target URL is
   third-party → `ShellExecuteW` + suppress (`NULL_WEBVIEW`); first-party popup → suppress (current
   behavior).

### Not gated

Subresources (images/css/fonts from another origin referenced by the original EA HTML, e.g.
`darkspore.com` images) keep loading — `mbOnLoadUrlBegin`/`loadUrlBeginThunk` continues to do only
the `game://` rewrite. Nav-lock is top-level only (per decision).

### Plumbing

`#include <shellapi.h>` + `#pragma comment(lib, "shell32.lib")` in `ReCapMiniBlink.cpp`.
`ShellExecuteW` is called directly from native (no dependency on the JS bridge). The url (utf8) is
widened to a stack `WCHAR` buffer before the call.

## Validation

- **IWebEngine**: `/W4 /WX` compile clean (ReCap*.cpp + MiniBlinkBackend.cpp); a VS2008 full build;
  in-game behavior is identical (pure refactor — launcher renders + buttons + Play, hub renders +
  interacts, no regression). This is the acceptance bar: no functional change.
- **Nav-lock**: recapmb.log shows first-party navigations allowed; an external top-level navigation
  (e.g. a ToS link `tos.ea.com`) is cancelled in-engine and opens in the system browser; the hub and
  its iframes (localhost/game://) keep working; external subresources still load (cosmetic).

## Out of scope (future, not this plan)

- Moving the register page out to the system browser.
- P2 robustness: localStorage persistence, mb disk/RAM cache, title/url-change notifications,
  download handling, network-error notifications.
- An out-of-process/sandboxed engine implementation of `IWebEngine`.
- CSP / deeper content policy.
