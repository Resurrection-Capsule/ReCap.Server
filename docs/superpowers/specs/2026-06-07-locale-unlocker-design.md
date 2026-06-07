# Locale Unlocker (ReCap.WebKit) Design

> Status: design (approved 2026-06-07). A runtime patch in the in-process ReCap.WebKit DLL that registers every `Data/Locale/<code>/` folder as a selectable client locale, so codes the retail client doesn't ship (e.g. `pt-br`) become usable. Lives in ReCap.WebKit (C++ DLL), separate from the C# server. Ghidra-grounded.

## 1. Problem & root cause (Ghidra-verified)

The retail Darkspore client registers exactly **5 locales** (en-us, de-de, fr-fr, pl-pl, ru-ru) at startup in `FUN_007fac90` @0x007FAC90 by calling its own registrar `FUN_00ae9420(wchar* langDef, wchar* regionDef)` once per locale. The resolver then reads `HKLM\Software\Electronic Arts\Darkspore` value `Locale`, and selects it **only if the code is registered** (`FUN_00ae6e40` checks two global EASTL trees: lang @0x0118f938 keyed by `langDef` field[0], region @0x0118f954 keyed by `regionDef` field[0]); otherwise it silently falls back to en-us. So `Locale/pt-br/` exists on disk but is never selected — `pt-br` isn't registered. Confirmed in-game: setting the registry to a *registered* code (`de-de`) switches the game to German; `pt-br` does nothing. Memory: `locale-system-ghidra`.

## 2. Goal

Make any `Data/Locale/<code>/` folder selectable by registering its code at runtime via the client's own registrar, from the already-injected ReCap.WebKit DLL. The user then selects a locale exactly as today (registry `Locale=<code>` — already works; the C# server auto-detects the same key, see `locale-system-ghidra`). No game-file hijack, no rebuild of the client.

## 3. Mechanism (verified contract)

- **Registrar:** `FUN_00ae9420` @0x00AE9420, `__cdecl(const wchar_t* langDef, const wchar_t* regionDef)`. Inserts `langDef` field[0] into the lang tree and `regionDef` field[0] into the region tree (both global, already-initialized by `FUN_00aea970` which runs inside `FUN_007fac90` before the 5 registrations). Calling it standalone **after** `FUN_007fac90`'s original body is safe.
- **Key formation:** lookup code = `lang.field0 + "-" + region.field0`. So a def pair `(L"pt^…", L"br^…")` registers the code `pt-br`.
- **Selection unchanged:** the resolver's registry/`-locale` read picks the active code; once registered, the lookup succeeds and the client mounts `Locale/<code>/`.
- **Patcher filter passes:** `Patcher::CleanImage` @0x00528420 compares only the 2-char lang prefix — `pt` passes.
- **No other gate.**

## 4. Architecture

A new translation unit `ReCap.WebKit/Source/Hooks/LocaleUnlock.{cpp,h}`, wired into the existing hook framework (`ReCapHooks.cpp`, Detours, installed from `RecapHooksInstall()` in `DllMain`). Kept separate from `ReCapHooks.cpp` so each file has one responsibility.

### 4.1 Hook

Detour `FUN_007fac90` (offset `0x003FAC90` = `0x007FAC90 - 0x00400000`, resolved as `GetModuleHandleA(NULL) + offset`, the same base+offset idiom the cert/DPI hooks use). The hook:

```
char __fastcall Hook_LocaleResolver(void* ecx, void* edx)   // FUN_007fac90 is __fastcall (1 ptr arg in ECX)
{
    char r = Real_LocaleResolver(ecx, edx);   // original: builds trees + registers the 5 natives
    if (!s_localesRegistered) { s_localesRegistered = true; RegisterLocaleFolders(); }
    return r;
}
```

`s_localesRegistered` guards against multiple resolver calls (register once).

### 4.2 Registrar pass — `RegisterLocaleFolders()`

1. **Resolve the Locale dir:** `GetModuleFileNameW(NULL)` → walk parent dirs upward until a `Data\Locale` directory exists (robust to the exe living in `DarksporeBin/...`). If none found → log + return (no-op).
2. **Enumerate** `Data\Locale\*` subdirectories (`FindFirstFileW`).
3. For each subdir name `code` (e.g. `"pt-br"`): lowercase it; **skip the 5 natives** `{en-us, de-de, fr-fr, pl-pl, ru-ru}` (already registered) and any malformed name (not `xx-yy`).
4. Build the def pair (4.3) and call `RegisterLocale(langDef, regionDef)` (the client's `FUN_00ae9420`).
5. **Dedup:** track lang and region field[0] keys already registered this pass; if a code's lang or region key was already added, the EASTL tree insert is idempotent for the key — registering the full pair is still correct (validation needs both keys present). Log each registered code.

### 4.3 Def building

- **Known good defs:** a small table maps a code → a curated def pair. Ships with **pt-br** (the Ghidra-extracted Portuguese def: weekday/month names, `R$`, comma decimal). Used when the folder code matches.
- **Generic clone (any other code `xx-yy`):** clone the en template def strings (baked constants copied from the client's en lang/region defs) and replace only field[0] (`en`→`xx`, `us`→`yy`). Result: text comes from the folder's `Text.package`, number/date formatting inherits en (accepted trade-off per scope decision).
- `RegisterLocale` typedef: `typedef void (__cdecl *FN)(const wchar_t*, const wchar_t*);` resolved as `base + 0x006E9420`.

### 4.4 Def-string schema (for the clone template + pt-br)

langDef: `"<lang2>^<EnglishName>^<NativeName>^<7 weekday names csv>^<7 weekday abbr csv>^<12 month names csv>^<12 month abbr csv>^<integral flag>^<name format>^"`.
regionDef: `"<region2>^<EnglishCountry>^<NativeCountry>^<R>^<E>^<AM,PM>^<time fmt>^<short date fmt>^<long date fmt>^<currency>^<thousands sep>^<decimal sep>^<pos money fmt>^<neg money fmt>^<ISO3>^<numeric code>"`.
(Field meaning per the en/de/fr templates; exact format-token semantics — `%f %l`, `%<02M`, `%>m` — were not fully decompiled, so the pt-br region formatting tokens are modeled on de-de/ru-ru and may need an in-game tweak.)

## 5. Selection & coherence

Active locale is still chosen by the registry value `Locale` (or `-locale:<code>`), unchanged. The C# server already auto-detects the same registry key (`RegistryLocaleSource`) and serves the matching webview strings — so client UI + webview stay coherent on one switch.

## 6. Error handling

- Addresses are for retail 5.3.0.127 (same build the cert/DPI hooks target). Guard: before detouring, sanity-check the target is inside the exe module (reuse `RecapAddrInModule(addr, /*exe*/)` pattern; the exe handle = `GetModuleHandleA(NULL)`). If the resolver offset looks wrong, skip the hook (log) rather than crash.
- `Data\Locale` not found, or zero non-native folders → no-op (log).
- All work is in a `__try`/`__except` (or guarded) block so a failure never crashes the client init.
- Wide/narrow conversions bounded.

## 7. Components / file map

| Path | Responsibility |
|---|---|
| `ReCap.WebKit/Source/Hooks/LocaleUnlock.h` | `RecapLocaleUnlockInstall(detours txn)` + `RecapLocaleUnlockResolveReal(base)` declarations |
| `ReCap.WebKit/Source/Hooks/LocaleUnlock.cpp` | resolver typedef + `Real_LocaleResolver`/`Real_RegisterLocale`, `Hook_LocaleResolver`, `RegisterLocaleFolders`, def table + clone, dir walk/enumerate |
| `ReCap.WebKit/Source/Hooks/ReCapHooks.cpp` | call into LocaleUnlock: resolve Real ptr + `DetourAttach` inside the existing transaction in `RecapHooksInstall`; `DetourDetach` in uninstall |
| `ReCap.WebKit/CMakeLists.txt` (or Source/CMakeLists) | add `LocaleUnlock.cpp` to the DLL sources |

## 8. Testing

- Native DLL has no unit harness (would be new infra — out of scope). The pure logic (code parse, native-skip, clone field[0] swap) is written as small free functions for clarity; verified by:
  - **Build:** CMake/Ninja x86 produces the DLL with 0 errors.
  - **In-game gate:** registry `Locale=pt-br` → game UI renders pt-br (the `Locale/pt-br/` content). A debug log line lists every locale code registered by the pass (so the user can confirm pt-br was added).
- Confirmed precondition (this session): `de-de` (a native) already switches the game → the selection path works; this feature only adds the registration so non-native codes also work.

## 9. Non-goals

- Per-locale correct number/date formatting for cloned codes (inherits en; only pt-br ships a curated def).
- A C# / server-side change (server locale already unified; this is purely the client-side registration gap).
- Adding a unit-test framework to the native DLL.
- Changing how the active locale is chosen (registry/`-locale` already works).

## 10. Open caveat

The region def format tokens (date/time/money) are modeled, not fully decompiled. If pt-br shows wrong date/time formatting in-game, the `PT_BR_REGION` tokens need an in-game adjustment pass — text correctness is unaffected (that comes from `Text.package`).
