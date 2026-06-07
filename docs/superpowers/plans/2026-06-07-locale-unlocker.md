# Locale Unlocker (ReCap.WebKit) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every `Data/Locale/<code>/` folder a selectable client locale (e.g. `pt-br`) by registering its code at runtime via the client's own registrar, from the in-process ReCap.WebKit DLL.

**Architecture:** A new TU `Source/Hooks/LocaleUnlock.cpp` detours the client locale resolver `FUN_007fac90` (exe offset `0x003FAC90`); after the original runs (which builds the locale trees + registers the 5 native locales), it enumerates `Data/Locale/*` and registers each non-native code by calling the client's `FUN_00ae9420` registrar (exe offset `0x006E9420`, `__cdecl(langDef, regionDef)`). The lookup key is `langDef.field0 + "-" + regionDef.field0`, so a `(L"pt^…", L"br^…")` pair registers `pt-br`. Wired into the existing Detours framework in `ReCapHooks.cpp`.

**Tech Stack:** C++ (x86 DLL), Microsoft Detours, CMake + Ninja (x86 MSVC). Target client: Darkspore.exe retail 5.3.0.127, image base 0x00400000 (same build the existing cert/DPI hooks target).

**Spec:** `docs/superpowers/specs/2026-06-07-locale-unlocker-design.md`. Ghidra facts: memory `locale-system-ghidra`.

**Hard rules:**
- NEVER add `Co-Authored-By`/"Generated with" to commits.
- NEVER `git add -A`/`-a`; stage only listed paths.
- The ReCap.WebKit repo is a **separate git repo** at `C:\CodingProjects\Personal\ReCap.WebKit` — run git there, commit there.
- Match the existing `ReCapHooks.cpp` idiom: addresses = `GetModuleHandleA(NULL) + offset`; Detour pairs inside the `RecapHooksInstall` transaction.
- Accented characters in wide string literals use `\uXXXX` escapes (avoid source-encoding pitfalls under MSVC).

---

## File Map

| Path (under `C:\CodingProjects\Personal\ReCap.WebKit\`) | Action | Responsibility |
|---|---|---|
| `Source/Hooks/LocaleUnlock.h` | create | `RecapLocaleUnlockPrepare/Attach/Detach` declarations |
| `Source/Hooks/LocaleUnlock.cpp` | create | resolver/registrar fn-ptrs, hook, folder enumeration + def building |
| `Source/Hooks/ReCapHooks.cpp` | modify | call Prepare (pre-transaction) + Attach (in install txn) + Detach (in uninstall txn) |
| `CMakeLists.txt` | modify | add `Source/Hooks/LocaleUnlock.cpp` to the DLL sources |

No unit-test harness exists for the native DLL (adding one is out of scope, spec §8). Verification = clean build + in-game gate + a debug log of registered codes.

---

### Task 1: `LocaleUnlock.{h,cpp}`

**Files:**
- Create: `C:\CodingProjects\Personal\ReCap.WebKit\Source\Hooks\LocaleUnlock.h`
- Create: `C:\CodingProjects\Personal\ReCap.WebKit\Source\Hooks\LocaleUnlock.cpp`

- [ ] **Step 1: Create `LocaleUnlock.h`**

```cpp
#pragma once

/* Locale unlocker: registers every Data/Locale/<code>/ folder as a selectable client locale
   by calling the client's own registrar after its locale resolver runs. See
   docs/superpowers/specs/2026-06-07-locale-unlocker-design.md. */

/* Resolve the client function pointers from the exe base. Call BEFORE the Detour transaction. */
void RecapLocaleUnlockPrepare(unsigned char* exeBase);

/* Attach/detach the resolver detour. Call INSIDE the same Detour transaction the other hooks use. */
void RecapLocaleUnlockAttach(void);
void RecapLocaleUnlockDetach(void);
```

- [ ] **Step 2: Create `LocaleUnlock.cpp`**

```cpp
#include "LocaleUnlock.h"

#include <windows.h>
#include <detours.h>
#include <string>
#include <cwctype>

/* exe offsets from image base 0x00400000 (retail 5.3.0.127) */
#define RECAP_LOCALERESOLVER_OFFSET 0x003FAC90u  /* FUN_007fac90: locale resolver, __fastcall(ECX) */
#define RECAP_REGISTERLOCALE_OFFSET 0x006E9420u  /* FUN_00ae9420: registrar, __cdecl(langDef, regionDef) */

typedef char (__fastcall *LocaleResolverFn)(void* ecx, void* edx);
typedef void (__cdecl   *RegisterLocaleFn)(const wchar_t* langDef, const wchar_t* regionDef);

static LocaleResolverFn Real_LocaleResolver = nullptr;
static RegisterLocaleFn s_registerLocale    = nullptr;
static bool             s_localesRegistered = false;

/* en/us template: everything AFTER the field[0] ("en"/"us"), copied from FUN_007fac90's
   registration. A cloned code reuses these tails with its own 2-char field[0]. */
static const wchar_t* EN_LANG_TAIL =
    L"^English^English"
    L"^Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday"
    L"^Mon.,Tue.,Wed.,Thur.,Fri.,Sat.,Sun."
    L"^January,February,March,April,May,June,July,August,September,October,November,December"
    L"^Jan.,Feb.,Mar.,Apr.,May,June,July,Aug.,Sept.,Oct.,Nov.,Dec.^0^%f %l^";
static const wchar_t* US_REGION_TAIL =
    L"^United States^United States^R^E^AM,PM^%h:%<02M:%<02S %<A^%#02m/%#02d/%02y"
    L"^%>+m %<#d, %<y^$^,^.^%$%W%?p%?02F^%$-%W%?p%?02F^USA^840";

/* Curated pt-br def (Ghidra-extracted; \uXXXX for accents). */
static const wchar_t* PT_BR_LANG =
    L"pt^Portuguese^Português"
    L"^Segunda-feira,Terça-feira,Quarta-feira,Quinta-feira,Sexta-feira,Sábado,Domingo"
    L"^Seg,Ter,Qua,Qui,Sex,Sáb,Dom"
    L"^Janeiro,Fevereiro,Março,Abril,Maio,Junho,Julho,Agosto,Setembro,Outubro,Novembro,Dezembro"
    L"^Jan,Fev,Mar,Abr,Mai,Jun,Jul,Ago,Set,Out,Nov,Dez^1^%f %l^";
static const wchar_t* PT_BR_REGION =
    L"br^Brazil^Brasil^R^M^^%H:%<02M:%<02S^%#02d/%#02m/%02y^%#d de %>m de %<y"
    L"^R$^.^,^%$%W%?p%?02F^-%$%W%?p%?02F^BRA^076";

static const wchar_t* const NATIVE_CODES[] = { L"en-us", L"de-de", L"fr-fr", L"pl-pl", L"ru-ru" };

static bool IsNativeCode(const std::wstring& code)
{
    for (auto* n : NATIVE_CODES) if (code == n) return true;
    return false;
}

static bool ParseCode(const std::wstring& code, std::wstring& lang, std::wstring& region)
{
    size_t dash = code.find(L'-');
    if (dash == std::wstring::npos) return false;
    lang = code.substr(0, dash);
    region = code.substr(dash + 1);
    return lang.size() >= 2 && region.size() >= 2;
}

/* Walk up from the exe path until a "Data\Locale" directory exists; "" if none. */
static std::wstring FindLocaleDir()
{
    wchar_t exe[MAX_PATH];
    if (!GetModuleFileNameW(NULL, exe, MAX_PATH)) return L"";
    std::wstring dir = exe;
    for (;;)
    {
        size_t slash = dir.find_last_of(L"\\/");
        if (slash == std::wstring::npos) return L"";
        dir.resize(slash);
        std::wstring cand = dir + L"\\Data\\Locale";
        DWORD attr = GetFileAttributesW(cand.c_str());
        if (attr != INVALID_FILE_ATTRIBUTES && (attr & FILE_ATTRIBUTE_DIRECTORY)) return cand;
    }
}

static void RegisterOne(const std::wstring& code, const std::wstring& lang, const std::wstring& region)
{
    if (code == L"pt-br") { s_registerLocale(PT_BR_LANG, PT_BR_REGION); return; }
    std::wstring langDef = lang + EN_LANG_TAIL;
    std::wstring regionDef = region + US_REGION_TAIL;
    s_registerLocale(langDef.c_str(), regionDef.c_str());
}

/* Has C++ objects; NO __try here (kept out of the SEH frame in the hook). */
static void RegisterLocaleFolders()
{
    if (!s_registerLocale) return;
    std::wstring localeDir = FindLocaleDir();
    if (localeDir.empty()) { OutputDebugStringW(L"[ReCap locale] Data\\Locale not found\n"); return; }

    std::wstring pattern = localeDir + L"\\*";
    WIN32_FIND_DATAW fd;
    HANDLE h = FindFirstFileW(pattern.c_str(), &fd);
    if (h == INVALID_HANDLE_VALUE) return;
    do
    {
        if (!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)) continue;
        std::wstring code = fd.cFileName;
        if (code == L"." || code == L"..") continue;
        for (auto& ch : code) ch = (wchar_t)towlower(ch);
        if (IsNativeCode(code)) continue;
        std::wstring lang, region;
        if (!ParseCode(code, lang, region)) continue;
        RegisterOne(code, lang, region);
        std::wstring msg = L"[ReCap locale] registered " + code + L"\n";
        OutputDebugStringW(msg.c_str());
    } while (FindNextFileW(h, &fd));
    FindClose(h);
}

/* No C++ objects with destructors in this frame -> __try/__except is legal here. */
static char __fastcall Hook_LocaleResolver(void* ecx, void* edx)
{
    char r = Real_LocaleResolver(ecx, edx);
    if (!s_localesRegistered)
    {
        s_localesRegistered = true;
        __try { RegisterLocaleFolders(); }
        __except (EXCEPTION_EXECUTE_HANDLER) { OutputDebugStringW(L"[ReCap locale] register pass faulted\n"); }
    }
    return r;
}

void RecapLocaleUnlockPrepare(unsigned char* exeBase)
{
    Real_LocaleResolver = (LocaleResolverFn)(exeBase + RECAP_LOCALERESOLVER_OFFSET);
    s_registerLocale    = (RegisterLocaleFn)(exeBase + RECAP_REGISTERLOCALE_OFFSET);
}

void RecapLocaleUnlockAttach(void)
{
    if (Real_LocaleResolver) DetourAttach(&(PVOID&)Real_LocaleResolver, (PVOID)Hook_LocaleResolver);
}

void RecapLocaleUnlockDetach(void)
{
    if (Real_LocaleResolver) DetourDetach(&(PVOID&)Real_LocaleResolver, (PVOID)Hook_LocaleResolver);
}
```

- [ ] **Step 3: Commit (in the ReCap.WebKit repo)**

```powershell
cd C:\CodingProjects\Personal\ReCap.WebKit
git add Source/Hooks/LocaleUnlock.h Source/Hooks/LocaleUnlock.cpp
git commit -m "feat(hooks): locale unlocker - register Data/Locale folders via client registrar"
```

---

### Task 2: Wire into `ReCapHooks.cpp` + CMake

**Files:**
- Modify: `C:\CodingProjects\Personal\ReCap.WebKit\Source\Hooks\ReCapHooks.cpp`
- Modify: `C:\CodingProjects\Personal\ReCap.WebKit\CMakeLists.txt`

- [ ] **Step 1: Include the header.** Near the top of `ReCapHooks.cpp` (with the other includes), add:

```cpp
#include "LocaleUnlock.h"
```

- [ ] **Step 2: Prepare + attach in `RecapHooksInstall`.** In `ReCapHooks.cpp`, find the line in `RecapHooksInstall`:

```cpp
    unsigned char* base = (unsigned char*)GetModuleHandleA(NULL);
```
Immediately after it add:
```cpp
    RecapLocaleUnlockPrepare(base);
```
Then find the existing WM_QUIT attach line inside the same transaction:
```cpp
    DetourAttach(&(PVOID&)Real_PeekMessageW, (PVOID)Hook_PeekMessageW);
```
Immediately after it add:
```cpp
    RecapLocaleUnlockAttach();
```

- [ ] **Step 3: Detach in `RecapHooksUninstall`.** In `ReCapHooks.cpp`, locate `RecapHooksUninstall` and its `DetourTransactionBegin()` / `DetourUpdateThread(...)` block. Inside that transaction (next to the other `DetourDetach` calls), add:

```cpp
    RecapLocaleUnlockDetach();
```
(If `RecapHooksUninstall` has no `DetourDetach` for `Real_PeekMessageW` to anchor next to, add `RecapLocaleUnlockDetach();` immediately after `DetourUpdateThread(GetCurrentThread());` and before `DetourTransactionCommit();`.)

- [ ] **Step 4: Add the source to CMake.** In `CMakeLists.txt`, find the DLL source list line:

```cmake
    Source/EAWebkitExports.cpp            # DllMain + host-process hook lifetime
```
Find the line that lists `Source/Hooks/ReCapHooks.cpp` in that same source list and add directly after it:
```cmake
    Source/Hooks/LocaleUnlock.cpp
```
(If `ReCapHooks.cpp` is not explicitly listed because the list uses a glob, confirm the glob covers `Source/Hooks/*.cpp`; if it does, no CMake change is needed — note that in the commit message. If it lists files explicitly, add the line above.)

- [ ] **Step 5: Commit**

```powershell
cd C:\CodingProjects\Personal\ReCap.WebKit
git add Source/Hooks/ReCapHooks.cpp CMakeLists.txt
git commit -m "feat(hooks): wire locale unlocker into RecapHooksInstall + CMake"
```

---

### Task 3: Build + in-game gate

**Files:** none (build + manual verification).

- [ ] **Step 1: Build the DLL** (x86 MSVC developer prompt; the repo's documented build):

```powershell
cd C:\CodingProjects\Personal\ReCap.WebKit
cmake -S . -B build -G Ninja -DCMAKE_BUILD_TYPE=RelWithDebInfo
cmake --build build
```
Expected: build succeeds, output `build\EAWebkit.dll`. Fix any compile error before continuing. (Common pitfall already handled: `__try`/`__except` lives only in `Hook_LocaleResolver`, which has no C++ objects with destructors — C2712 avoided.)

- [ ] **Step 2: Deploy the DLL** to the game's `EAWebkit.dll` location (where the client loads it — the same path used for the existing ReCap.WebKit deployment; copy `build\EAWebkit.dll` over it). Ensure `Data\Locale\pt-br\` exists (confirmed present this session).

- [ ] **Step 3: Select pt-br + launch.** Set the registry once (elevated), then launch the game the normal way:

```powershell
Set-ItemProperty -Path 'HKLM:\SOFTWARE\WOW6432Node\Electronic Arts\Darkspore' -Name 'Locale' -Value 'pt-br'
```

- [ ] **Step 4: Confirm the gate.** PASS criteria:
  - A DebugView/OutputDebugString line `[ReCap locale] registered pt-br` appears at startup (the pass ran and registered it).
  - The game UI renders in **pt-br** (the `Locale/pt-br/` content), not English.
  - The C# server, started without `--locale`, logs `Locale: pt-br` (auto-detected from the same registry key) so the webview matches.
  - Switch back: set the registry value to `en-us` → game returns to English (proves the unlocker didn't break the native path).
  Record the result. If text shows but date/time formatting looks off, that's the known caveat (spec §10) — note it for a follow-up token-tweak; text correctness is the gate.

- [ ] **Step 5: Update memory** (`memory/locale-system-ghidra.md`): mark the locale unlocker implemented (commits, in-game result), noting pt-br now selectable natively.

---

## Self-Review

- **Spec coverage:** §3 mechanism → Task 1 (hook + registrar call). §4.1 hook → Task 1 `Hook_LocaleResolver` + Task 2 attach. §4.2 registrar pass → Task 1 `RegisterLocaleFolders`. §4.3 def building (pt-br curated + en clone) → Task 1 `RegisterOne`/templates. §4.4 schema → embedded in the template constants. §5 selection → Task 3 Step 3 (registry, unchanged). §6 error handling → `__try`/`__except` + empty-dir no-op + module-base resolution. §7 file map → Tasks 1-2. §8 testing → Task 3 (build + in-game + OutputDebugString log). All covered.
- **Placeholder scan:** none — full code in every code step; the one conditional (CMake glob vs explicit list) gives both branches explicitly.
- **Type consistency:** `RecapLocaleUnlockPrepare(unsigned char*)`, `RecapLocaleUnlockAttach(void)`, `RecapLocaleUnlockDetach(void)` declared in `.h` (Task 1 Step 1), defined in `.cpp` (Task 1 Step 2), called in `ReCapHooks.cpp` (Task 2). `LocaleResolverFn`/`RegisterLocaleFn` typedefs used consistently. Offsets `0x003FAC90`/`0x006E9420` match the spec.
- **SEH/C++ split:** `__try`/`__except` only in `Hook_LocaleResolver` (no destructor-bearing locals); the C++ string work is in `RegisterLocaleFolders` (no SEH) — avoids C2712.
```
