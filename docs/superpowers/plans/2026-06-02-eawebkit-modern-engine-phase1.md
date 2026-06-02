# EAWebKit modern engine — Phase 1 (pixel path) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make MiniBlink (modern Blink, Chromium 132) render a modern-CSS page into Darkspore's in-game web surface, behind EAWebKit's real `View`/`ISurface` ABI — proving the engine coexists in-process and the pixel path works.

**Architecture:** Inside the EAWebKit.dll we build, the real `View` keeps its ARGB `mpSurface` but its renderer is swapped WebCore→MiniBlink (behind a `RECAP_MINIBLINK` compile flag). A small `recapmb` wrapper uses the **`mb` API** (release `miniblink132`, DLL `mb132_x32.dll`); `mb.h` self-loads the DLL (`mbSetMbMainDllPath` + `mbInit`), so there is no hand-written symbol resolver. An offscreen `mbWebView`'s `mbOnPaintBitUpdated` callback copies BGRA frames into `mpSurface`.

**Tech Stack:** C++ (VC9 / EAWebKit), MiniBlink `mb` API (`mb132_x32.dll` + `mb.h`, 32-bit, from the `miniblink132_251212` release), Win32. Standalone smoke test via VS2008 `cl` (the engine DLL is x86; build the test x86).

**Spec:** `docs/superpowers/specs/2026-06-02-eawebkit-modern-engine-design.md`

**Scope:** Phase 1 only (foundation + pixel path). Phases 2 (JS bridge), 3 (input), 4 (modern content) get their own plans after this phase's in-game gate.

**Downloaded SDK (confirmed):** `C:\Users\dell04\Downloads\miniblink132_251212\`
- `mb132_x32.dll` — the runtime (verified **i386/x86**, exports `mbCreateWebView`/`mbInit`/`mbOnPaintBitUpdated`/`mbLoadURL`).
- `demo_src\mb.h` — the header (self-contained; ships an inline loader). NOTE it defaults the DLL name to `mb108_x32.dll`; we override to `mb132_x32.dll` via `mbSetMbMainDllPath`.

**Real `mb` API used (from `mb.h`, all `__stdcall`; `mbWebView` is a handle):**
```c
void       mbInit(const mbSettings* settings);          // mb.h inline: LoadLibrary + fill fn ptrs
void       mbSetMbMainDllPath(const WCHAR* dllPath);    // mb.h inline
mbWebView  mbCreateWebView();
void       mbResize(mbWebView, int w, int h);
typedef void(__stdcall* mbPaintBitUpdatedCallback)(mbWebView, void* param, const void* buffer, const mbRect* r, int width, int height); // mbRect={x,y,w,h}; buffer=BGRA top-down, stride=w*4
void       mbOnPaintBitUpdated(mbWebView, mbPaintBitUpdatedCallback, void* param);
void       mbLoadURL(mbWebView, const utf8* url);
void       mbLoadHtmlWithBaseUrl(mbWebView, const utf8* html, const utf8* baseUrl);
void       mbDestroyWebView(mbWebView);
```

---

## File Structure

| File | Responsibility |
|---|---|
| `eawebkit/source/mb.h` (NEW, copied) | The MiniBlink `mb` header (copied from the release `demo_src\mb.h`), so the project includes it locally. |
| `eawebkit/source/recapmb.h` (NEW) | C++ API the View calls: `Available/Create/LoadHTML/LoadURL/Resize/Destroy`. No mb types leak out. |
| `eawebkit/source/recapmb.cpp` (NEW) | `#include "mb.h"`; `mbInit` once (path → `mb132_x32.dll`); owns the offscreen `mbWebView`; routes `mbOnPaintBitUpdated` → a paint sink. |
| `eawebkit/tests/recapmb_smoke.cpp` (NEW, dev-only) | Standalone exe: init mb, render test HTML offscreen, assert the paint callback delivered a non-blank frame. Proves the engine works on this box before touching EAWebKit. |
| `eawebkit/source/EAWebKitView.cpp` (EDIT) | Under `#ifdef RECAP_MINIBLINK`: `InitView`/`SetURI`/`SetSize`/`Tick` drive `recapmb` + copy frames into `mpSurface`; `GetSurface` unchanged. |
| `include/EAWebKit/EAWebKitView.h` (EDIT) | add `recap::MbView* mpRecapMb;` member under the flag. |
| `EAWebkit.vcproj` (EDIT) | compile `recapmb.cpp`; define `RECAP_MINIBLINK`. |

---

## Task 0: Stage the SDK (USER-RUN)

**Files:** copy only

- [ ] **Step 1:** Copy the header into the project:
  `copy "C:\Users\dell04\Downloads\miniblink132_251212\demo_src\mb.h" "C:\CodingProjects\Personal\eawebkit\source\mb.h"`
- [ ] **Step 2:** Keep `mb132_x32.dll` reachable for the smoke test and (later) deploy. For the test, it will be copied next to the test exe. For the game, it ships next to `Darkspore.exe`.
- [ ] **Step 3 (sanity):** `mb.h` is self-contained (the release demo includes only `mb.h` + `windows.h`). If the VS2008 compile later reports a missing include pulled in by `mb.h`, copy that header in too and report it.

---

## Task 1: `recapmb.h` — the wrapper API

**Files:**
- Create: `C:\CodingProjects\Personal\eawebkit\source\recapmb.h`

- [ ] **Step 1: Write the header**

```cpp
#ifndef RECAP_MB_H
#define RECAP_MB_H

/* Modern-engine (MiniBlink "mb" API) backend for the EAWebKit View.
   Hides all mb/DLL details. A paint sink receives 32-bit BGRA frames; the View copies
   them into its ISurface. */

namespace recap {

typedef void (*MbPaintSink)(void* user, const void* bgra, int srcStride,
                            int x, int y, int w, int h);

struct MbView; // opaque

/* True once the MiniBlink DLL is loaded + mbInit succeeded. */
bool MbAvailable();

/* Offscreen view sized w x h; frames delivered to sink(user, ...). */
MbView* MbCreate(int w, int h, MbPaintSink sink, void* user);
void    MbLoadHTML(MbView*, const char* utf8Html);
void    MbLoadURL(MbView*, const char* utf8Url);
void    MbResize(MbView*, int w, int h);
void    MbDestroy(MbView*);

} // namespace recap

#endif /* RECAP_MB_H */
```

- [ ] **Step 2: Commit** — none (eawebkit tree is not a git repo).

---

## Task 2: `recapmb.cpp` — mb backend + smoke test (TDD)

**Files:**
- Create: `C:\CodingProjects\Personal\eawebkit\source\recapmb.cpp`
- Test: `C:\CodingProjects\Personal\eawebkit\tests\recapmb_smoke.cpp`

- [ ] **Step 1: Write the smoke test FIRST**

`tests/recapmb_smoke.cpp`:
```cpp
#include <windows.h>
#include <stdio.h>
#include "../source/recapmb.h"

static volatile int g_painted = 0, g_nonblank = 0;

static void onPaint(void* /*user*/, const void* bgra, int stride, int /*x*/, int /*y*/, int w, int h)
{
    g_painted = 1;
    const unsigned char* p = (const unsigned char*)bgra;
    for (int row = 0; row < h && !g_nonblank; ++row)
        for (int col = 0; col < w * 4; ++col)
            if (p[(size_t)row * stride + col] != 0) { g_nonblank = 1; break; }
}

int main(void)
{
    if (!recap::MbAvailable()) { printf("FAIL: MiniBlink mb DLL not available\n"); return 2; }
    recap::MbView* v = recap::MbCreate(640, 480, onPaint, 0);
    if (!v) { printf("FAIL: MbCreate returned null\n"); return 3; }
    recap::MbLoadHTML(v,
        "<html><body style='margin:0'>"
        "<div style='display:grid;grid-template-columns:1fr 1fr;width:640px;height:480px'>"
        "<div style='background:#f0f'></div><div style='background:#0ff'></div>"
        "<div style='background:#ff0'></div><div style='background:#0f0'></div>"
        "</div></body></html>");

    DWORD start = GetTickCount(); MSG msg;
    while (GetTickCount() - start < 4000 && !g_nonblank)
    {
        while (PeekMessage(&msg, 0, 0, 0, PM_REMOVE)) { TranslateMessage(&msg); DispatchMessage(&msg); }
        Sleep(10);
    }
    recap::MbDestroy(v);
    if (g_painted && g_nonblank) { printf("ALL PASS (painted + non-blank)\n"); return 0; }
    printf("FAIL: painted=%d nonblank=%d\n", g_painted, g_nonblank);
    return 1;
}
```

- [ ] **Step 2: Run the test — verify it fails (no implementation yet)**

In a **VS2008 Command Prompt** (x86), from `C:\CodingProjects\Personal\eawebkit`:
```
cl /nologo /MT /EHsc tests\recapmb_smoke.cpp source\recapmb.cpp /Fetests\recapmb_smoke.exe
```
Expected: link error (recapmb.cpp not written) or, once it builds, `FAIL: ... not available`. Not `ALL PASS`.

- [ ] **Step 3: Write `recapmb.cpp`**

```cpp
#include <windows.h>
#include "mb.h"          // copied from the release; self-loads mb132_x32.dll
#include "recapmb.h"

namespace recap {

static int s_state = 0;  // 0=unprobed, 1=ok, -1=unavailable

static bool ensureInit()
{
    if (s_state) return s_state == 1;
    s_state = -1;
    mbSetMbMainDllPath(L"mb132_x32.dll");   // override mb.h's mb108 default
    mbSettings settings;
    memset(&settings, 0, sizeof(settings));
    mbInit(&settings);                       // mb.h inline: LoadLibrary + fill fn ptrs
    if (!mbCreateWebView) return false;      // fn ptr stays null if the DLL didn't load
    s_state = 1;
    return true;
}

struct MbView {
    mbWebView   wv;
    MbPaintSink sink;
    void*       user;
};

bool MbAvailable() { return ensureInit(); }

static void __stdcall paintThunk(mbWebView, void* param, const void* buffer,
                                 const mbRect* r, int width, int height)
{
    MbView* self = (MbView*)param;
    if (self && self->sink && buffer && r)
        self->sink(self->user, buffer, width * 4, r->x, r->y, r->w, r->h);
    (void)height;
}

MbView* MbCreate(int w, int h, MbPaintSink sink, void* user)
{
    if (!ensureInit()) return 0;
    MbView* self = new MbView();
    self->sink = sink; self->user = user;
    self->wv = mbCreateWebView();
    mbResize(self->wv, w, h);
    mbOnPaintBitUpdated(self->wv, paintThunk, self);
    return self;
}

void MbLoadHTML(MbView* v, const char* html) { if (v) mbLoadHtmlWithBaseUrl(v->wv, html, "about:blank"); }
void MbLoadURL(MbView* v, const char* url)   { if (v) mbLoadURL(v->wv, url); }
void MbResize(MbView* v, int w, int h)       { if (v) mbResize(v->wv, w, h); }
void MbDestroy(MbView* v)                    { if (v) { mbDestroyWebView(v->wv); delete v; } }

} // namespace recap
```

- [ ] **Step 4: Run the test — verify it passes**

```
copy "C:\Users\dell04\Downloads\miniblink132_251212\mb132_x32.dll" tests\
cl /nologo /MT /EHsc tests\recapmb_smoke.cpp source\recapmb.cpp /Fetests\recapmb_smoke.exe
tests\recapmb_smoke.exe
```
Expected: `ALL PASS (painted + non-blank)`, exit 0.
- `not available` → check `mb132_x32.dll` is next to the exe + 32-bit.
- paints but `nonblank=0` → log `width`/`r`; the buffer/stride may differ — informative, engine still works.
- If `mb.h` won't compile under `cl` (macro/clang guards), report the exact error — `mb.h` has `#if`/`#else` branches keyed on `ENABLE_MB`/`__clang__`; we want the non-`ENABLE_MB`, non-clang inline-loader branch (default for a normal `cl` include).

- [ ] **Step 5: Commit** — none (eawebkit tree).

---

## Task 3: Route the View renderer to MiniBlink (behind `RECAP_MINIBLINK`)

**Files:**
- Modify: `C:\CodingProjects\Personal\eawebkit\source\EAWebKitView.cpp`
- Modify: `C:\CodingProjects\Personal\eawebkit\include\EAWebKit\EAWebKitView.h`

Context: `View` has `mpSurface` (ARGB `EA::Raster::ISurface`) + `mpWebView` (WebCore). Keep `mpSurface`; under the flag drive MiniBlink and skip WebCore.

- [ ] **Step 1: Add the member (header)**

In `include/EAWebKit/EAWebKitView.h`, in `View`'s private members near `mpSurface`, add:
```cpp
#ifdef RECAP_MINIBLINK
    void* mpRecapMb;   /* recap::MbView* (opaque here to avoid leaking mb into the public header) */
#endif
```

- [ ] **Step 2: Add include + ctor init (EAWebKitView.cpp)**

At the top includes of `EAWebKitView.cpp`:
```cpp
#ifdef RECAP_MINIBLINK
#include "recapmb.h"
#endif
```
In the `View` ctor initializer list (where `mpSurface(0)` is, ~line 359):
```cpp
#ifdef RECAP_MINIBLINK
    , mpRecapMb(0)
#endif
```

- [ ] **Step 3: Add the paint sink (copies BGRA frame into `mpSurface`)**

Near the top of `EAWebKitView.cpp` after includes:
```cpp
#ifdef RECAP_MINIBLINK
namespace {
void RecapMbPaintSink(void* user, const void* bgra, int srcStride,
                      int x, int y, int w, int h)
{
    EA::WebKit::View* view = (EA::WebKit::View*)user;
    EA::Raster::ISurface* s = view ? view->GetSurface() : 0;
    if (!s || !bgra) return;
    unsigned char* dst = (unsigned char*)s->GetData();
    int dstStride = s->GetStride();
    if (!dst) return;
    /* EA kPixelFormatTypeARGB == 0xAARRGGBB == BGRA byte order on LE; mb buffer is BGRA. */
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

- [ ] **Step 4: `InitView` — create the MiniBlink view instead of WebCore**

In `View::InitView` (~line 414), AFTER `mpSurface` is created (~454-466) and BEFORE the WebCore `mpWebView` bring-up, add:
```cpp
#ifdef RECAP_MINIBLINK
    if (mpSurface && recap::MbAvailable())
    {
        int w = mpSurface->GetWidth(), h = mpSurface->GetHeight();
        mpRecapMb = recap::MbCreate(w, h, RecapMbPaintSink, this);
        if (mpRecapMb)
            return (mpSurface != NULL);   /* engine is MiniBlink; skip WebCore */
    }
#endif
```
(If MiniBlink is unavailable, control falls through to stock WebCore — graceful fallback.)

- [ ] **Step 5: `SetURI` / `SetSize` / `Tick`**

`View::SetURI` (~600), top of body:
```cpp
#ifdef RECAP_MINIBLINK
    if (mpRecapMb) { recap::MbLoadURL((recap::MbView*)mpRecapMb, pURI); return true; }
#endif
```
`View::SetSize` (~567), after `mpSurface` is resized (~577-582):
```cpp
#ifdef RECAP_MINIBLINK
    if (mpRecapMb) { recap::MbResize((recap::MbView*)mpRecapMb, w, h); return true; }
#endif
```
`View::Tick` (~941), top of body:
```cpp
#ifdef RECAP_MINIBLINK
    if (mpRecapMb) return true;  /* MiniBlink paints async via the sink on the msg loop */
#endif
```

- [ ] **Step 6: Destroy in teardown**

In the View shutdown that frees `mpSurface` (~542-547), before freeing the surface:
```cpp
#ifdef RECAP_MINIBLINK
    if (mpRecapMb) { recap::MbDestroy((recap::MbView*)mpRecapMb); mpRecapMb = 0; }
#endif
```

- [ ] **Step 7: Verify** — deferred to Task 5 build. Visually confirm each `#ifdef RECAP_MINIBLINK` block is balanced; member/ctor-init/include present; casts `(recap::MbView*)mpRecapMb` consistent.

- [ ] **Step 8: Commit** — none (eawebkit tree).

---

## Task 4: Wire the EAWebKit project (`EAWebkit.vcproj`)

**Files:**
- Modify: `C:\CodingProjects\Personal\eawebkit\projects\VS2008\EAWebKit\1.21.00.darkspore\EAWebkit.vcproj`

- [ ] **Step 1: Add `recapmb.cpp` to the file list**

Find the `<File RelativePath="..\..\..\..\source\recaphooks.cpp">` block (from the redirect work) and insert a sibling immediately before it:
```xml
      <File RelativePath="..\..\..\..\source\recapmb.cpp">
        <FileConfiguration Name="pc-vc-dev-debug|Win32">
          <Tool Name="VCCLCompilerTool" ObjectFile="pc-vc-dev-debug\build\EAWebkit\vcproj\source\recapmb.cpp.obj" />
        </FileConfiguration>
        <FileConfiguration Name="pc-vc-dev-opt|Win32">
          <Tool Name="VCCLCompilerTool" ObjectFile="pc-vc-dev-opt\build\EAWebkit\vcproj\source\recapmb.cpp.obj" />
        </FileConfiguration>
      </File>
```

- [ ] **Step 2: Define `RECAP_MINIBLINK`**

Read the `pc-vc-dev-opt|Win32` configuration's `VCCLCompilerTool` line and append `;RECAP_MINIBLINK` to its `PreprocessorDefinitions` attribute (append a token — do not replace the existing list). `mb.h`/`recapmb.h` are included via the source dir (already on the project's include path, alongside the other `source/*.cpp`); no extra include dir needed since `mb.h` is copied into `source/`.

- [ ] **Step 3: Verify XML well-formedness**

```
C:\Strawberry\c\bin\perl.exe -MXML::Simple -e "XMLin('C:/CodingProjects/Personal/eawebkit/projects/VS2008/EAWebKit/1.21.00.darkspore/EAWebkit.vcproj'); print qq{OK\n}"
```
Expected: `OK` (or visually confirm balance if `XML::Simple` absent).

- [ ] **Step 4: Commit** — none (eawebkit tree).

---

## Task 5: Build, deploy, in-game gate (USER-RUN)

- [ ] **Step 1: Build** `EAWebKit.sln` `pc-vc-dev-opt|Win32` in VS2008. Expect `0 failed`; `recapmb.cpp` + `EAWebKitView.cpp` (with `RECAP_MINIBLINK`) compile.
  - `recap::MbView` undefined in `EAWebKitView.cpp` → confirm `#include "recapmb.h"` under the flag.
  - `mb.h` compile errors → see Task 2 Step 4 note (the inline-loader branch).

- [ ] **Step 2: Deploy**
  1. Back up the working `EAWebkit.dll` next to `Darkspore.exe`.
  2. Copy the new `Distribution\pc\9.0.21022\dev-opt\bin\EAWebkit.dll` over it.
  3. Copy `mb132_x32.dll` next to `Darkspore.exe`.
  4. Keep `recap.cfg` in place (redirect still applies).

- [ ] **Step 3: In-game gate**
  Launch Darkspore.
  - For a pure pixel-path proof, temporarily make `InitView` call `recap::MbLoadHTML(mpRecapMb, <the grid HTML from the smoke test>)` right after create. **PASS:** a 2×2 magenta/cyan/yellow/green grid appears where the web UI is → modern CSS + pixel path proven end-to-end.
  - **FAIL (blank/black):** MiniBlink loaded but didn't paint into `mpSurface` — check the sink stride/format; if mb needs pumping, try calling `mbWake` from `View::Tick`.
  - **FAIL (crash on load):** `mb132_x32.dll` missing/wrong-bitness or in-proc conflict — confirm it's next to the exe + x86; capture the crash address (base 0x400000).
  - **Fallback intact:** a build WITHOUT `RECAP_MINIBLINK` (or with the DLL absent) must still run on stock WebCore.

- [ ] **Step 4: Record the milestone (ReCap repo)**
  Update `memory/` with the Phase-1 result (works / paint-format finding / whether `mbWake` was needed), then:
  ```bash
  git add docs/superpowers memory MEMORY.md
  git commit -m "docs(eawebkit): modern-engine Phase 1 (MiniBlink mb pixel path) result"
  ```

---

## Self-review notes
- Spec coverage: engine (MiniBlink mb) + self-load (T0/T2), swap-painter-keep-mpSurface (T3), compile flag + vcproj (T4), pixel path + BGRA→ARGB + stride (T2 sink/T3 Step 3), graceful WebCore fallback (T3 Step 4), in-game gate (T5). JS bridge/input/content are out of Phase 1.
- Placeholder scan: every code step is complete; mb signatures are the real ones from `mb.h`. Open verification points (paint stride; whether `mbWake` pumping is needed) are concrete runtime checks with fallbacks, not TODOs.
- Type consistency: `recap::MbView`, `MbPaintSink(user,bgra,srcStride,x,y,w,h)`, `MbAvailable/Create/LoadHTML/LoadURL/Resize/Destroy` identical across T1/T2/T3. Paint thunk passes `width*4` as `srcStride`, matching the sink + the View row math. `mpRecapMb` is `void*` in the header, cast to `recap::MbView*` at every use in T3.
- API correction: this revises the earlier wke-based draft — the downloaded release exposes the `mb` API only (no `wke*` exports), and `mb.h` self-loads the DLL (no manual resolver needed).
