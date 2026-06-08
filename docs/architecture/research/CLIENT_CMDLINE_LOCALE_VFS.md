# Darkspore Client — Command-line Flags, VFS & Locale System

Ghidra-mapped 2026-06-03 from `Darkspore.exe` (retail 5.3.0.127, image base `0x00400000`).
Source for adding launch flags, understanding the data/resource layout, and i18n (e.g. pt-BR).

## 1. Virtual File System / data directories

`Core::MountDataDirectories` **@0x005141f0** (standalone) and its twin
`Core::MountDataDirectories_Struct` **@0x007ed2a0** (operates on an app struct) set up the
resource mount points. (Two near-identical copies = the prebuilt-vs-source build drift noted in
CLAUDE.md.)

Flow:
1. Resolve the **Data root**: `-dataDir` option, else `"Data"` beside the exe (fatal "The game
   cannot find data files…" if absent).
2. Mount, relative to the root: **`Locale/`, `Config/`, `UserData/`, `Patches/`**.
3. Register each as a named mount (FNV-hashed, registrar `FUN_007bbd70`).

- **`Patches/` is an overlay** — files there override the base packages. The clean,
  non-destructive channel for mods / extra content / i18n.
- **Dev vs ship layout** (flag-driven): `-devDirs` → UserData = `../UserData/` (relative to exe);
  `-shipDirs` → UserData from registry `HKLM\Software\Electronic Arts\Darkspore\PlayerDir` +
  `AppDir`. Struct form stores devMode at `+0x140`, the `mce` (editor) flag at `+0x1dc`.

The client's own font set (from `SP_App` init `FUN_00515aa0`): `EAPirulen-RGDS`,
`EAHelveticaNeueLTCom-BdCnDS`, `EAHelveticaNeueConBol-Mod_Cyr_DS`. DDF list:
`DDFLists/SporeLabs_DDFList.txt`. Bootstrap config endpoint (`FUN_004642e0`):
`http://config.darkspore.com/bootstrap/api?version=1` → `api.config.getConfigs`.

## 2. Command-line flags

Parsed by `Core::GetCommandLineOption` **@0x00aed790**: flag prefix **`-` or `/`**
(`DAT_0104540c` = `2d 2f`), `key:value` separator **`:`**. Returns the arg index if present
(and copies the value after `:` into the out-param), else `0xFFFFFFFF`.

Discovered flags (callers of GetCommandLineOption; non-exhaustive — ~7 caller fns not yet swept):

| Flag | Effect | Where |
|---|---|---|
| `-dataDir:<path>` | override the Data directory | MountDataDirectories |
| `-userDataDir:<path>` | override UserData | MountDataDirectories |
| `-devDirs` | dev dir layout (UserData = ../UserData/) | MountDataDirectories |
| `-shipDirs` | release dir layout (UserData from registry) | MountDataDirectories |
| `-writeableData` | mark data writable | 0x00515aa0 |
| `-mce` | editor mode flag (+0x1dc) | MountDataDirectories |
| `-noServer` | (server disabled) | MountDataDirectories region |
| `-noPlugins` | skip plugins | 0x007ee4e0 |
| `-safe` | safe mode (skip plugins, safe render path, +config alerts) | 0x007ee4e0 / 0x007eb970 / 0x0085df00 |
| `-showConfigAlerts` | surface config alerts | 0x0085df00 |
| `-noSound` | disable audio init | 0x00c5dd80 |
| `-nofocus` | don't require window focus (audio) | 0x00c5dd80 |
| `-vSync` / `-noVSync` | force vsync on/off | 0x007eb970 |
| `-flock` | (flocking/debug tuning, alt value) | 0x007eb970 |
| `-noDevEffects` | disable dev effects | 0x007ef720 |
| `-dumpShaders` | dump shaders | 0x007ef720 |
| `-dumpShadersForPIX` | dump shaders for PIX | 0x007ef720 |
| `-dumpFragmentShaders` | dump fragment shaders | 0x007ef720 |
| `-dumpDirectShaders` | dump direct shaders | 0x007ef720 |
| `-noScripts` | disable all scripts | 0x007ef720 |
| `-noMaterialScripts` | disable material scripts | 0x007ef720 |
| `-noEffectScripts` | disable effect scripts | 0x007ef720 |
| `-testPatch` | bootstrap: patch active=false | 0x004642e0 |
| `-patch` / `-upgrading` | patcher entry (self-relaunch path) | 0x005375f0 |

Dev convenience: `-dataDir:<path> -devDirs` runs against a modified Data tree + local UserData
without touching the install/registry. `-safe` is the troubleshooting path.

## 3. Locale system

Chain: Blaze `AccountLocale` (uint, e.g. `"enUS"`=0x656E5553) → `Client.getLocaleId()` → `"en-us"`
→ load `Locale/<loc>/Text.package` → `retrieveLocaleString("0xTABLE!0xKEY")` → string. The
registry `HKLM\Software\Electronic Arts\Darkspore\Locale` holds the selected locale.

**First-run auto-detect** — `FUN_00514cc0`: if registry `InstallID` unset, probes which
`<loc>/Movies.package` exists, in order **en-us → de-de → fr-fr → pl-pl → ru-ru** (default en-us),
and writes the result to the registry `Locale`. Only these 5 shipped with content; only **en-us**
has Audio+Movies, the rest are **Text-only** (audio/movies fall back to en-us) — proof the engine
tolerates text-only locales.

**Hardcoded locale metadata table** (~0x010427c0+): the FULL Windows language set, each entry
`<id>^<winAbbr>^<iso3>^<codepage>^<iso2>` (+ a separate `<id>^<LCID>` table, e.g. `pl-pl^0415`).
Includes far more than the 5 shipped: `es-es, es-mx, sv-se, sv-fi, th-th, tl-ph, tr-tr, vi-vn,
ro-ro, sk-sk, no-no, **pt-pt**, **pt-br**` … e.g. `pt-br^   ^ptb^bra^1252` (Portuguese-Brazil,
codepage 1252).

### Adding pt-BR (viable!)

`pt-br` is **already in the engine's locale table** — no exe patch needed for the engine to
recognize it. Steps:
1. Create `Data/Locale/pt-br/Text.package` (DBPF) with translated strings — same `0xKEY value`
   text-table format we already parse ([`webview-game-package-serving`] / `LocaleStore`). Codepage
   for pt-br is 1252; our pipeline is UTF-8 so accents are fine.
2. Make the client select it: set registry `…\Darkspore\Locale = "pt-br"`, or have Blaze send
   `AccountLocale = "ptBR"` (ReCap currently hardcodes `0x656E5553`="enUS" in
   AuthenticationComponent / GameManagerComponent).
3. ReCap's `LocaleStore` (webview): read the active locale's folder instead of hardcoded `en-us`.

**Open / to verify in Ghidra:** the function that reads the registry `Locale` and builds
`Locale/<loc>/` — confirm it validates against the metadata table (which contains pt-br) vs the
5-with-content auto-detect list. Pragmatic fallback if it rejects pt-br: drop translated content
into an unused official locale (e.g. `ru-ru`) and send that locale.

## Mapped symbols (this pass)

| Address | Name | Role |
|---|---|---|
| 0x005141f0 | `Core::MountDataDirectories` | VFS mount (standalone) |
| 0x007ed2a0 | `Core::MountDataDirectories_Struct` | VFS mount (struct form) |
| 0x00aed790 | `Core::GetCommandLineOption` | flag/option parser |
| 0x00514cc0 | (locale first-run auto-detect) | probes Movies.package, writes registry Locale |
