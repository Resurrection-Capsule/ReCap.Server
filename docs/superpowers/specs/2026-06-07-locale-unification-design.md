# Locale Unification Design

> Status: design (approved 2026-06-07). One source of truth for the active locale, consumed everywhere, easy to switch/test a pt-br package, mocks removed. Grounded in the Ghidra locale model.

## 1. Purpose

The active locale is currently **hardcoded and inconsistent** across ~6 files: the in-game text package is `pt-br` (`PackageMounts.LocaleText`), but Blaze tells the client `enUS` (`0x656E5553` in Auth/GameManager/Util), and the REST bootstrap locale is commented-out (`pt-BR`). There is no single knob; switching/testing a pt-br package means editing multiple files.

Goal: a **single source of truth** for the active locale, resolved once at startup, consumed by every locale-dependent site, de-hardcoded, with the mocked bits removed — so switching to pt-br (or back) is one parameter and the whole system stays coherent.

## 2. Verified facts (Ghidra, see memory `locale-system-ghidra`)

- **Dir code = `{lang}-{region}` lowercase-hyphen**: `en-us`, `pt-br`, `de-de`, `fr-fr`, `pl-pl`, `ru-ru`. Client mounts `Locale/<code>/`. Our `pt-br` casing is correct.
- **The client never sends its locale to the Blaze server** (LoginRequest/PreAuth have no locale field). ⇒ A Blaze "client override" is impossible; the `LANG`/`LOC` TDF fields stay 0.
- **The client stores its locale in the registry** `HKLM\Software\Electronic Arts\Darkspore\Locale` (32-bit client ⇒ likely under `WOW6432Node` on 64-bit Windows). Server on the same machine can READ it → auto-detect.
- **Blaze AccountLocale uint is cosmetic**: server→client only; the client stores but never uses/validates it. Safe to send a derived value or 0.
- **`retrieveLocaleString` key `0x{tableInstanceId:x8}!{lineId}`** — `LocaleStore`'s format is already correct; no change.
- **No crash on unknown locale**: client silently falls back. So the server's locale choice cannot crash the client.

## 3. Architecture

### 3.1 Single source of truth — `LocaleSettings` (new, `ReCap.Server/Config/LocaleSettings.cs`)

An immutable value holding the resolved locale, exposed process-wide (same static-access pattern as `ServerConfig`):

```
public sealed record LocaleSettings(string Code)
{
    public uint BlazeId { get; }   // derived from Code, see 3.3
    public string TextPackageRelativePath { get; }  // Path.Combine("Locale", Code, "Text.package")
}
```

`Code` is the dir code (`"pt-br"`). One instance is built at startup and set as `LocaleSettings.Current` (static, like `ServerConfig`).

### 3.2 Resolution order (built in `Program.cs` at startup)

`--locale=<code>` CLI (explicit force) → registry `HKLM\...\Darkspore\Locale` (the client's own locale) → `"en-us"` default. This mirrors the client's own chain (registry→CLI→OS→en-us) but server-appropriate: CLI wins for deliberate testing, registry auto-matches the client otherwise.

Registry read is isolated behind a tiny seam for testability:

```
public interface ILocaleSource { string? Read(); }   // returns a dir code or null
```

- `RegistryLocaleSource` (Windows impl): tries `HKLM\Software\WOW6432Node\Electronic Arts\Darkspore\Locale` then `HKLM\Software\Electronic Arts\Darkspore\Locale`; returns the `Locale` value or null. Non-Windows / missing key → null. Uses `Microsoft.Win32.Registry` (Windows-only API; guard with `OperatingSystem.IsWindows()`).
- `LocaleSettings.Resolve(string? cliCode, ILocaleSource registry)` applies the order and **validates** each candidate. **Normalization rule:** trim, lowercase, replace `_` with `-`; accept the candidate iff the result is one of the 6 known codes (`en-us`, `pt-br`, `de-de`, `fr-fr`, `pl-pl`, `ru-ru`), else reject and fall through to the next source, finally `en-us`. The stored `Code` is always the normalized lowercase-hyphen form.

### 3.3 `BlazeId` derivation (de-hardcode `0x656E5553`)

`0x656E5553` = ASCII `"enUS"` big-endian = lang lowercase + region UPPERCASE, packed BE. Derive from `Code`:

```
"pt-br" → 'p','t','B','R' → ('p'<<24)|('t'<<16)|('B'<<8)|'R' = 0x70744252
```

i.e. split on `-`, lang→lower (2 chars), region→upper (2 chars), pack big-endian. (All 6 codes are 2-2.) The client ignores this value; deriving it keeps the wire consistent and removes the hardcode.

### 3.4 Consumers (replace hardcode with `LocaleSettings.Current`)

| Site | Before | After |
|---|---|---|
| `PackageMounts.LocaleText` | `new(Path.Combine("Locale","pt-br","Text.package"))` | built from `LocaleSettings.Current.TextPackageRelativePath` |
| `AuthenticationComponent.cs:175`, `GameManagerComponent.cs:171`, `UtilComponent.cs:104` | `0x656E5553` | `LocaleSettings.Current.BlazeId` |
| `BootstrapRestController.cs` (commented Patches block, incl. `Locale="pt-BR"`) | mock block | **remove the mock** — the whole `Patches` block is disabled (TODO "I don't think so") and the client ignores server-sent locale (Ghidra), so no active REST emit is wired. `ConfigPatchesContract.Locale` stays as schema. |
| `LocaleStore` | reads `PackageMounts.LocaleText` | unchanged (follows PackageMounts) |

`PackageMounts.LocaleText` is currently a `static readonly` field; make it resolve from `LocaleSettings.Current` (a static property getter, or built when `PackageMounts.Initialize` runs). Keep the `WellKnownPackage` shape.

### 3.5 Mock removal

- Delete the commented `Locale = "pt-BR"` block in `BootstrapRestController` (replaced by the real wire-up).
- The `ClientLocale` (`LOC`) / `Locale` (`LANG`) TDF fields in the Blaze structs are inert decode targets the client never populates. Leave the struct fields (harmless TDF schema) but remove any code/comments implying we read a client locale from them. Do not add logic that depends on them.

## 4. Data flow

```
startup: Program → LocaleSettings.Resolve(cli --locale, RegistryLocaleSource) → LocaleSettings.Current
                                                                                      │
        ┌───────────────────────────────┬───────────────────────────┬───────────────┘
        ▼                               ▼                           ▼
PackageMounts.LocaleText        Blaze AccountLocale            REST ConfigPatches.locale
   (Locale/<code>/Text.package)    (= BlazeId)                    (= Code)
        ▼
LocaleStore → webview strings (key 0x{inst}!{line}, already correct)
```

The user sets the **client** locale separately (registry or `-locale:pt-br`); the server auto-matches via the registry, or is forced with `--locale`.

## 5. Error handling

- Registry unavailable / non-Windows / key missing → `Read()` returns null → fall through to default. No throw.
- Unknown/garbage code (CLI or registry) → rejected by validation → next source → `en-us`.
- `Locale/<code>/Text.package` missing on disk → existing `PackageMounts.Get` already logs a warning and returns null; `LocaleStore` already handles null (locale strings unavailable). No new path.

## 6. Testing

- `LocaleSettingsTests` (`ReCap.Tests`):
  - Resolution priority: CLI beats registry beats default; invalid CLI falls to registry; invalid registry falls to default `en-us` (using a fake `ILocaleSource`).
  - `BlazeId` derivation: `"en-us"→0x656E5553`, `"pt-br"→0x70744252` (locks the format).
  - `TextPackageRelativePath`: `"pt-br"→Locale/pt-br/Text.package`.
  - Code normalization/validation: `"PT-BR"`→`"pt-br"`, `"pt_br"`→`"pt-br"` (accepted); `"xx-yy"`/garbage → rejected (falls through).
- Registry read itself is not unit-tested (env-coupled); it sits behind `ILocaleSource` and is exercised only via the resolver with a fake.

## 7. File map

| Path | Action |
|---|---|
| `ReCap.Server/Config/LocaleSettings.cs` | create (record + Resolve + BlazeId/path derivation + known-codes + validation) |
| `ReCap.Server/Config/ILocaleSource.cs` + `RegistryLocaleSource.cs` | create (registry seam) |
| `ReCap.Server/Program.cs` | add `--locale=` parse + resolve + set `LocaleSettings.Current` |
| `ReCap.Server/Adapters/Persistence/PackageMounts.cs` | `LocaleText` from `LocaleSettings.Current` |
| `ReCap.Server/Adapters/Blaze/Component/AuthenticationComponent.cs`, `GameManager/GameManagerComponent.cs`, `UtilComponent.cs` | `0x656E5553` → `LocaleSettings.Current.BlazeId` |
| `ReCap.Server/Adapters/Rest/BootstrapRestController.cs` (+ `Contracts/Bootstrap/ConfigPatchesContract.cs`) | emit `Code`; remove commented mock |
| `ReCap.Tests/.../LocaleSettingsTests.cs` | create |

## 8. Non-goals

- Changing `retrieveLocaleString` / `LocaleStore` key format (already correct).
- Making the server tell the client which locale to use (not possible; client picks its own).
- Translating content or shipping a pt-br package (the user supplies `Locale/pt-br/`).
- Per-request/per-session locale (single process-wide locale is enough for single-player testing).
