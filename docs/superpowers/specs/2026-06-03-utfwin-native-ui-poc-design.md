# UTFwin Native UI — Hello-World PoC — Design

Date: 2026-06-03
Scope: `eawebkit/source/` (the ReCap EAWebKit.dll) + a hand-authored `.spui` (SPMFX) dropped into
`Data/Patches/`.
Status: design approved, pending implementation plan.

## Goal

Prove the native-UI path end to end: from the ReCap DLL, call the game's own `UTFWin::UILayout::Load`
to render a hand-authored SPUI layout on screen. This isolates the unknown technical risk (calling
the exe's `__thiscall` UTFwin API from our DLL + a custom `.spui` loading) with the smallest possible
artifact, before reconstructing any real screen (register etc).

Background + the full UTFwin map: `memory/utfwin-native-ui-spike.md`. Cross-checked 1:1 with the Spore
ModAPI SDK (`C:\CodingProjects\Personal\Spore-ModAPI`, `Spore/UTFWin/UILayout.h`) — same engine, same
struct (`sizeof 0x18`, `mResourceKey@0x08`, `mpLayoutObjects@0x14`), same default ids.

## Approach (chosen: direct calls by address)

The DLL calls the exe's UTFwin functions through base-relative pointers, exactly like `ReCapHooks`
already does for the ProtoSSL functions (`base = GetModuleHandleA(NULL)`, `fn = base + offset`).
Rejected: (B) linking the Spore ModAPI SDK — its addresses are Spore's, and linking it under VS2008
is overkill for a PoC; (C) patching an existing screen — less control, mixes host context.

## Mapped exe functions (Darkspore.exe, image base 0x00400000)

| Symbol | Address | Offset (addr-0x400000) | Convention / signature |
|---|---|---|---|
| `MemoryAllocator::Alloc` | 0x0051ce60 | 0x0011ce60 | `__cdecl void* Alloc(size_t, const char* tag, int,int,int,int)` |
| `UTFWin__UILayout__Ctor` | 0x009102d0 | 0x005102d0 | `__thiscall void Ctor(void* self)` |
| `UTFWin__UILayout__Load` | 0x00912670 | 0x00512670 | `__thiscall bool Load(void* self, ResourceKey* key, bool, uint32_t)` |
| `UTFWin__UILayout__SetParentWindow` | 0x00912750 | 0x00512750 | `__thiscall bool SetParentWindow(void* self, void* parent, bool, uint32_t)` |
| `UTFWin__UILayout__FindWindowByID` | 0x009109e0 | 0x005109e0 | `__thiscall IWindow* FindWindowByID(void* self, uint32_t controlID, bool recursive)` |

(`Free`@0x0051ce40 not needed — PoC leaks the layout on purpose.)

**The real template = `ClientWeb::cSPUIHostBrowser::Initialize` @0x0042fad0** (plate-commented in Ghidra).
It does exactly: `key={0x88F2E922, 0x510A95B, 0x40464100}` → `Alloc(0x18,"SP_UI")` → `Ctor` →
`Load(&key,0,0x5B598FA)` → `SetParentWindow(parent,1,0x5B598FA)` → `FindWindowByID(0x07DF1121,1)` →
attaches the web view to that window. (`0x07DF1121` is a *controlID* inside layout `0x88F2E922`, NOT a
loadable instance — that resolved the "DEVUI id" question.) We mirror this with our own layout.

`ResourceKey` memory layout (confirmed in the host-browser init): three uint32 in order
`{ instance, type, group }`.

Defaults (confirmed in binary + SDK): `kDefaultType = 0x510A95B`, `kDefaultGroup = 0x40464100`
(= the name `layouts~` in SPMFX reg_file), `kDefaultParameter = 0x5B598FA`. The `.spui` PHYSICAL
resource type is `0x250FE9A2` (SPMFX reg_type "spui"); `Load` is still called with the LOGICAL
type `0x510A95B` — the factory resolves the physical `.spui` (0x250FE9A2) internally.

## Components

### 1. The test `.spui` (authored by the user, SPMFX)

A trivial layout: one container window with a visible label/button. The user has already authored
`ReCapDebugWindow.spui`. Key facts (computed):
- instance id = `FNV1a("ReCapDebugWindow")` = **0x4F95272F**
- physical type = **0x250FE9A2** (.spui, automatic from SPMFX reg_type)
- group MUST be **`layouts~`** (= 0x40464100, kDefaultGroup) — i.e. put the file in the `layouts~`
  folder of the SPMFX project, **NOT `layouts_atlas~`** (0x7A3F61F0). This is the likely cause of the
  package not working: it's in the wrong group folder.

Pack the project (SPMFX → generates `ReCap.UITest.package`) and drop it in `Data/Patches/` (overlay
mount, non-destructive — see `memory/client-vfs-and-cmdline-flags.md`).

### 2. `ReCapNativeUI.{h,cpp}` (this DLL, new files)

- Typedef'd function pointers to the four exe functions above, resolved once from
  `GetModuleHandleA(NULL) + offset`.
- `struct RecapResourceKey { uint32_t instance, type, group; };`
- Constants (editable in one place): `RECAP_TEST_SPUI_INSTANCE = 0x4F95272F` (FNV1a "ReCapDebugWindow"),
  `kDefaultType = 0x510A95B`, `kDefaultGroup = 0x40464100`, `kDefaultParameter = 0x5B598FA`.
- `void RecapNativeUiTest()` (mirrors `cSPUIHostBrowser::Initialize`):
  1. `void* layout = Alloc(0x18, "SP_UI", 0,0,0,0);`
  2. `Ctor(layout);`
  3. `RecapResourceKey key = { 0x4F95272F, 0x510A95B, 0x40464100 };`
  4. `bool ok = Load(layout, &key, false, 0x5B598FA);`  // host browser passes bool=0 here
  5. `SetParentWindow(layout, NULL, true, 0x5B598FA);` (NULL parent → default main window, per
     `UTFWin__UILayoutObjects__SetParentWindow`@0x009125f0)
  6. store `layout` in a file-static global (lifetime; intentional leak for the PoC)
  7. `MbLog` every step + the `ok` result. (Optional: if the user gives a widget in the .spui a
     known controlID, call `FindWindowByID` to confirm the tree built.)
- Guard: run the body once (a static `bool s_done`) so repeated F9 doesn't stack layouts (or allow
  re-trigger — decided in plan; default once).

### 3. Hotkey trigger (F9)

Detect `WM_KEYDOWN` with `VK_F9` (0x78) inside the existing `Hook_PeekMessageW` (ReCapHooks), with a
simple edge debounce, and call `RecapNativeUiTest()`. Runs on the game's UI thread with the window
manager already live (the game is running). No new hook infra.

### 4. Wiring

`ReCapNativeUI.cpp` added to `EAWebkit.vcproj` + `tests/_chk_w4.bat`. `ReCapHooks.cpp` calls
`recap::RecapNativeUiOnKey(...)` (or includes the F9 check) from `Hook_PeekMessageW`.

## Data flow

`F9 keydown → Hook_PeekMessageW → RecapNativeUiTest → Alloc+Ctor+Load(key)+SetParentWindow → the
game's UILayout pipeline (LoadLayoutObjects → UILayoutBinary factory → reads recaptest.spui from
Patches/ → builds the window tree, parents it to the main window) → window renders.`

## Validation

Press F9 in-game. Success = the test label/window appears on screen, and `recapmb.log` shows
`Load ok=1` + the step trace. If `Load ok=0` or nothing shows, the log pinpoints the failing step
(alloc / load / parent), and we iterate. This is an iterative test, not a one-shot.

## Risks / unknowns (honest)

- `__thiscall` from the DLL — standard (typedef + the compiler passes `this` in ECX), but watch the
  extra stack args on `Load`/`SetParentWindow` (the decompiler showed 2 args; SDK declares 4 — pass
  all 4, harmless if the callee ignores trailing).
- `SetParentWindow(NULL)` using the main window is inferred from the LayoutObjects impl; if nothing
  shows, fall back to finding an explicit parent IWindow* (next iteration).
- The `.spui` must be valid for Darkspore's UTFwin schema (SPMFX targets Spore — confirm the SPUI
  version Darkspore accepts; the Moron note says Spore SPUIs load with zero alteration).
- Instance-id mismatch between our constant and SPMFX's hash → `Load` finds nothing; confirm the id.
- Window-manager readiness — mitigated by hotkey (game already running).

## Out of scope (future)

- Populating widgets with data / binding events (`FindWindowByID`, `SetVisible`).
- Reconstructing the register screen (the real second step, once this PoC passes).
- Loading the `.spui` from a local loader instead of Patches/.
- Calling-convention/SDK-grade type wrappers (PoC uses raw typedefs).
