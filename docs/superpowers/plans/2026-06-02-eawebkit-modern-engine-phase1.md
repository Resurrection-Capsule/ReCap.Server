# EAWebKit modern engine — Phase 1 (pixel path) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make MiniBlink (a modern Blink engine) render a modern-CSS test page into Darkspore's in-game web surface, behind EAWebKit's real `View`/`ISurface` ABI — proving the engine coexists in-process and the pixel path works.

**Architecture:** Inside the EAWebKit.dll we build, the real `View` keeps its ARGB `mpSurface` but its *renderer* is swapped from WebCore to MiniBlink (behind a `RECAP_MINIBLINK` compile flag). A small `recapwke` wrapper dynamically loads the MiniBlink runtime DLL (`GetProcAddress`) and drives an offscreen `wkeWebView` whose `wkeOnPaintBitUpdated` callback copies pixels into `mpSurface`.

**Tech Stack:** C++ (VC9 / EAWebKit), MiniBlink (`weolar/miniblink49`, prebuilt 32-bit runtime DLL + `wke.h`), Win32. Standalone smoke test compiled with MinGW gcc (`C:\Strawberry\c\bin\gcc.exe`).

**Spec:** `docs/superpowers/specs/2026-06-02-eawebkit-modern-engine-design.md`

**Scope:** Phase 1 only (foundation + pixel path). Phases 2 (JS bridge), 3 (input), 4 (modern content) get their own plans after this phase's in-game gate passes.

**Ground-truth paths:**
- MiniBlink source clone (has `wke.h`): `C:\CodingProjects\Personal\miniblink49\wke\wke.h`
- EAWebKit source: `C:\CodingProjects\Personal\eawebkit\source\`
- EAWebKit DLL project: `C:\CodingProjects\Personal\eawebkit\projects\VS2008\EAWebKit\1.21.00.darkspore\EAWebkit.vcproj`
- Built DLL: `C:\CodingProjects\Personal\eawebkit\Distribution\pc\9.0.21022\dev-opt\bin\EAWebkit.dll`

**Real wke API used (from `wke.h`):**
```c
void        wkeInitialize();
wkeWebView  wkeCreateWebView();
void        wkeResize(wkeWebView, int w, int h);
void        wkeSetTransparent(wkeWebView, bool);
void        wkeLoadHTML(wkeWebView, const char* utf8);
void        wkeLoadURL(wkeWebView, const char* utf8);
typedef void(*wkePaintBitUpdatedCallback)(wkeWebView, void* param, const void* buffer, const wkeRect* r, int w, int h);
void        wkeOnPaintBitUpdated(wkeWebView, wkePaintBitUpdatedCallback, void* param);
```
(`wkeRect` = `{int x,y,w,h;}`. The paint buffer is 32-bit BGRA, top-down, stride = w*4.)

---

## File Structure

| File | Responsibility |
|---|---|
| `eawebkit/source/recapwke.h` (NEW) | C++ API the View calls: `Available/Create/LoadHTML/LoadURL/Resize/SetPaintSink`. No wke types leak out. |
| `eawebkit/source/recapwke.cpp` (NEW) | Dynamically loads the MiniBlink DLL, resolves wke entry points, owns the offscreen `wkeWebView`, routes the paint callback to a sink. |
| `eawebkit/tests/recapwke_smoke.cpp` (NEW, dev-only) | Standalone exe: loads the DLL, renders test HTML offscreen, asserts the paint callback delivered a non-blank frame. Proves the engine works on this box before touching EAWebKit. |
| `eawebkit/source/EAWebKitView.cpp` (EDIT) | Under `#ifdef RECAP_MINIBLINK`: `InitView`/`SetURI`/`SetSize`/`Tick` drive `recapwke` and copy frames into `mpSurface`; `GetSurface` unchanged. |
| `EAWebkit.vcproj` (EDIT) | Compile `recapwke.cpp`; define `RECAP_MINIBLINK`; add `miniblink49\wke` to include dirs. |

`mMiniblinkPaint` membership: the View needs a per-instance paint sink. We store the latest frame in the existing `mpSurface` directly from the callback (the callback param carries the `View*`).

---

## Task 0: Obtain the MiniBlink runtime DLL (USER-RUN)

**Files:** none (download)

- [ ] **Step 1:** The cloned `miniblink49` repo is **source only** — it does not contain the prebuilt engine DLL. Download a prebuilt **32-bit** runtime from the project's GitHub *Releases* (e.g. `miniblink_x32.dll` / older `node.dll`). Place it at:
  `C:\CodingProjects\Personal\miniblink49\bin\miniblink.dll` (rename to `miniblink.dll`).
- [ ] **Step 2:** Confirm it is 32-bit:
  Run: `dumpbin /headers "C:\CodingProjects\Personal\miniblink49\bin\miniblink.dll" | findstr machine`
  Expected: `14C machine (x86)`.
- [ ] **Step 3:** Confirm it exports the wke API:
  Run: `dumpbin /exports "C:\CodingProjects\Personal\miniblink49\bin\miniblink.dll" | findstr wkeCreateWebView`
  Expected: a line listing `wkeCreateWebView`.

> The loader and smoke test use the path/name `miniblink.dll`. If you keep a different name, set `RECAP_MINIBLINK_DLL` accordingly in Task 1.

---

## Task 1: `recapwke.h` — the wrapper API

**Files:**
- Create: `C:\CodingProjects\Personal\eawebkit\source\recapwke.h`

- [ ] **Step 1: Write the header**

```cpp
#ifndef RECAP_WKE_H
#define RECAP_WKE_H

/* Modern-engine (MiniBlink) backend for the EAWebKit View. Hides all wke/DLL details.
   A paint sink receives 32-bit BGRA frames; the View copies them into its ISurface. */

namespace recap {

typedef void (*WkePaintSink)(void* user, const void* bgra, int srcStride,
                             int x, int y, int w, int h);

class WkeView; // opaque

/* True if the MiniBlink runtime DLL loaded and the wke entry points resolved. */
bool WkeAvailable();

/* Create an offscreen view sized w x h; frames are delivered to sink(user, ...). */
WkeView* WkeCreate(int w, int h, WkePaintSink sink, void* user);
void     WkeLoadHTML(WkeView*, const char* utf8Html);
void     WkeLoadURL(WkeView*, const char* utf8Url);
void     WkeResize(WkeView*, int w, int h);
void     WkeDestroy(WkeView*);

} // namespace recap

#endif /* RECAP_WKE_H */
```

- [ ] **Step 2: Commit** — none (eawebkit tree is not a git repo; record progress at end of plan).

---

## Task 2: `recapwke.cpp` — dynamic loader + offscreen view + smoke test (TDD)

**Files:**
- Create: `C:\CodingProjects\Personal\eawebkit\source\recapwke.cpp`
- Test: `C:\CodingProjects\Personal\eawebkit\tests\recapwke_smoke.cpp`

- [ ] **Step 1: Write the smoke test FIRST**

`tests/recapwke_smoke.cpp`:
```cpp
#include <windows.h>
#include <stdio.h>
#include "../source/recapwke.h"

static volatile int g_painted = 0;
static volatile int g_nonblank = 0;

static void onPaint(void* /*user*/, const void* bgra, int stride, int /*x*/, int /*y*/, int w, int h)
{
    g_painted = 1;
    const unsigned char* p = (const unsigned char*)bgra;
    for (int row = 0; row < h && !g_nonblank; ++row)
        for (int col = 0; col < w * 4; ++col)
            if (p[row * stride + col] != 0) { g_nonblank = 1; break; }
}

int main(void)
{
    if (!recap::WkeAvailable()) { printf("FAIL: MiniBlink DLL/symbols not available\n"); return 2; }
    recap::WkeView* v = recap::WkeCreate(640, 480, onPaint, 0);
    if (!v) { printf("FAIL: WkeCreate returned null\n"); return 3; }
    recap::WkeLoadHTML(v,
        "<html><body style='margin:0'>"
        "<div style='display:grid;grid-template-columns:1fr 1fr;width:640px;height:480px'>"
        "<div style='background:#f0f'></div><div style='background:#0ff'></div>"
        "<div style='background:#ff0'></div><div style='background:#0f0'></div>"
        "</div></body></html>");

    /* offscreen MiniBlink paints via the host message loop — pump ~3s */
    DWORD start = GetTickCount();
    MSG msg;
    while (GetTickCount() - start < 3000 && !g_nonblank)
    {
        while (PeekMessage(&msg, 0, 0, 0, PM_REMOVE)) { TranslateMessage(&msg); DispatchMessage(&msg); }
        Sleep(10);
    }
    recap::WkeDestroy(v);
    if (g_painted && g_nonblank) { printf("ALL PASS (painted + non-blank)\n"); return 0; }
    printf("FAIL: painted=%d nonblank=%d\n", g_painted, g_nonblank);
    return 1;
}
```

- [ ] **Step 2: Run the test — verify it fails (no implementation yet)**

Run (from `C:\CodingProjects\Personal\eawebkit`):
```
C:\Strawberry\c\bin\gcc.exe -m32 tests/recapwke_smoke.cpp source/recapwke.cpp -o tests/recapwke_smoke.exe -lstdc++
```
Expected: compile/link error (recapwke.cpp not written yet) OR if it builds, `FAIL: ... not available`. Either way, not `ALL PASS`.

> If MinGW gcc here is 64-bit only (no `-m32`), compile the test with the VS2008 `cl` instead:
> `cl /nologo /MT tests\recapwke_smoke.cpp source\recapwke.cpp /Fetests\recapwke_smoke.exe`

- [ ] **Step 3: Write `recapwke.cpp`**

```cpp
#include <windows.h>
#include "recapwke.h"

/* ---- minimal wke surface we use (names/sigs from miniblink49/wke/wke.h) -------- */
extern "C" {
typedef void* wkeWebView;
struct wkeRect { int x, y, w, h; };
typedef void (*wkePaintBitUpdatedCallback)(wkeWebView, void* param, const void* buffer,
                                           const wkeRect* r, int width, int height);
}

#ifndef RECAP_MINIBLINK_DLL
#define RECAP_MINIBLINK_DLL "miniblink.dll"
#endif

namespace recap {

struct WkeApi {
    void       (*Initialize)();
    wkeWebView (*CreateWebView)();
    void       (*Resize)(wkeWebView, int, int);
    void       (*SetTransparent)(wkeWebView, bool);
    void       (*LoadHTML)(wkeWebView, const char*);
    void       (*LoadURL)(wkeWebView, const char*);
    void       (*OnPaintBitUpdated)(wkeWebView, wkePaintBitUpdatedCallback, void*);
    void       (*DestroyWebView)(wkeWebView);
};

static HMODULE s_dll = 0;
static WkeApi  s_api;
static int     s_state = 0; /* 0=unprobed, 1=ok, -1=unavailable */

template <class T> static bool resolve(T& fn, const char* name) {
    fn = (T)GetProcAddress(s_dll, name);
    return fn != 0;
}

static bool ensureLoaded() {
    if (s_state) return s_state == 1;
    s_state = -1;
    s_dll = LoadLibraryA(RECAP_MINIBLINK_DLL);
    if (!s_dll) return false;
    bool ok = true;
    ok &= resolve(s_api.Initialize,        "wkeInitialize");
    ok &= resolve(s_api.CreateWebView,     "wkeCreateWebView");
    ok &= resolve(s_api.Resize,            "wkeResize");
    ok &= resolve(s_api.SetTransparent,    "wkeSetTransparent");
    ok &= resolve(s_api.LoadHTML,          "wkeLoadHTML");
    ok &= resolve(s_api.LoadURL,           "wkeLoadURL");
    ok &= resolve(s_api.OnPaintBitUpdated, "wkeOnPaintBitUpdated");
    ok &= resolve(s_api.DestroyWebView,    "wkeDestroyWebView");
    if (!ok) return false;
    s_api.Initialize();
    s_state = 1;
    return true;
}

struct WkeView {
    wkeWebView   wv;
    WkePaintSink sink;
    void*        user;
};

bool WkeAvailable() { return ensureLoaded(); }

static void __cdecl paintThunk(wkeWebView, void* param, const void* buffer,
                               const wkeRect* r, int width, int height) {
    WkeView* self = (WkeView*)param;
    if (self && self->sink && buffer && r)
        self->sink(self->user, buffer, width * 4, r->x, r->y, r->w, r->h);
    (void)height;
}

WkeView* WkeCreate(int w, int h, WkePaintSink sink, void* user) {
    if (!ensureLoaded()) return 0;
    WkeView* self = new WkeView();
    self->sink = sink; self->user = user;
    self->wv = s_api.CreateWebView();
    s_api.SetTransparent(self->wv, false);
    s_api.Resize(self->wv, w, h);
    s_api.OnPaintBitUpdated(self->wv, paintThunk, self);
    return self;
}

void WkeLoadHTML(WkeView* v, const char* html) { if (v) s_api.LoadHTML(v->wv, html); }
void WkeLoadURL(WkeView* v, const char* url)   { if (v) s_api.LoadURL(v->wv, url); }
void WkeResize(WkeView* v, int w, int h)       { if (v) s_api.Resize(v->wv, w, h); }
void WkeDestroy(WkeView* v)                    { if (v) { s_api.DestroyWebView(v->wv); delete v; } }

} // namespace recap
```

- [ ] **Step 4: Run the test — verify it passes**

Ensure `miniblink.dll` (from Task 0) is on the test's DLL search path (copy it next to `recapwke_smoke.exe`, i.e. into `tests\`). Rebuild and run:
```
C:\Strawberry\c\bin\gcc.exe -m32 tests/recapwke_smoke.cpp source/recapwke.cpp -o tests/recapwke_smoke.exe -lstdc++
copy C:\CodingProjects\Personal\miniblink49\bin\miniblink.dll tests\
tests\recapwke_smoke.exe
```
Expected: `ALL PASS (painted + non-blank)`, exit 0.
- If it prints `not available` → check the DLL name/bitness/exports (Task 0).
- If it paints but `nonblank=0` → the buffer may be delivered with a different stride; log `width`/`r` and adjust (still informative — engine works).

- [ ] **Step 5: Commit** — none (eawebkit tree).

---

## Task 3: Route the View renderer to MiniBlink (behind `RECAP_MINIBLINK`)

**Files:**
- Modify: `C:\CodingProjects\Personal\eawebkit\source\EAWebKitView.cpp`

Context: `View` has `mpSurface` (ARGB `EA::Raster::ISurface`) and `mpWebView` (WebCore). We keep `mpSurface` and, under the flag, drive MiniBlink instead of WebCore. Add a `recap::WkeView* mpRecapWke;` member.

- [ ] **Step 1: Add the include + member**

At the top of `EAWebKitView.cpp` includes, add:
```cpp
#ifdef RECAP_MINIBLINK
#include "recapwke.h"
#endif
```
In the `View` class definition (header `include/EAWebKit/EAWebKitView.h`, in the private members near `mpSurface`/`mpWebView`), add:
```cpp
#ifdef RECAP_MINIBLINK
    recap::WkeView* mpRecapWke;
#endif
```
And initialize it in the `View` ctor initializer list (where `mpSurface(0)` is, `EAWebKitView.cpp:359`):
```cpp
#ifdef RECAP_MINIBLINK
    , mpRecapWke(0)
#endif
```

- [ ] **Step 2: Add the paint sink (copies BGRA frame into `mpSurface`)**

Add this file-static function near the top of `EAWebKitView.cpp` (after includes):
```cpp
#ifdef RECAP_MINIBLINK
namespace {
void RecapWkePaintSink(void* user, const void* bgra, int srcStride,
                       int x, int y, int w, int h)
{
    EA::WebKit::View* view = (EA::WebKit::View*)user;
    EA::Raster::ISurface* s = view ? view->GetSurface() : 0;
    if (!s || !bgra) return;
    unsigned char* dst = (unsigned char*)s->GetData();
    int dstStride = s->GetStride();
    if (!dst) return;
    /* EA kPixelFormatTypeARGB == 0xAARRGGBB == BGRA byte order on LE; wke buffer is BGRA.
       Direct row copy of the dirty rect. */
    for (int row = 0; row < h; ++row)
    {
        const unsigned char* sp = (const unsigned char*)bgra + (size_t)(y + row) * srcStride + (size_t)x * 4;
        unsigned char* dp = dst + (size_t)(y + row) * dstStride + (size_t)x * 4;
        memcpy(dp, sp, (size_t)w * 4);
    }
}
} // namespace
#endif
```

- [ ] **Step 3: `InitView` — create the MiniBlink view instead of WebCore**

In `View::InitView` (`EAWebKitView.cpp:414`), AFTER `mpSurface` is created (the block around line 454-466) and BEFORE the WebCore `mpWebView` bring-up, add:
```cpp
#ifdef RECAP_MINIBLINK
    if (mpSurface && recap::WkeAvailable())
    {
        int w = mpSurface->GetWidth(), h = mpSurface->GetHeight();
        mpRecapWke = recap::WkeCreate(w, h, RecapWkePaintSink, this);
        if (mpRecapWke)
            return (mpSurface != NULL);   /* skip WebCore init; engine is MiniBlink */
    }
#endif
```
(If MiniBlink is unavailable, control falls through to the stock WebCore path — graceful fallback.)

- [ ] **Step 4: `SetURI` / `SetSize` / `Tick` — route to MiniBlink when active**

In `View::SetURI` (`:600`), at the very top of the body:
```cpp
#ifdef RECAP_MINIBLINK
    if (mpRecapWke) { recap::WkeLoadURL(mpRecapWke, pURI); return true; }
#endif
```
In `View::SetSize` (`:567`), after `mpSurface` is resized (around `:577-582`):
```cpp
#ifdef RECAP_MINIBLINK
    if (mpRecapWke) { recap::WkeResize(mpRecapWke, w, h); return true; }
#endif
```
In `View::Tick` (`:941`), at the top of the body:
```cpp
#ifdef RECAP_MINIBLINK
    if (mpRecapWke) return true;  /* MiniBlink paints async via the sink on the msg loop */
#endif
```

- [ ] **Step 5: Destroy in the View teardown**

In the View shutdown path that frees `mpSurface` (around `:542-547`), before freeing the surface add:
```cpp
#ifdef RECAP_MINIBLINK
    if (mpRecapWke) { recap::WkeDestroy(mpRecapWke); mpRecapWke = 0; }
#endif
```

- [ ] **Step 6: Verify (deferred to Task 5 build)** — no standalone compile here (needs the full EAWebKit graph). Visually confirm each `#ifdef RECAP_MINIBLINK` block is balanced and the member/ctor-init/include are present.

- [ ] **Step 7: Commit** — none (eawebkit tree).

---

## Task 4: Wire the EAWebKit project (`EAWebkit.vcproj`)

**Files:**
- Modify: `C:\CodingProjects\Personal\eawebkit\projects\VS2008\EAWebKit\1.21.00.darkspore\EAWebkit.vcproj`

- [ ] **Step 1: Add `recapwke.cpp` to the file list**

Find the `<File RelativePath="..\..\..\..\source\recaphooks.cpp">` block (added by the redirect work) and insert a sibling immediately before it:
```xml
      <File RelativePath="..\..\..\..\source\recapwke.cpp">
        <FileConfiguration Name="pc-vc-dev-debug|Win32">
          <Tool Name="VCCLCompilerTool" ObjectFile="pc-vc-dev-debug\build\EAWebkit\vcproj\source\recapwke.cpp.obj" />
        </FileConfiguration>
        <FileConfiguration Name="pc-vc-dev-opt|Win32">
          <Tool Name="VCCLCompilerTool" ObjectFile="pc-vc-dev-opt\build\EAWebkit\vcproj\source\recapwke.cpp.obj" />
        </FileConfiguration>
      </File>
```

- [ ] **Step 2: Define `RECAP_MINIBLINK` + add the wke include dir**

In the `pc-vc-dev-opt|Win32` configuration's `VCCLCompilerTool`, append to `PreprocessorDefinitions` the token `RECAP_MINIBLINK`, and append to `AdditionalIncludeDirectories` the path `C:\CodingProjects\Personal\miniblink49\wke` (so `#include "wke.h"` would resolve — though `recapwke.cpp` declares the minimal wke surface itself and does not include `wke.h`; the include dir is harmless and future-proof).
Read the existing `VCCLCompilerTool` line for `pc-vc-dev-opt|Win32`, then add the two tokens to the respective semicolon-separated attributes. Example shape (your existing line will have more values — append, don't replace):
```
PreprocessorDefinitions="...existing...;RECAP_MINIBLINK"
AdditionalIncludeDirectories="...existing...;C:\CodingProjects\Personal\miniblink49\wke"
```

- [ ] **Step 3: Verify XML well-formedness**

Run:
```
C:\Strawberry\c\bin\perl.exe -MXML::Simple -e "XMLin('C:/CodingProjects/Personal/eawebkit/projects/VS2008/EAWebKit/1.21.00.darkspore/EAWebkit.vcproj'); print qq{OK\n}"
```
Expected: `OK`. (If `XML::Simple` is absent, visually confirm the new `<File>` block + edited attributes are balanced.)

- [ ] **Step 4: Commit** — none (eawebkit tree).

---

## Task 5: Build, deploy, in-game gate (USER-RUN)

**Files:** none

- [ ] **Step 1: Build** the `EAWebKit.sln` `pc-vc-dev-opt|Win32` config in VS2008.
  Expected: `0 failed`. `recapwke.cpp` compiles; `EAWebKitView.cpp` compiles with `RECAP_MINIBLINK` defined.
  - If `recap::WkeView` is undefined in `EAWebKitView.cpp` → confirm `#include "recapwke.h"` under the flag (Task 3 Step 1).
  - If `GetStride`/`GetData`/`GetWidth` aren't on `ISurface` → they are (used elsewhere in this file); check the `EA::Raster::ISurface` include is in scope.

- [ ] **Step 2: Deploy**
  1. Back up the working `EAWebkit.dll` next to `Darkspore.exe`.
  2. Copy the new `Distribution\pc\9.0.21022\dev-opt\bin\EAWebkit.dll` over it.
  3. Copy `C:\CodingProjects\Personal\miniblink49\bin\miniblink.dll` next to `Darkspore.exe`.
  4. Keep `recap.cfg` in place (the redirect still applies).

- [ ] **Step 3: In-game gate**
  Launch Darkspore.
  - **PASS:** where the web UI normally is, the MiniBlink-rendered page appears. For a pure pixel-path check before wiring real pages, temporarily point `View::InitView` at a test page via `recap::WkeLoadHTML` (the grid from the smoke test) — a 2×2 magenta/cyan/yellow/green grid in-game proves modern CSS + the pixel path end-to-end.
  - **FAIL (blank/black where the UI was):** MiniBlink loaded but didn't paint into `mpSurface` — check the sink stride/format and that `Tick` isn't required to pump (try calling a wke pump if the release needs one).
  - **FAIL (crash on load):** likely `miniblink.dll` missing/ò wrong bitness, or in-proc conflict — check it's next to the exe and 32-bit; capture the crash address (image base 0x400000).
  - **Fallback intact:** building WITHOUT `RECAP_MINIBLINK`, or with `miniblink.dll` absent, must still run on stock WebCore (graceful path).

- [ ] **Step 4: Record the milestone (ReCap repo)**
  Update `memory/` with the Phase-1 result (works / the paint-format finding / any pump requirement), then commit the spec/plan/memory notes:
  ```bash
  git add docs/superpowers memory MEMORY.md
  git commit -m "docs(eawebkit): modern-engine Phase 1 (MiniBlink pixel path) result"
  ```

---

## Self-review notes
- Spec coverage: engine choice (MiniBlink) + dynamic load (T1/T2), swap-the-painter-keep-mpSurface (T3), compile flag + vcproj (T4), pixel path + BGRA→ARGB + stride (T2 sink, T3 Step 2), graceful WebCore fallback (T3 Step 3), in-game gate (T5). JS bridge/input/modern-content are explicitly out of Phase 1 (spec phases 2-4).
- Placeholder scan: every code step shows full code; wke signatures are the real ones from `wke.h`. Open verification points (paint stride, whether a wke pump is needed) are framed as runtime checks with concrete fallbacks, not TODOs.
- Type consistency: `recap::WkeView`, `WkePaintSink(user,bgra,srcStride,x,y,w,h)`, `WkeAvailable/Create/LoadHTML/LoadURL/Resize/Destroy` used identically across T1/T2/T3. Paint thunk passes `width*4` as `srcStride`, matching the sink's `srcStride` param and the View sink's row math.
- Risk carried from spec: whether offscreen MiniBlink needs an explicit pump (no `wke` pump in the API list — assumed message-loop driven); T2/T5 gates surface this early.
