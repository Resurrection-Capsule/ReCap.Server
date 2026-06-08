# WebView Engine — Parity Gaps & Implementation TODO

**New engine vs original EAWebKit.** What our MiniBlink (mb) shim covers, what it stubs, and what still has to be built to match the contract the retail client expects.

Date: 2026-06-03. Sources: full audit of `C:\CodingProjects\Personal\eawebkit\source` + `include` (original EAWebKit ABI) vs the `RECAP_MINIBLINK` paths in the shim. Companion docs: [`WEBVIEW_MODERNIZATION.md`](WEBVIEW_MODERNIZATION.md), [`WEBVIEW_SHIM_DESIGN.md`](WEBVIEW_SHIM_DESIGN.md), [`WEBVIEW_SHIM_KICKOFF.md`](WEBVIEW_SHIM_KICKOFF.md).

## How it works now

`EAWebKit.dll` is a shim exporting the real EAWebKit ABI (`CreateEAWebkitInstance @1`, `IEAWebkit`/`View`/`ViewNotification`/`ISurface` vtables) but routing each call to **MiniBlink (mb132_x32.dll)** instead of WebCore. Key files:

- `ReCapMiniBlink.cpp/.h` — mb wrapper (create, load, resize, paint sinks, input, JS object).
- `ReCapJsBridge.cpp/.h` — JS bridge (`window.Client.*` ⇄ `JavascriptMethodInvoked`), `:hover` shim.
- `EAWebKitView.cpp` — every ABI method has an `#ifdef RECAP_MINIBLINK` / `mpRecapMb` branch routing to mb; **methods without that branch fall through to legacy WebCore code that is dead in mb mode (null `mpWebView`/`GetFrame()`), so they silently no-op.**
- `EAWebKit.cpp` — `IEAWebkit` concrete; most `Parameters`/cookie/cache/cert calls still target WebCore and have **no effect on mb**.
- `ReCapHooks.cpp` — process-wide Detours (host/connect redirect + cert bypass), independent of mb.

Three live webviews (sizes from [`webview-dimensions`], recapmb.log): **launcher 800×600**, **announce banner 230×346**, **register 820×464**.

The design principle: mb owns rendering, network, and JS — so large parts of the EAWebKit ABI (transport handlers, font server, cookies, cache, SSL, DirtySDK socket API) are **structurally irrelevant** and must be re-expressed against mb's own APIs, not ported.

## Confirmed root causes already diagnosed (2026-06-03)

| Symptom | Root cause | Status |
|---|---|---|
| Launcher/buttons wrong (monospace) font | `@font-face` is per-document; `launcher.html` didn't link `dsfonts.css`; mb doesn't resolve OS family names | **FIXED** (linked dsfonts.css; `'PirulenRg-Regular'` fallback). See [`webview-fonts-miniblink`] |
| Announce window **black** | (1) client loads `game:///UI/XHTML/Labs/Setup/announcement.html`; mb has no `game://` handler → no paint. (2) shim force-opaques alpha → unpainted DIB = black | **FIXED 2026-06-03** (pending rebuild+test): general `game://` mapping (`mapGameUrl` in `MbLoadURL` + `mbOnLoadUrlBegin` net rewrite) + transparency path. See [`webview-game-scheme-redirect`] |
| Announce should be **invisible when empty** | Original used `ViewParameters.mbTransparentBackground` → transparent surface. Shim ignores it + forces alpha 0xFF | **FIXED 2026-06-03** (pending rebuild+test): `MbSetTransparent` from `vp.mbTransparentBackground`; opaque alpha-force moved into the HDC paint thunk, transparent views keep mb's alpha (one-shot alpha census log proves whether mb delivers alpha through the HDC path) |
| Register page `Uncaught SyntaxError: Unexpected end of JSON input` | ~~JS bridge sync return~~ **SUPERSEDED 2026-06-03 — wrong.** Real cause: `eawebkit.js HTTP.get/post` fired the callback on ANY `onreadystatechange` with status 200 (a quirk-workaround for original EAWebKit, which never reached readyState 4); Chromium fires at readyState 2/3 with an **empty body** → `JSON.parse("")` throws. The bridge isn't involved — the page talks to the server via XHR, not `Client.*` | **FIXED 2026-06-03** in `ReCap.Server/resources/static/assets/js/eawebkit.js` (fire once at readyState 4 / onload). No DLL rebuild needed for this one — server-side JS |
| `EAWebKit.isUserAgent()` false on mb (UA = plain Chrome) → `closeWindow()` falls to `window.close()` (no-op), log gating wrong | mb UA never configured | **FIXED 2026-06-03** (pending rebuild+test): `MbSetUserAgent` in `InitView` mirrors WebCore path: `"<Parameters.mpUserAgent> EAWebKit/<version>"` |

**Evidence note:** the original EAWebKit's announce reached our server at `/web/sporelabsgame/announceen` (old 404 logs 05-28→30). The client passes `game://…announcement.html` to both DLLs; the original resolved `game://`, loaded the packaged shell, and the shell fetched that server URL. mb can't load the shell → chain breaks. The redirect reconnects it. *(Open: confirm in Ghidra whether the packaged shell uses iframe vs fetch — only needed if we ever want to keep the real shell instead of redirecting.)*

## Parity matrix

Status legend: ✅ implemented · 🟡 partial · 🔴 stub/no-op · ⬛ missing · ⚪ N/A for mb (handled by mb itself or irrelevant to ReCap).

### P0 — broken for current features — ✅ ALL IMPLEMENTED 2026-06-03 (pending rebuild + in-game test)

| Capability | Original | mb shim | Implementation |
|---|---|---|---|
| `game://` scheme (fonts/images/other UI pages) | host `TransportHandler` for `game://` | ✅ | `mapGameUrl`: `announcement.html` → `/web/sporelabsgame/announceen`; any other `game:///X` → `http://localhost/game/X`. Applied in `MbLoadURL` (top-level) **and** `mbOnLoadUrlBegin`+`mbNetChangeRequestUrl` (subresources). **Server contract:** ReCap.Server must serve recreated packaged pages under `/game/<path>` (nothing there yet — only announce is mapped to real content) |
| JS bridge: object/array args | full marshalling (`JavascriptValue` array/object) | ✅ | nested `{}`/`[]` args cross as their **raw JSON text** (kJsString). Faithful for everything live: our pages and the game's stricmp handler consume strings. Build real array/object `JavascriptValue`s only if a page ever needs structure |
| JS sync return value (`EvaluateJavaScript`) | synchronous return | ✅ | `MbRunJsSync` (mbRunJsSync + `mbGetGlobalExecByFrame` + `mbJsTo*`), null-export-guarded with async fallback. Note: JS→native sync (`var x=Client.getX()`) remains async-only — mbQuery has no sync channel; **no live page needs it** (register error was XHR readyState, not this) |
| `SetHTML` / `SetContent` | WebCore loader | ✅ | mb branch → `MbLoadHTML` (`mbLoadHtmlWithBaseUrl`, NUL-safe copy, baseURL honored) |

### P1 — needed for real/robust pages — implemented 2026-06-03 except where noted

| Capability | Original | mb shim | Status |
|---|---|---|---|
| Transparency / `mbTransparentBackground` | `setTranparentBackground` + transparent surface | ✅ | `MbSetTransparent` from `vp.mbTransparentBackground` in `InitView`; alpha policy moved into the HDC paint thunk (opaque → force 0xFF; transparent → keep mb alpha + one-shot census log). **Open evidence:** whether mb's HDC path actually carries alpha, and whether the announce quad renders alpha=0 invisible — the census log + in-game test answer both |
| Cookies (login/session persistence) | `SetCookieUsage`/`AddCookie` on WebCore CookieManager (`EAWebKit.cpp:426-475`) | ✅ | `setupCookieJar` in `MbCreate`: `mbSetCookieEnabled(TRUE)` + `mbSetCookieJarFullPath` → `recapmb_cookies.dat` next to the live EAWebKit.dll image (survives DLL reload) |
| Child windows / popups (`window.open`, `openChildPage` — a bound Client method!) | `ViewNotification::CreateView` | 🟡 | `mbOnCreateView` registered: **log + suppress** (`NULL_WEBVIEW`) so stray popups can't spawn OS windows. The `ViewNotification::CreateView` bridge waits for a page that needs it |
| Alert/confirm/prompt | WebCore dialogs | ✅ | `mbOnAlertBox/ConfirmBox/PromptBox` registered: log, never show native dialogs (confirm auto-TRUE, prompt auto-cancel). Context menu disabled too (`mbSetContextMenuEnabled(FALSE)`) |
| Navigation intercept / link handling | `ViewNotification::LinkSelected`, domain filter | 🟡 | `mbOnNavigation` registered: **log + allow all**. External links in our pages go through `Client.openExternalBrowser` (bound method), not navigation — add a domain filter only if a rogue navigation shows in the log |
| User-Agent | `Parameters.mpUserAgent` → WebCore | ✅ | `MbSetUserAgent` in `InitView`: `"<mpUserAgent> EAWebKit/<EAWEBKIT_VERSION>"` (pages gate on `"EAWebKit"` substring) |
| Reload / history (`Refresh`,`GoBack/Forward`,`CancelLoad`,`GetURI`) | WebCore | ✅ | mb branches: `mbReload`/`mbGoBack`/`mbGoForward`/`mbStopLoading`/`mbGetUrl`. Caveat: mb history depth is async-only — `GoBack/Forward` dispatch and return true |
| Keyboard modifiers / IME | full flags + IME codes | 🟡 | unchanged: Shift/Ctrl arrive as their own VK key events which mb's engine tracks itself; repeat/extended flags still 0. IME untested. Revisit only if a page shows a concrete failure |
| Mouse modifiers | full | ✅ | `MbFireMouseMove/Button` now take shift/ctrl → `MB_SHIFT`/`MB_CONTROL` flags |
| Text input into focused field (`EnterTextIntoSelectedInput`) | WebCore DOM | 🔴 | unchanged no-op; typing works natively (recapmb_type smoke) — no live caller observed |
| Clipboard (copy/paste in inputs) | `ViewNotification::ClipboardEvent` | ⬛ | unchanged — `mbEditorCopy`/`mbEditorPaste` when needed |
| JS properties / unregister method | register/unregister property+method | 🔴 | unchanged no-op — implement in JS bridge if any page uses properties |

### P2 — robustness / parity nice-to-have

| Capability | mb shim | Fix |
|---|---|---|
| Title/URL change notifications | ⬛ | `mbOnTitleChanged`/`mbOnURLChanged` → `LoadUpdate` title/uri |
| Download handling | ⬛ | `mbOnDownload` |
| `localStorage` persistence | ⬛ | `mbSetLocalStorageFullPath` |
| mb disk/RAM cache | ⬛ (EAWebKit cache calls hit WebCore) | `mbSetDiskCacheEnabled/Path/Limit`, `mbSetMemoryCacheEnable` |
| Proxy | ⬛ | `mbSetProxy` (likely keep off) |
| Context menu (right-click) | ✅ 2026-06-03 | suppressed in `MbCreate` |
| Clean shutdown | 🟡 `mbUninit` not called (`EAWebKit.cpp:794-822`) | call `mbUninit` in `Shutdown` |
| `GetURI` | ✅ 2026-06-03 | `mbGetUrl` mb branch |
| `GetEstimatedProgress` | 🔴 null (`EAWebKitView.cpp:1697`) | no mb equivalent worth wiring |
| Network error / XHR / auth notifications | ⬛ (mb's stack) | `mbOnLoadUrlFail`, `mbNetOnResponse` → map to `ViewNotification::NetworkError/XMLHttpRequestEvent/Authenticate` |
| SSL cert policy | 🔴 DirtySDK-only (`EAWebKit.cpp:1498`) | rely on `ReCapHooks` cert bypass (already in place) or mb cert config |

### P3 — N/A or out of scope for ReCap

Gamepad/D-pad navigation (`JumpToNearestElement`, `AdvanceFocus`) 🔴 — launcher UI is mouse-driven. Font server / glyph cache ⚪ — mb uses Skia (handle fonts via CSS `@font-face`, see [`webview-fonts-miniblink`]). DirtySDK `PlatformSocketAPI` ⚪ — mb has its own network stack. Movie/`<embed>` surface, printing, DevTools, profiling (`ViewProcessStatus`), drag&drop, custom scrollbar/button/focus-ring draw, overlay/modal `<select>` popups, EAText effects — all ⬛/⚪, not needed for the current panels.

## What IS solidly implemented (don't re-touch)

mbInit + DLL-reload-safe hidden window class (`ReCapMiniBlink.cpp:93-124`); offscreen resize + DIB; both paint paths (HDC primary, BGRA fallback); paint→ISurface copy; mouse down/up with capture-redirect fix (`ReCapMiniBlink.cpp:79-91`); wheel; focus; `:hover` JS shim (mb ignores synthetic mousemove); document-ready → 5 `LoadUpdate` events; Tick/`mbWake` with coalesced dirty; `CreateJavascriptBindings`/`RegisterJavascriptMethod` + re-injection on every new script context (`ReCapJsBridge.cpp:399-451`); console/error capture; WM_QUIT re-post for Play/Exit (`ReCapMiniBlink.cpp:67-91` + hooks); persistent `recapmb.log`.

## Status 2026-06-03: P0 + P1 implemented — awaiting one VS2008 rebuild + in-game test

All P0 rows and the actionable P1 rows above were implemented in one pass (plus the register
JSON error, which turned out to be server-side JS, and the UA gate, found while tracing it).
What the in-game test must answer:

1. Register flow completes end-to-end (account created → success modal → **Ok closes the
   window** — exercises both the XHR fix and the UA gate).
2. Announce slot: page renders via the general `game://` mapping; alpha census line
   (`transparent paint census: N/M px alpha>0`) in recapmb.log tells whether mb's HDC path
   carries real alpha; visually: empty announce = invisible, not black.
3. No popup/dialog regressions (watch for `mb createView suppressed` / `JS alert` log lines).

## Open items (need Ghidra / runtime)

- Confirm the packaged announce shell's fetch mechanism (iframe vs fetch) and exact URL — only if we choose to keep the real shell over the redirect. *(Ghidra agent was stopped; server-side URL already proven via old 404.)*
- Confirm whether the announce view's 230×346 is fixed or scales with resolution (View `InitView` w/h source). See [`webview-dimensions`].
- Verify whether the game's announce composit quad renders alpha=0 as invisible vs black (the launcher quad renders it black) — the transparency implementation + census log now make this directly observable.
- ReCap.Server: decide whether to serve recreated packaged UI under `/game/<path>` (the general `game://` mapping's target). Today only the announce page has real content.
