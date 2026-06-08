# UTFwin Native UI Hello-World PoC — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** From the ReCap DLL, on F9, call the game's own `UTFWin::UILayout` API to load and show a hand-authored SPUI layout (`ReCapDebugWindow.spui`) — proving the native-UI path end to end.

**Architecture:** A small DLL module calls the exe's `MemoryAllocator::Alloc` + `UILayout::Ctor/Load/SetParentWindow` through base-relative `__thiscall`/`__cdecl` function pointers (same technique as `ReCapHooks`), mirroring `cSPUIHostBrowser::Initialize`. F9 is detected in the existing `Hook_PeekMessageW`.

**Tech Stack:** C++ (VS2008/VC9), Win32, Darkspore.exe UTFwin internals (addresses verified in Ghidra). Spec: `docs/superpowers/specs/2026-06-03-utfwin-native-ui-poc-design.md`.

## Project realities (read first)

- `C:\CodingProjects\Personal\eawebkit\source\` is a **non-git tree** with **no unit-test harness**. No git commits, no pytest in this plan.
- **Per-task verification = compile-check** `/W4 /WX`: `cmd //c "C:\CodingProjects\Personal\eawebkit\tests\_chk_w4.bat"` (compiles the ReCap*.cpp shim sources) and `_chk_hooks.bat` (ReCapHooks.cpp).
- **Final validation = full VS2008 build + in-game F9 test** (user). The `.spui` package (`ReCapNativeUI.package`) is already authored and in the game's `Data/` (ideally `Data/Patches/`).
- Addresses are Ghidra absolute (image base 0x00400000); the code uses offsets `addr - 0x00400000` against `GetModuleHandleA(NULL)`.

## File structure

- Create `eawebkit/source/ReCapNativeUI.h` — one public fn `recap::RecapNativeUiTest()`.
- Create `eawebkit/source/ReCapNativeUI.cpp` — the exe fn-ptr typedefs + the test routine.
- Modify `eawebkit/source/ReCapHooks.cpp` — call `RecapNativeUiTest()` from `Hook_PeekMessageW` on F9.
- Modify `eawebkit/tests/_chk_w4.bat` — add `ReCapNativeUI.cpp` to the compile-check.
- Modify `eawebkit/projects/VS2008/EAWebKit/1.21.00.darkspore/EAWebkit.vcproj` — add `ReCapNativeUI.cpp`.

## Verified exe entry points (Darkspore.exe, base 0x00400000)

| Fn | Addr | Offset | Convention (verified in host-browser disasm @0x0042fb40) |
|---|---|---|---|
| `MemoryAllocator::Alloc` | 0x0051ce60 | 0x0011ce60 | `__cdecl void* (size_t, const char* tag, int,int,int,int)` |
| `UILayout::Ctor` | 0x009102d0 | 0x005102d0 | `__thiscall void* (void* self)` |
| `UILayout::Load` | 0x00912670 | 0x00512670 | `__thiscall char (void* self, ResourceKey*, int bool, uint param)` — 3 stack args |
| `UILayout::SetParentWindow` | 0x00912750 | 0x00512750 | `__thiscall char (void* self, void* parent, int bool, uint param)` — 3 stack args |

ResourceKey = `{ uint instance, type, group }`. Our layout: instance `0x4F95272F`, type `0x510A95B`, group `0x40464100`. Default param `0x5B598FA`.

---

### Task 1: ReCapNativeUI module

**Files:**
- Create: `eawebkit/source/ReCapNativeUI.h`
- Create: `eawebkit/source/ReCapNativeUI.cpp`
- Modify: `eawebkit/tests/_chk_w4.bat`

- [ ] **Step 1: Create the header**

`eawebkit/source/ReCapNativeUI.h`, exactly:
```cpp
#ifndef RECAP_NATIVE_UI_H
#define RECAP_NATIVE_UI_H

/* Hello-world PoC: render a native UTFwin SPUI layout (ReCapDebugWindow.spui) by calling the
   game's own UILayout API from the DLL. Triggered by F9 (see ReCapHooks). */

namespace recap {
void RecapNativeUiTest();   /* idempotent; safe to call repeatedly (runs once) */
}

#endif /* RECAP_NATIVE_UI_H */
```

- [ ] **Step 2: Create the implementation**

`eawebkit/source/ReCapNativeUI.cpp`, exactly:
```cpp
#define _CRT_SECURE_NO_WARNINGS
#include <windows.h>
#include "ReCapNativeUI.h"
#include "ReCapLog.h"

namespace recap {

/* Darkspore.exe (image base 0x00400000) entry points, as offsets (addr - 0x00400000). */
#define RECAP_OFF_ALLOC            0x0011ce60u  /* MemoryAllocator::Alloc (__cdecl) */
#define RECAP_OFF_UILAYOUT_CTOR    0x005102d0u  /* UTFWin::UILayout::Ctor */
#define RECAP_OFF_UILAYOUT_LOAD    0x00512670u  /* UTFWin::UILayout::Load */
#define RECAP_OFF_UILAYOUT_SETPAR  0x00512750u  /* UTFWin::UILayout::SetParentWindow */

/* ReCapDebugWindow.spui in group "layouts~". */
#define RECAP_SPUI_INSTANCE   0x4F95272Fu  /* FNV1a("ReCapDebugWindow") */
#define RECAP_SPUI_TYPE       0x0510A95Bu  /* UILayout::kDefaultType (logical) */
#define RECAP_SPUI_GROUP      0x40464100u  /* layouts~ = kDefaultGroup */
#define RECAP_DEFAULT_PARAM   0x05B598FAu  /* UILayout::kDefaultParameter */

struct RecapResourceKey { unsigned int instance, type, group; };

typedef void* (__cdecl    *AllocFn)(unsigned int, const char*, int, int, int, int);
typedef void* (__thiscall *CtorFn)(void*);
typedef char  (__thiscall *LoadFn)(void*, RecapResourceKey*, int, unsigned int);
typedef char  (__thiscall *SetParentFn)(void*, void*, int, unsigned int);

static void* s_layout = 0;   /* intentionally leaked (PoC lifetime) */
static int   s_done   = 0;

void RecapNativeUiTest()
{
    if (s_done) { MbLog("NativeUiTest: already ran"); return; }
    s_done = 1;

    unsigned char* base = (unsigned char*)GetModuleHandleA(NULL);
    AllocFn     Alloc     = (AllocFn)    (base + RECAP_OFF_ALLOC);
    CtorFn      Ctor      = (CtorFn)     (base + RECAP_OFF_UILAYOUT_CTOR);
    LoadFn      Load      = (LoadFn)     (base + RECAP_OFF_UILAYOUT_LOAD);
    SetParentFn SetParent = (SetParentFn)(base + RECAP_OFF_UILAYOUT_SETPAR);

    MbLog("NativeUiTest: F9 -> Alloc UILayout (0x18, SP_UI)");
    void* layout = Alloc(0x18u, "SP_UI", 0, 0, 0, 0);
    if (!layout) { MbLog("NativeUiTest: Alloc FAILED"); return; }
    Ctor(layout);
    s_layout = layout;

    RecapResourceKey key;
    key.instance = RECAP_SPUI_INSTANCE;
    key.type     = RECAP_SPUI_TYPE;
    key.group    = RECAP_SPUI_GROUP;
    MbLogf("NativeUiTest: Load key={0x%08X,0x%08X,0x%08X}", key.instance, key.type, key.group);

    char ok = Load(layout, &key, 1, RECAP_DEFAULT_PARAM);
    MbLogf("NativeUiTest: Load ok=%d", (int)ok);
    if (!ok) { MbLog("NativeUiTest: Load FAILED (check .spui present in Data/Patches + ids match)"); return; }

    char shown = SetParent(layout, 0, 1, RECAP_DEFAULT_PARAM);  /* NULL parent -> default main window */
    MbLogf("NativeUiTest: SetParentWindow ret=%d (window should now be visible)", (int)shown);
}

} // namespace recap
```

- [ ] **Step 3: Add ReCapNativeUI.cpp to the compile-check**

In `eawebkit/tests/_chk_w4.bat`, after the `ReCapMiniBlinkBackend.cpp` line, add:
```bat
cl /nologo /c /W4 /WX /MT /EHsc source\ReCapNativeUI.cpp /Fotests\nu.obj
```

- [ ] **Step 4: Compile-check**

Run: `cmd //c "C:\CodingProjects\Personal\eawebkit\tests\_chk_w4.bat"`
Expected: prints `ReCapNativeUI.cpp` among the sources, no `error`/`warning` lines (clean /W4 /WX). If `__thiscall`-on-pointer warns, it won't — VC9 supports it.

---

### Task 2: F9 trigger in ReCapHooks

**Files:**
- Modify: `eawebkit/source/ReCapHooks.cpp`

- [ ] **Step 1: Include the module**

In `ReCapHooks.cpp`, find:
```cpp
#include "ReCapHooks.h"
#include "ReCapLog.h"
```
Change to:
```cpp
#include "ReCapHooks.h"
#include "ReCapLog.h"
#include "ReCapNativeUI.h"
```

- [ ] **Step 2: Detect F9 in Hook_PeekMessageW**

Find the existing `Hook_PeekMessageW` (it handles WM_QUIT re-post). Its current body:
```cpp
static BOOL WINAPI Hook_PeekMessageW(LPMSG msg, HWND h, UINT a, UINT b, UINT r)
{
    BOOL ok = Real_PeekMessageW(msg, h, a, b, r);
    if (ok && msg && msg->message == RECAP_WM_QUIT)
    {
        void* caller = _ReturnAddress();
        if (RecapAddrInModule(caller, "mb132_x32.dll"))
        {
            PostThreadMessageA(GetCurrentThreadId(), RECAP_WM_QUIT, msg->wParam, msg->lParam);
            recap::MbLogf("WMQ: mb ate WM_QUIT (caller=%p) -> re-posted for the game loop", caller);
            msg->message = 0;   /* WM_NULL */
            return FALSE;        /* hide from mb so its pump yields back to the game's loop */
        }
    }
    return ok;
}
```
Insert the F9 check right after `BOOL ok = Real_PeekMessageW(...)`:
```cpp
static BOOL WINAPI Hook_PeekMessageW(LPMSG msg, HWND h, UINT a, UINT b, UINT r)
{
    BOOL ok = Real_PeekMessageW(msg, h, a, b, r);
    if (ok && msg && msg->message == 0x0100 /*WM_KEYDOWN*/ && msg->wParam == 0x78 /*VK_F9*/)
        recap::RecapNativeUiTest();
    if (ok && msg && msg->message == RECAP_WM_QUIT)
    {
        void* caller = _ReturnAddress();
        if (RecapAddrInModule(caller, "mb132_x32.dll"))
        {
            PostThreadMessageA(GetCurrentThreadId(), RECAP_WM_QUIT, msg->wParam, msg->lParam);
            recap::MbLogf("WMQ: mb ate WM_QUIT (caller=%p) -> re-posted for the game loop", caller);
            msg->message = 0;   /* WM_NULL */
            return FALSE;        /* hide from mb so its pump yields back to the game's loop */
        }
    }
    return ok;
}
```

- [ ] **Step 3: Compile-check hooks**

Run: `cmd //c "C:\CodingProjects\Personal\eawebkit\tests\_chk_hooks.bat"`
Expected: prints `ReCapHooks.cpp`, no error/warning. (Note: `_chk_hooks.bat` compiles ReCapHooks.cpp alone; the `ReCapNativeUI.h` include resolves; the `recap::RecapNativeUiTest` symbol is unresolved at link but this is a compile-only check, so it passes.)

---

### Task 3: Wire the build + validate

**Files:**
- Modify: `eawebkit/projects/VS2008/EAWebKit/1.21.00.darkspore/EAWebkit.vcproj`

- [ ] **Step 1: Add ReCapNativeUI.cpp to the project**

In `EAWebkit.vcproj`, find the `ReCapMiniBlinkBackend.cpp` `<File>` block (added in the IWebEngine work). Add a sibling right after it:
```xml
      <File RelativePath="..\..\..\..\source\ReCapNativeUI.cpp">
        <FileConfiguration Name="pc-vc-dev-debug|Win32">
          <Tool Name="VCCLCompilerTool" ObjectFile="pc-vc-dev-debug\build\EAWebkit\vcproj\source\ReCapNativeUI.cpp.obj" />
        </FileConfiguration>
        <FileConfiguration Name="pc-vc-dev-opt|Win32">
          <Tool Name="VCCLCompilerTool" ObjectFile="pc-vc-dev-opt\build\EAWebkit\vcproj\source\ReCapNativeUI.cpp.obj" />
        </FileConfiguration>
      </File>
```
(If the `ReCapMiniBlinkBackend.cpp` block isn't found, copy any existing `ReCap*.cpp` `<File>` block and rename to `ReCapNativeUI.cpp`.)

- [ ] **Step 2: Compile-check both**

Run: `cmd //c "C:\CodingProjects\Personal\eawebkit\tests\_chk_w4.bat"` then `cmd //c "C:\CodingProjects\Personal\eawebkit\tests\_chk_hooks.bat"`
Expected: all sources clean.

- [ ] **Step 3: Full build (USER)**

VS2008 → `EAWebKit.sln` → `pc-vc-dev-opt|Win32`. Deploy `EAWebkit.dll` next to `Darkspore.exe`. Ensure `ReCapNativeUI.package` is in `Data/Patches/`.

- [ ] **Step 4: In-game test (USER)**

Launch Darkspore, get in-game (so the window manager is live), press **F9**. Expected in `recapmb.log`:
```
NativeUiTest: F9 -> Alloc UILayout (0x18, SP_UI)
NativeUiTest: Load key={0x4F95272F,0x0510A95B,0x40464100}
NativeUiTest: Load ok=1
NativeUiTest: SetParentWindow ret=1 (window should now be visible)
```
and the `ReCapDebugWindow` layout appears on screen. 
- If `Load ok=0`: the `.spui` wasn't found — verify the package is in `Data/Patches/`, the file is in the `layouts~` folder, and the instance id matches `FNV("ReCapDebugWindow")`=0x4F95272F.
- If `Load ok=1` but nothing visible: the `.spui` has no visible widget, or the layout needs an explicit visible flag — add a colored panel/label in SPMFX, or try a `SetVisible` call (next iteration).
- If it crashes in `RecapNativeUiTest`: a calling-convention/timing issue — capture the exception address; the `bool` arg to `Load` (we pass 1) is the first knob to flip to 0.

---

## Self-review notes

- **Spec coverage:** module + typedefs (Task 1) ↔ spec "Components/2"; F9 trigger (Task 2) ↔ spec "Hotkey"; build wiring + validation (Task 3) ↔ spec "Wiring/Validation". The `.spui` authoring is the user's (done). FindWindowByID is spec-optional, omitted from the minimal PoC.
- **Convention consistency:** typedefs match the verified disasm (Load/SetParent = this + 3 stack args; Alloc = __cdecl 6 args; Ctor = this only). Offsets = Ghidra addr − 0x400000.
- **No git commits / no unit tests:** intentional (non-git tree, no harness).
- **Known knobs if it fails:** `bool` arg to Load (1↔0); NULL parent → explicit main window; SetVisible; .spui visible content.
