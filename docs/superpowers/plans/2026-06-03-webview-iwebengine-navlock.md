# Webview IWebEngine + Navigation Lock — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put the MiniBlink web backend behind a neutral `IWebEngine`/`IWebView` interface (swap-ready), and confine the engine's top-level navigation to first-party origins (external links → system browser).

**Architecture:** Thin-delegation abstraction — a pure C++ interface whose MiniBlink implementation forwards to the existing, untouched `recap::Mb*` functions. `EAWebKitView` holds an `IWebView*` instead of a raw `MbView*`. Nav-lock is enforced in the already-registered `mbOnNavigation` / `mbOnCreateView` callbacks via an `isFirstParty(url)` allowlist (`localhost`/`127.0.0.1`/`game://`/`about:blank`).

**Tech Stack:** C++ (VS2008 / VC9), MiniBlink (mb132), Win32 (`ShellExecuteW`). Spec: `docs/superpowers/specs/2026-06-03-webview-iwebengine-navlock-design.md`.

## Project realities (read first)

- `C:\CodingProjects\Personal\eawebkit\source\` is a **non-git tree** with **no unit-test harness**. There are NO git commits and NO pytest-style tests in this plan.
- **Per-task verification = compile-check** under `/W4 /WX`: `cmd //c "C:\CodingProjects\Personal\eawebkit\tests\_chk_w4.bat"`. It compiles every `source\ReCap*.cpp` — so all new backend files are named `ReCap*` to be covered automatically. (`EAWebKitView.cpp` is NOT covered by the chk; it only compiles in the full VS2008 build — its task uses a grep sanity check instead.)
- **Final validation = full VS2008 build + in-game smoke** (done by the user): open `projects/VS2008/EAWebKit/1.21.00.darkspore/EAWebKit.sln`, build `pc-vc-dev-opt|Win32`, deploy the DLL, run Darkspore.
- Naming: files use the `ReCap*` prefix (project convention + chk coverage). The C++ *interface types* keep their logical names `IWebView` / `IWebEngine` / `MiniBlinkWebView` / `MiniBlinkEngine` inside `ReCapWebEngine.h` / `ReCapMiniBlinkBackend.*`. (This refines the spec's tentative filenames `IWebEngine.h`/`MiniBlinkBackend.cpp`.)

## File structure

- Create `eawebkit/source/ReCapWebEngine.h` — pure `IWebView`/`IWebEngine` interfaces + `GetWebEngine()` decl.
- Create `eawebkit/source/ReCapMiniBlinkBackend.h` — `MiniBlinkWebView`/`MiniBlinkEngine` class decls.
- Create `eawebkit/source/ReCapMiniBlinkBackend.cpp` — delegating implementations + `GetWebEngine()`.
- Modify `eawebkit/source/ReCapMiniBlink.cpp` — nav-lock (`isFirstParty`, `openInSystemBrowser`, updated `navigationThunk`/`createViewThunk`, `<shellapi.h>`).
- Modify `eawebkit/source/EAWebKitView.cpp` — route the ~20 `recap::Mb*` call sites through `IWebView*`.
- Modify `eawebkit/projects/VS2008/EAWebKit/1.21.00.darkspore/EAWebkit.vcproj` — add `ReCapMiniBlinkBackend.cpp` to the build.

---

### Task 1: Navigation lock (ReCapMiniBlink.cpp)

Independent of the abstraction. Adds an origin allowlist and routes external top-level navigations + external popups to the system browser.

**Files:**
- Modify: `eawebkit/source/ReCapMiniBlink.cpp`

- [ ] **Step 1: Add the shellapi include + lib pragma**

In `ReCapMiniBlink.cpp`, the top currently has (after the mb.h block):
```c
#include "ReCapMiniBlink.h"
#include "ReCapJsBridge.h"   /* MbRunJs — used to drive the CSS-:hover shim (mb ignores synthetic MOVE) */
#include "ReCapLog.h"
```
Change it to add shellapi (for `ShellExecuteW`) and the lib pragma:
```c
#include "ReCapMiniBlink.h"
#include "ReCapJsBridge.h"   /* MbRunJs — used to drive the CSS-:hover shim (mb ignores synthetic MOVE) */
#include "ReCapLog.h"
#include <shellapi.h>        /* ShellExecuteW — nav-lock opens external links in the OS browser */
#pragma comment(lib, "shell32.lib")
```

- [ ] **Step 2: Add the first-party allowlist + system-browser helper**

Add these two helpers immediately ABOVE the existing `navigationThunk` function (find `static BOOL MB_CALL_TYPE navigationThunk`):
```c
/* Nav-lock allowlist. The engine only ever legitimately loads our own server (which the
   socket-level redirect resolves to local or the configured remote) or the packaged game://
   scheme. Everything else is third-party and must not render in the engine. */
static bool hostIs(const char* host, const char* name)
{
    size_t n = strlen(name);
    return strncmp(host, name, n) == 0 && (host[n] == 0 || host[n] == '/' || host[n] == ':');
}
static bool isFirstParty(const char* url)
{
    if (!url) return false;
    if (strncmp(url, "game://", 7) == 0) return true;
    if (strncmp(url, "about:blank", 11) == 0) return true;
    const char* host = 0;
    if      (strncmp(url, "http://",  7) == 0) host = url + 7;
    else if (strncmp(url, "https://", 8) == 0) host = url + 8;
    if (!host) return false;
    return hostIs(host, "localhost") || hostIs(host, "127.0.0.1");
}
static void openInSystemBrowser(const char* url)
{
    if (!url) return;
    WCHAR w[1024]; int n = 0;
    for (; url[n] && n < 1023; ++n) w[n] = (WCHAR)(unsigned char)url[n];
    w[n] = 0;
    ShellExecuteW(NULL, L"open", w, NULL, NULL, SW_SHOWNORMAL);
    MbLogf("nav-lock: external -> system browser: %s", url);
}
```

- [ ] **Step 3: Enforce in navigationThunk (top-level nav)**

Replace the existing:
```c
static BOOL MB_CALL_TYPE navigationThunk(mbWebView, void*, mbNavigationType type, const utf8* url)
{
    MbLogf("mb navigate[%d]: %s", (int)type, url ? url : "");
    return TRUE;
}
```
with:
```c
static BOOL MB_CALL_TYPE navigationThunk(mbWebView, void*, mbNavigationType, const utf8* url)
{
    if (isFirstParty(url)) return TRUE;        /* allow first-party page loads */
    openInSystemBrowser(url);                  /* external link -> OS browser */
    return FALSE;                              /* cancel in-engine navigation */
}
```

- [ ] **Step 4: Enforce in createViewThunk (window.open / target=_blank)**

Replace the existing:
```c
static mbWebView MB_CALL_TYPE createViewThunk(mbWebView, void*, mbNavigationType,
                                              const utf8* url, const mbWindowFeatures*)
{
    MbLogf("mb createView suppressed: %s", url ? url : "(null)");
    return NULL_WEBVIEW;
}
```
with:
```c
static mbWebView MB_CALL_TYPE createViewThunk(mbWebView, void*, mbNavigationType,
                                              const utf8* url, const mbWindowFeatures*)
{
    if (!isFirstParty(url)) openInSystemBrowser(url);   /* external popup -> OS browser */
    return NULL_WEBVIEW;                                /* never spawn a child OS window */
}
```

- [ ] **Step 5: Compile-check**

Run: `cmd //c "C:\CodingProjects\Personal\eawebkit\tests\_chk_w4.bat"`
Expected: prints `ReCapMiniBlink.cpp` / `ReCapJsBridge.cpp` / `ReCapLog.cpp` with NO error/warning lines (clean `/W4 /WX`). Any `error C####` or `warning C####` = fix before moving on.

---

### Task 2: IWebEngine interface (ReCapWebEngine.h)

Pure interface, no mb/EAWebKit types. Boundary types reuse the existing neutral typedefs.

**Files:**
- Create: `eawebkit/source/ReCapWebEngine.h`

- [ ] **Step 1: Create the header**

Create `eawebkit/source/ReCapWebEngine.h` with exactly:
```cpp
#ifndef RECAP_WEBENGINE_H
#define RECAP_WEBENGINE_H

/* Engine-neutral web view abstraction. EAWebKitView talks to IWebView/IWebEngine so the
   underlying engine (today MiniBlink) can be swapped without touching the consumer. Boundary
   types are the already-neutral typedefs from the mb wrapper headers (no mb/EAWebKit types here). */

#include "ReCapMiniBlink.h"   /* MbPaintSink, MbLoadSink */
#include "ReCapJsBridge.h"    /* JsVal, MbJsSink */

namespace recap {

struct IWebView {
    virtual ~IWebView() {}

    virtual void LoadURL(const char* url) = 0;
    virtual void LoadHTML(const char* html, const char* baseUrl) = 0;
    virtual void Resize(int w, int h) = 0;
    virtual void Wake() = 0;
    virtual int  ConsumeDirty() = 0;
    virtual void SetLoadSink(MbLoadSink sink, void* user) = 0;

    virtual void FireMouseMove(int x, int y, int shift, int ctrl) = 0;
    virtual void FireMouseButton(int button, int down, int x, int y, int shift, int ctrl) = 0;
    virtual void FireWheel(int x, int y, int delta) = 0;
    virtual void FireKey(unsigned id, int isChar, int down) = 0;
    virtual void SetFocus(int focus) = 0;

    virtual void SetUserAgent(const char* ua) = 0;
    virtual void SetTransparent(int transparent) = 0;
    virtual void Reload() = 0;
    virtual void StopLoading() = 0;
    virtual int  GoBack() = 0;
    virtual int  GoForward() = 0;
    virtual int  GetURL(char* buf, unsigned bufSize) = 0;

    virtual void RunJs(const char* script) = 0;
    virtual int  RunJsSync(const char* script, JsVal* ret, char* strBuf, unsigned strBufSize) = 0;
    virtual void CreateJsObject(const char* obj) = 0;
    virtual void BindJsMethod(const char* obj, const char* method, MbJsSink sink, void* user) = 0;

    virtual void Destroy() = 0;   /* tears down the view AND deletes this */
};

struct IWebEngine {
    virtual ~IWebEngine() {}
    virtual bool      Available() = 0;
    virtual IWebView* CreateView(int w, int h, MbPaintSink sink, void* user) = 0;  /* null on failure */
};

/* Process-wide engine singleton (MiniBlink today). */
IWebEngine* GetWebEngine();

} // namespace recap

#endif /* RECAP_WEBENGINE_H */
```

- [ ] **Step 2: Compile-check (header is exercised once Task 3's .cpp includes it; for now verify it parses)**

Run: `cmd //c "C:\CodingProjects\Personal\eawebkit\tests\_chk_w4.bat"`
Expected: still clean (the header isn't compiled alone yet, but this confirms no accidental break to the existing ReCap*.cpp). The header is fully exercised in Task 3.

---

### Task 3: MiniBlink backend (ReCapMiniBlinkBackend.{h,cpp})

Implements the interface by delegating to the existing `recap::Mb*` functions. No mb core changes.

**Files:**
- Create: `eawebkit/source/ReCapMiniBlinkBackend.h`
- Create: `eawebkit/source/ReCapMiniBlinkBackend.cpp`

- [ ] **Step 1: Create the backend header**

Create `eawebkit/source/ReCapMiniBlinkBackend.h` with exactly:
```cpp
#ifndef RECAP_MINIBLINK_BACKEND_H
#define RECAP_MINIBLINK_BACKEND_H

#include "ReCapWebEngine.h"

namespace recap {

/* IWebView backed by a MiniBlink MbView*. Every method forwards to the recap::Mb* wrapper. */
class MiniBlinkWebView : public IWebView {
public:
    explicit MiniBlinkWebView(MbView* view) : m_view(view) {}

    void LoadURL(const char* url);
    void LoadHTML(const char* html, const char* baseUrl);
    void Resize(int w, int h);
    void Wake();
    int  ConsumeDirty();
    void SetLoadSink(MbLoadSink sink, void* user);

    void FireMouseMove(int x, int y, int shift, int ctrl);
    void FireMouseButton(int button, int down, int x, int y, int shift, int ctrl);
    void FireWheel(int x, int y, int delta);
    void FireKey(unsigned id, int isChar, int down);
    void SetFocus(int focus);

    void SetUserAgent(const char* ua);
    void SetTransparent(int transparent);
    void Reload();
    void StopLoading();
    int  GoBack();
    int  GoForward();
    int  GetURL(char* buf, unsigned bufSize);

    void RunJs(const char* script);
    int  RunJsSync(const char* script, JsVal* ret, char* strBuf, unsigned strBufSize);
    void CreateJsObject(const char* obj);
    void BindJsMethod(const char* obj, const char* method, MbJsSink sink, void* user);

    void Destroy();

private:
    MbView* m_view;
};

class MiniBlinkEngine : public IWebEngine {
public:
    bool      Available();
    IWebView* CreateView(int w, int h, MbPaintSink sink, void* user);
};

} // namespace recap

#endif /* RECAP_MINIBLINK_BACKEND_H */
```

- [ ] **Step 2: Create the backend implementation**

Create `eawebkit/source/ReCapMiniBlinkBackend.cpp` with exactly:
```cpp
#include "ReCapMiniBlinkBackend.h"

namespace recap {

void MiniBlinkWebView::LoadURL(const char* url)                 { MbLoadURL(m_view, url); }
void MiniBlinkWebView::LoadHTML(const char* html, const char* baseUrl) { MbLoadHTML(m_view, html, baseUrl); }
void MiniBlinkWebView::Resize(int w, int h)                    { MbResize(m_view, w, h); }
void MiniBlinkWebView::Wake()                                  { MbWake(m_view); }
int  MiniBlinkWebView::ConsumeDirty()                          { return MbConsumeDirty(m_view); }
void MiniBlinkWebView::SetLoadSink(MbLoadSink sink, void* user){ MbSetLoadSink(m_view, sink, user); }

void MiniBlinkWebView::FireMouseMove(int x, int y, int shift, int ctrl) { MbFireMouseMove(m_view, x, y, shift, ctrl); }
void MiniBlinkWebView::FireMouseButton(int button, int down, int x, int y, int shift, int ctrl) { MbFireMouseButton(m_view, button, down, x, y, shift, ctrl); }
void MiniBlinkWebView::FireWheel(int x, int y, int delta)      { MbFireWheel(m_view, x, y, delta); }
void MiniBlinkWebView::FireKey(unsigned id, int isChar, int down) { MbFireKey(m_view, id, isChar, down); }
void MiniBlinkWebView::SetFocus(int focus)                     { MbSetFocus(m_view, focus); }

void MiniBlinkWebView::SetUserAgent(const char* ua)           { MbSetUserAgent(m_view, ua); }
void MiniBlinkWebView::SetTransparent(int transparent)        { MbSetTransparent(m_view, transparent); }
void MiniBlinkWebView::Reload()                              { MbReload(m_view); }
void MiniBlinkWebView::StopLoading()                         { MbStopLoading(m_view); }
int  MiniBlinkWebView::GoBack()                             { return MbGoBack(m_view); }
int  MiniBlinkWebView::GoForward()                         { return MbGoForward(m_view); }
int  MiniBlinkWebView::GetURL(char* buf, unsigned bufSize)  { return MbGetURL(m_view, buf, bufSize); }

void MiniBlinkWebView::RunJs(const char* script)            { MbRunJs(m_view, script); }
int  MiniBlinkWebView::RunJsSync(const char* script, JsVal* ret, char* strBuf, unsigned strBufSize) { return MbRunJsSync(m_view, script, ret, strBuf, strBufSize); }
void MiniBlinkWebView::CreateJsObject(const char* obj)      { MbCreateJsObject(m_view, obj); }
void MiniBlinkWebView::BindJsMethod(const char* obj, const char* method, MbJsSink sink, void* user) { MbBindJsMethod(m_view, obj, method, sink, user); }

void MiniBlinkWebView::Destroy()                            { MbDestroy(m_view); m_view = 0; delete this; }

bool MiniBlinkEngine::Available() { return MbAvailable(); }

IWebView* MiniBlinkEngine::CreateView(int w, int h, MbPaintSink sink, void* user)
{
    MbView* v = MbCreate(w, h, sink, user);
    if (!v) return 0;
    return new MiniBlinkWebView(v);
}

IWebEngine* GetWebEngine()
{
    static MiniBlinkEngine s_engine;
    return &s_engine;
}

} // namespace recap
```

- [ ] **Step 3: Compile-check**

Run: `cmd //c "C:\CodingProjects\Personal\eawebkit\tests\_chk_w4.bat"`
Expected: prints `ReCapMiniBlink.cpp`, `ReCapMiniBlinkBackend.cpp`, `ReCapJsBridge.cpp`, `ReCapLog.cpp` — all clean (no `error`/`warning` lines). `ReCapMiniBlinkBackend.cpp` now appears because it matches `ReCap*.cpp`. If it does NOT appear, the chk glob didn't pick it up — confirm the filename starts with `ReCap`.

---

### Task 4: Route EAWebKitView through IWebView

Mechanical: `mpRecapMb` now holds an `IWebView*`; every `recap::MbX((recap::MbView*)mpRecapMb, ...)` becomes `((recap::IWebView*)mpRecapMb)->X(...)`.

**Files:**
- Modify: `eawebkit/source/EAWebKitView.cpp`

- [ ] **Step 1: Add the include**

Find the existing block near the top:
```cpp
#ifdef RECAP_MINIBLINK
#include "ReCapMiniBlink.h"
#include "ReCapLog.h"
#include "ReCapJsBridge.h"
```
Add the engine header:
```cpp
#ifdef RECAP_MINIBLINK
#include "ReCapMiniBlink.h"
#include "ReCapLog.h"
#include "ReCapJsBridge.h"
#include "ReCapWebEngine.h"
```

- [ ] **Step 2: InitView — create via the engine**

Find (in `View::InitView`):
```cpp
        int w = mpSurface->GetWidth(), h = mpSurface->GetHeight();
        mpRecapMb = recap::MbCreate(w, h, RecapMbPaintSink, this);
        recap::MbLog(mpRecapMb ? "View::InitView mb ready" : "View::InitView mb create failed -> WebCore");
        if (mpRecapMb)
        {
```
Replace with:
```cpp
        int w = mpSurface->GetWidth(), h = mpSurface->GetHeight();
        mpRecapMb = recap::GetWebEngine()->CreateView(w, h, RecapMbPaintSink, this);
        recap::MbLog(mpRecapMb ? "View::InitView web engine ready" : "View::InitView engine create failed -> WebCore");
        if (mpRecapMb)
        {
```
Then, in the SAME `if (mpRecapMb)` block, find the body that calls the mb functions:
```cpp
                recap::MbSetUserAgent((recap::MbView*)mpRecapMb, userAgent);
            }
            recap::MbSetTransparent((recap::MbView*)mpRecapMb, vp.mbTransparentBackground ? 1 : 0);
            recap::MbSetLoadSink((recap::MbView*)mpRecapMb, RecapMbLoadThunk, this);
            recap::MbSetFocus((recap::MbView*)mpRecapMb, 1);   /* start focused so hover/click register */
            return (mpSurface != NULL);   /* engine is MiniBlink; skip WebCore */
```
Replace with:
```cpp
                ((recap::IWebView*)mpRecapMb)->SetUserAgent(userAgent);
            }
            ((recap::IWebView*)mpRecapMb)->SetTransparent(vp.mbTransparentBackground ? 1 : 0);
            ((recap::IWebView*)mpRecapMb)->SetLoadSink(RecapMbLoadThunk, this);
            ((recap::IWebView*)mpRecapMb)->SetFocus(1);   /* start focused so hover/click register */
            return (mpSurface != NULL);   /* engine ready; skip WebCore */
```
Also gate the `MbAvailable` check just above (find `recap::MbAvailable()`):
```cpp
    if (mpSurface && recap::MbAvailable())
```
Replace with:
```cpp
    if (mpSurface && recap::GetWebEngine()->Available())
```

- [ ] **Step 3: Replace the remaining call sites**

Each of the following is a single-line replacement (the cast changes from `(recap::MbView*)` to `(recap::IWebView*)` and the free function `recap::MbX(view, args)` becomes `view->X(args)`). Apply each:

| Method | Find | Replace |
|---|---|---|
| ShutdownView | `recap::MbDestroy((recap::MbView*)mpRecapMb); mpRecapMb = 0;` | `((recap::IWebView*)mpRecapMb)->Destroy(); mpRecapMb = 0;` |
| Resize | `recap::MbResize((recap::MbView*)mpRecapMb, w, h); return bResult;` | `((recap::IWebView*)mpRecapMb)->Resize(w, h); return bResult;` |
| SetURI | `recap::MbLoadURL((recap::MbView*)mpRecapMb, pURI); return true;` | `((recap::IWebView*)mpRecapMb)->LoadURL(pURI); return true;` |
| SetContent | `recap::MbLoadHTML((recap::MbView*)mpRecapMb, pHtml, pBaseURL);` | `((recap::IWebView*)mpRecapMb)->LoadHTML(pHtml, pBaseURL);` |
| Refresh | `recap::MbReload((recap::MbView*)mpRecapMb); return;` | `((recap::IWebView*)mpRecapMb)->Reload(); return;` |
| CancelLoad | `recap::MbStopLoading((recap::MbView*)mpRecapMb); return;` | `((recap::IWebView*)mpRecapMb)->StopLoading(); return;` |
| GoBack | `return recap::MbGoBack((recap::MbView*)mpRecapMb) != 0;` | `return ((recap::IWebView*)mpRecapMb)->GoBack() != 0;` |
| GoForward | `return recap::MbGoForward((recap::MbView*)mpRecapMb) != 0;` | `return ((recap::IWebView*)mpRecapMb)->GoForward() != 0;` |
| EvaluateJavaScript (sync) | `recap::MbRunJsSync((recap::MbView*)mpRecapMb, buf, &ret, strBuf, sizeof(strBuf));` | `((recap::IWebView*)mpRecapMb)->RunJsSync(buf, &ret, strBuf, sizeof(strBuf));` |
| EvaluateJavaScript (async) | `recap::MbRunJs((recap::MbView*)mpRecapMb, buf);` | `((recap::IWebView*)mpRecapMb)->RunJs(buf);` |
| Tick (wake) | `recap::MbWake((recap::MbView*)mpRecapMb);   /* pump MiniBlink each frame */` | `((recap::IWebView*)mpRecapMb)->Wake();   /* pump the web engine each frame */` |
| Tick (dirty) | `if (recap::MbConsumeDirty((recap::MbView*)mpRecapMb) && mpSurface)` | `if (((recap::IWebView*)mpRecapMb)->ConsumeDirty() && mpSurface)` |
| OnKeyboardEvent | `if (mpRecapMb) { recap::MbFireKey((recap::MbView*)mpRecapMb, keyboardEvent.mId, keyboardEvent.mbChar ? 1 : 0, keyboardEvent.mbDepressed ? 1 : 0); return; }` | `if (mpRecapMb) { ((recap::IWebView*)mpRecapMb)->FireKey(keyboardEvent.mId, keyboardEvent.mbChar ? 1 : 0, keyboardEvent.mbDepressed ? 1 : 0); return; }` |
| OnMouseMoveEvent | `if (mpRecapMb) { recap::MbFireMouseMove((recap::MbView*)mpRecapMb, mouseMoveEvent.mX, mouseMoveEvent.mY,` | `if (mpRecapMb) { ((recap::IWebView*)mpRecapMb)->FireMouseMove(mouseMoveEvent.mX, mouseMoveEvent.mY,` |
| OnMouseButtonEvent (focus) | `if (mouseButtonEvent.mbDepressed) recap::MbSetFocus((recap::MbView*)mpRecapMb, 1);` | `if (mouseButtonEvent.mbDepressed) ((recap::IWebView*)mpRecapMb)->SetFocus(1);` |
| OnMouseButtonEvent (button) | `recap::MbFireMouseButton((recap::MbView*)mpRecapMb, (int)mouseButtonEvent.mId,` | `((recap::IWebView*)mpRecapMb)->FireMouseButton((int)mouseButtonEvent.mId,` |
| OnMouseWheelEvent | `if (mpRecapMb) { recap::MbFireWheel((recap::MbView*)mpRecapMb, mouseWheelEvent.mX, mouseWheelEvent.mY, mouseWheelEvent.mZDelta); return; }` | `if (mpRecapMb) { ((recap::IWebView*)mpRecapMb)->FireWheel(mouseWheelEvent.mX, mouseWheelEvent.mY, mouseWheelEvent.mZDelta); return; }` |
| OnFocusChangeEvent | `if (mpRecapMb) { recap::MbSetFocus((recap::MbView*)mpRecapMb, bHasFocus ? 1 : 0); return; }` | `if (mpRecapMb) { ((recap::IWebView*)mpRecapMb)->SetFocus(bHasFocus ? 1 : 0); return; }` |
| CreateJavascriptBindings | `recap::MbCreateJsObject((recap::MbView*)mpRecapMb, GetFixedString(mJavascriptBindingObjectName)->c_str());` | `((recap::IWebView*)mpRecapMb)->CreateJsObject(GetFixedString(mJavascriptBindingObjectName)->c_str());` |
| RegisterJavascriptMethod | `recap::MbBindJsMethod((recap::MbView*)mpRecapMb,` | `((recap::IWebView*)mpRecapMb)->BindJsMethod(` |

Note for the multi-line `OnMouseMoveEvent`, `OnMouseButtonEvent (button)`, and `RegisterJavascriptMethod` sites: only the FIRST line changes (shown above); the continuation lines (the remaining arguments) stay exactly as-is. For `RegisterJavascriptMethod`, the original continuation is:
```cpp
			GetFixedString(mJavascriptBindingObjectName)->c_str(), name, RecapMbJsSink, this);
```
which remains unchanged after the `->BindJsMethod(` opener.

- [ ] **Step 4: Verify no raw mb call sites remain**

Run (Grep tool, or): `grep -nE "recap::Mb[A-Z]" eawebkit/source/EAWebKitView.cpp`
Expected: matches ONLY the thunk DEFINITIONS that legitimately stay — i.e. lines inside `RecapMbPaintSink` / `RecapMbLoadThunk` / `RecapMbJsSink` that call `recap::MbLog`/`recap::MbLogf` (logging) and the `recap::JsVal` / `recap::kJs*` type uses. There must be **zero** `recap::Mb` calls that take `(recap::MbView*)mpRecapMb` as an argument (those all became `->` calls). If any `(recap::MbView*)mpRecapMb` remains, convert it.

Run: `grep -nE "\(recap::MbView\*\)mpRecapMb" eawebkit/source/EAWebKitView.cpp`
Expected: no matches.

- [ ] **Step 5: Note — EAWebKitView.cpp is verified by the full build**

`EAWebKitView.cpp` is not covered by `_chk_w4.bat` (it needs the full WebCore/EAWebKit include tree). Its compile is verified in Task 5's full VS2008 build. The grep in Step 4 is the pre-build sanity gate.

---

### Task 5: Wire the build + final validation

**Files:**
- Modify: `eawebkit/projects/VS2008/EAWebKit/1.21.00.darkspore/EAWebkit.vcproj`

- [ ] **Step 1: Add ReCapMiniBlinkBackend.cpp to the project**

Open `EAWebkit.vcproj`. Find where the other ReCap shim sources are listed (search for `ReCapMiniBlink.cpp`). Add a sibling `<File>` entry next to it:
```xml
				<File
					RelativePath="..\..\..\..\source\ReCapMiniBlinkBackend.cpp"
					>
				</File>
```
Match the existing `RelativePath` style/prefix of the neighboring `ReCapMiniBlink.cpp` entry exactly (copy that entry and change the filename). Headers (`ReCapWebEngine.h`, `ReCapMiniBlinkBackend.h`) do not need project entries but may be added for IDE visibility.

- [ ] **Step 2: Compile-check the shim sources once more**

Run: `cmd //c "C:\CodingProjects\Personal\eawebkit\tests\_chk_w4.bat"`
Expected: `ReCapMiniBlink.cpp`, `ReCapMiniBlinkBackend.cpp`, `ReCapJsBridge.cpp`, `ReCapLog.cpp` all clean.

- [ ] **Step 3: Full build (USER)**

Open `eawebkit/projects/VS2008/EAWebKit/1.21.00.darkspore/EAWebKit.sln`, build `pc-vc-dev-opt|Win32`. Expected: builds with no errors (this is where `EAWebKitView.cpp` compiles). Deploy the resulting `EAWebkit.dll` + `mb132_x32.dll` next to `Darkspore.exe`.

- [ ] **Step 4: In-game smoke (USER)**

Launch Darkspore. Expected, from behavior + `recapmb.log`:
- **No functional regression** (IWebEngine is a pure refactor): launcher renders + hover + buttons; Play opens the game; in-game hub (`mainwebview.html` + iframes) renders and interacts; locale text present; quit works.
- **Nav-lock**: clicking an external link (e.g. a Terms-of-Service link → `tos.ea.com`) does NOT navigate the in-engine view; `recapmb.log` shows `nav-lock: external -> system browser: ...` and the link opens in the OS default browser. First-party pages (localhost/game://) and the iframes keep loading. External subresources (images from darkspore.com) still load (cosmetic).

---

## Self-review notes

- **Spec coverage:** Part 1 (IWebEngine) → Tasks 2–5; Part 2 (nav-lock) → Task 1. Threat-model/validation captured in spec; in-game validation in Task 5 Step 4. All spec interface methods (`LoadURL/LoadHTML/Resize/Wake/ConsumeDirty/SetLoadSink/Fire*/SetFocus/SetUserAgent/SetTransparent/Reload/StopLoading/GoBack/GoForward/GetURL/RunJs/RunJsSync/CreateJsObject/BindJsMethod/Destroy` + engine `Available/CreateView` + `GetWebEngine`) are defined in Task 2 and implemented in Task 3.
- **Signature consistency:** interface decls (Task 2) ↔ backend impls (Task 3) ↔ `recap::Mb*` wrappers (existing `ReCapMiniBlink.h`/`ReCapJsBridge.h`) cross-checked: `CreateView(w,h,MbPaintSink,user)`, `RunJsSync(script,JsVal*,char*,unsigned)`, `FireMouseButton(button,down,x,y,shift,ctrl)`, `GetURL(char*,unsigned)` all match.
- **No git commits / no unit tests:** intentional — non-git tree, no harness (see Project realities).
