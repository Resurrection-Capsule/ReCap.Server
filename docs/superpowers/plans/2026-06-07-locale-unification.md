# Locale Unification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the scattered/hardcoded locale (in-game text package = `pt-br`, Blaze = `0x656E5553`, REST mock) with a single source of truth (`LocaleSettings`) resolved once at startup (`--locale` → registry → `en-us`) and consumed everywhere, so switching/testing a pt-br package is one parameter.

**Architecture:** `LocaleSettings` (record: `Code` dir-string + derived `BlazeId` uint + `TextPackageRelativePath`) is built once in `Program.cs` from `--locale=` (force) → the registry key the client itself uses (`SOFTWARE\WOW6432Node\Electronic Arts\Darkspore` value `Locale`) → default `en-us`, and stored in `LocaleSettings.Current`. `PackageMounts` and the Blaze components read `Current` instead of literals. The registry read sits behind `ILocaleSource` for unit-testing the resolution logic without touching the real registry.

**Tech Stack:** .NET 10 (net10.0), xUnit, `Microsoft.Win32.Registry` (already used by `GameInstallLocator`, no package ref needed).

**Spec:** `docs/superpowers/specs/2026-06-07-locale-unification-design.md`. Ghidra facts: memory `locale-system-ghidra` (client never sends locale → registry is the override source; dir code `{lang}-{region}`; AccountLocale uint cosmetic; `retrieveLocaleString` key already correct).

**Hard rules:**
- NEVER add `Co-Authored-By`/"Generated with" to commits (standing user order).
- NEVER `git add -A`/`-a`; stage only the listed paths (the working tree has unrelated WIP).
- No C# comments except short verified-cite one-liners (CLAUDE.md).
- Logging only via `ReCap.Server.Util.Logging.Log`.

---

## File Map

| Path | Action | Responsibility |
|---|---|---|
| `ReCap.Server/Config/ILocaleSource.cs` | create | one-method seam returning a raw locale code or null |
| `ReCap.Server/Config/RegistryLocaleSource.cs` | create | Windows registry impl (the key the client uses) |
| `ReCap.Server/Config/LocaleSettings.cs` | create | the source of truth: `Code`/`BlazeId`/`TextPackageRelativePath`, `Normalize`, `Resolve`, `Current` |
| `ReCap.Tests/Config/LocaleSettingsTests.cs` | create | resolution priority, BlazeId derivation, path, normalization |
| `ReCap.Server/Program.cs` | modify | parse `--locale=`, resolve, set `Current` |
| `ReCap.Server/Adapters/Persistence/PackageMounts.cs` | modify | `LocaleText` from `LocaleSettings.Current` |
| `ReCap.Server/Adapters/Blaze/Component/AuthenticationComponent.cs` | modify | `0x656E5553` → `BlazeId` |
| `ReCap.Server/Adapters/Blaze/Component/GameManager/GameManagerComponent.cs` | modify | `0x656E5553` → `BlazeId` |
| `ReCap.Server/Adapters/Blaze/Component/UtilComponent.cs` | modify | `0x656E5553` → `BlazeId` |
| `ReCap.Server/Adapters/Rest/BootstrapRestController.cs` | modify | remove commented Patches mock |

---

### Task 1: `LocaleSettings` core + registry seam (TDD)

**Files:**
- Create: `ReCap.Server/Config/ILocaleSource.cs`, `ReCap.Server/Config/RegistryLocaleSource.cs`, `ReCap.Server/Config/LocaleSettings.cs`
- Test: `ReCap.Tests/Config/LocaleSettingsTests.cs`

- [ ] **Step 1: Write the failing tests** — `ReCap.Tests/Config/LocaleSettingsTests.cs`:

```csharp
using ReCap.Server.Config;

namespace ReCap.Tests.Config;

public class LocaleSettingsTests
{
    private sealed class FakeSource(string? value) : ILocaleSource
    {
        public string? Read() => value;
    }

    [Fact]
    public void CliCodeWinsOverRegistry()
    {
        var s = LocaleSettings.Resolve("pt-br", new FakeSource("en-us"));
        Assert.Equal("pt-br", s.Code);
    }

    [Fact]
    public void RegistryUsedWhenNoCli()
    {
        var s = LocaleSettings.Resolve(null, new FakeSource("pt-br"));
        Assert.Equal("pt-br", s.Code);
    }

    [Fact]
    public void FallsBackToEnUsWhenNoneValid()
    {
        var s = LocaleSettings.Resolve("garbage", new FakeSource("also-bad"));
        Assert.Equal("en-us", s.Code);
    }

    [Fact]
    public void InvalidCliFallsToRegistry()
    {
        var s = LocaleSettings.Resolve("xx-yy", new FakeSource("pt-br"));
        Assert.Equal("pt-br", s.Code);
    }

    [Theory]
    [InlineData("PT-BR", "pt-br")]
    [InlineData("pt_br", "pt-br")]
    [InlineData("  en-US ", "en-us")]
    public void NormalizesCasingAndUnderscore(string raw, string expected)
    {
        var s = LocaleSettings.Resolve(raw, new FakeSource(null));
        Assert.Equal(expected, s.Code);
    }

    [Theory]
    [InlineData("en-us", 0x656E5553u)]
    [InlineData("pt-br", 0x70744252u)]
    public void DerivesBlazeIdBigEndianLangLowerRegionUpper(string code, uint expected)
    {
        var s = LocaleSettings.Resolve(code, new FakeSource(null));
        Assert.Equal(expected, s.BlazeId);
    }

    [Fact]
    public void TextPackagePathIsLocaleCodeText()
    {
        var s = LocaleSettings.Resolve("pt-br", new FakeSource(null));
        Assert.Equal(System.IO.Path.Combine("Locale", "pt-br", "Text.package"), s.TextPackageRelativePath);
    }
}
```

- [ ] **Step 2: Run, verify FAIL** (types don't exist)

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~LocaleSettingsTests"`
Expected: compile error / FAIL.

- [ ] **Step 3: Create `ILocaleSource.cs`**

```csharp
namespace ReCap.Server.Config;

public interface ILocaleSource
{
    string? Read();
}
```

- [ ] **Step 4: Create `RegistryLocaleSource.cs`** (mirrors `GameInstallLocator.ProbeRegistry` usage; same key the client writes)

```csharp
namespace ReCap.Server.Config;

public sealed class RegistryLocaleSource : ILocaleSource
{
    public string? Read()
    {
        if (!OperatingSystem.IsWindows()) return null;
        return ReadValue(@"SOFTWARE\WOW6432Node\Electronic Arts\Darkspore")
            ?? ReadValue(@"SOFTWARE\Electronic Arts\Darkspore");
    }

    private static string? ReadValue(string subKey)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(subKey);
            return key?.GetValue("Locale") as string;
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 5: Create `LocaleSettings.cs`**

```csharp
namespace ReCap.Server.Config;

public sealed class LocaleSettings
{
    public static readonly string[] KnownCodes = ["en-us", "pt-br", "de-de", "fr-fr", "pl-pl", "ru-ru"];
    public const string DefaultCode = "en-us";

    public string Code { get; }
    public uint BlazeId { get; }
    public string TextPackageRelativePath { get; }

    private LocaleSettings(string code)
    {
        Code = code;
        BlazeId = DeriveBlazeId(code);
        TextPackageRelativePath = Path.Combine("Locale", code, "Text.package");
    }

    public static LocaleSettings Current { get; private set; } = new(DefaultCode);

    public static void SetCurrent(LocaleSettings settings) => Current = settings;

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var code = raw.Trim().ToLowerInvariant().Replace('_', '-');
        return Array.IndexOf(KnownCodes, code) >= 0 ? code : null;
    }

    public static LocaleSettings Resolve(string? cliCode, ILocaleSource registry)
    {
        var code = Normalize(cliCode) ?? Normalize(registry.Read()) ?? DefaultCode;
        return new LocaleSettings(code);
    }

    // BlazeId mirrors the legacy literal 0x656E5553 = ASCII "enUS" big-endian
    // (lang lowercase + region UPPERCASE). Cosmetic: the client stores but ignores it (Ghidra).
    internal static uint DeriveBlazeId(string code)
    {
        var parts = code.Split('-');
        var lang = parts[0];
        var region = parts[1].ToUpperInvariant();
        return ((uint)lang[0] << 24) | ((uint)lang[1] << 16) | ((uint)region[0] << 8) | region[1];
    }
}
```

- [ ] **Step 6: Run, verify PASS**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~LocaleSettingsTests"`
Expected: all PASS (8 tests).

- [ ] **Step 7: Commit**

```powershell
git add ReCap.Server/Config/ILocaleSource.cs ReCap.Server/Config/RegistryLocaleSource.cs ReCap.Server/Config/LocaleSettings.cs ReCap.Tests/Config/LocaleSettingsTests.cs
git commit -m "feat(config): LocaleSettings single source of truth + registry locale source"
```

---

### Task 2: Wire `LocaleSettings` into startup + consumers, remove mock

**Files:**
- Modify: `ReCap.Server/Program.cs`, `ReCap.Server/Adapters/Persistence/PackageMounts.cs`, `ReCap.Server/Adapters/Blaze/Component/AuthenticationComponent.cs`, `ReCap.Server/Adapters/Blaze/Component/GameManager/GameManagerComponent.cs`, `ReCap.Server/Adapters/Blaze/Component/UtilComponent.cs`, `ReCap.Server/Adapters/Rest/BootstrapRestController.cs`

- [ ] **Step 1: `PackageMounts.LocaleText` → dynamic.** In `ReCap.Server/Adapters/Persistence/PackageMounts.cs`, replace the line:

```csharp
    public static readonly WellKnownPackage LocaleText = new(Path.Combine("Locale", "pt-br", "Text.package"));
```
with:
```csharp
    public static WellKnownPackage LocaleText => new(Config.LocaleSettings.Current.TextPackageRelativePath);
```

(`PackageMounts` is in `ReCap.Server.Adapters.Persistence`; `Config.LocaleSettings` resolves via the root namespace. If the build can't resolve `Config`, add `using ReCap.Server.Config;` at the top.)

- [ ] **Step 2: Blaze sites → `BlazeId`.** Make these three replacements:

`ReCap.Server/Adapters/Blaze/Component/AuthenticationComponent.cs` (line ~175):
```csharp
        userAdded.UserInfo.AccountLocale = 0x656E5553;
```
→
```csharp
        userAdded.UserInfo.AccountLocale = ReCap.Server.Config.LocaleSettings.Current.BlazeId;
```

`ReCap.Server/Adapters/Blaze/Component/GameManager/GameManagerComponent.cs` (line ~171):
```csharp
            AccountLocale = 0x656E5553,
```
→
```csharp
            AccountLocale = ReCap.Server.Config.LocaleSettings.Current.BlazeId,
```

`ReCap.Server/Adapters/Blaze/Component/UtilComponent.cs` (line ~104):
```csharp
        response.Telemetry.Locale = 0x656E5553;
```
→
```csharp
        response.Telemetry.Locale = ReCap.Server.Config.LocaleSettings.Current.BlazeId;
```

- [ ] **Step 3: Remove the REST mock.** In `ReCap.Server/Adapters/Rest/BootstrapRestController.cs`, delete the entire commented `response.Patches = new ConfigPatchesContract{ ... }` block (the `// TODO: Should we use the original Patches?` comment through the closing `// };`) inside the `if (includePatches)` body. Leave the `if (includePatches) { }` empty (it already produces no patches).

- [ ] **Step 4: `Program.cs` — parse `--locale=` + resolve + set Current.**

(a) With the other `const string _*_ARG` declarations (near line 33-38), add:
```csharp
    const string _LOCALE_ARG = "--locale=";
```

(b) Before the argument loop, with the other locals, add:
```csharp
        string? cliLocale = null;
```

(c) In the argument loop (the `if/else if` chain on `arg`), add a branch (next to the `_GAME_PATH_ARG` branch):
```csharp
            else if (arg.StartsWith(_LOCALE_ARG))
            {
                cliLocale = arg[_LOCALE_ARG.Length..];
            }
```

(d) Immediately BEFORE `ServerConfig.Configure(serverOpts);` (line ~144), add:
```csharp
        ReCap.Server.Config.LocaleSettings.SetCurrent(
            ReCap.Server.Config.LocaleSettings.Resolve(cliLocale, new ReCap.Server.Config.RegistryLocaleSource()));
        Log.Server.Info($"Locale: {ReCap.Server.Config.LocaleSettings.Current.Code}");
```

(e) Add `--locale` to the help text array (the `_HELP`/options list near line 291-295, matching the existing `$"{_BEFORE_ARG}..."` style):
```csharp
        $"{_BEFORE_ARG}{_LOCALE_ARG}<code>      {_AFTER_ARG}Active locale dir code (en-us/pt-br/...); default: registry then en-us",
```

- [ ] **Step 5: Build + full suite (no regressions).**

Run: `dotnet build ReCap.Server/ReCap.Server.csproj` → `0 Erro(s)`.
Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj` → `Falha: 0`.
(Ensure no server instance is running — it locks the exe.)

- [ ] **Step 6: Commit**

```powershell
git add ReCap.Server/Program.cs ReCap.Server/Adapters/Persistence/PackageMounts.cs ReCap.Server/Adapters/Blaze/Component/AuthenticationComponent.cs ReCap.Server/Adapters/Blaze/Component/GameManager/GameManagerComponent.cs ReCap.Server/Adapters/Blaze/Component/UtilComponent.cs ReCap.Server/Adapters/Rest/BootstrapRestController.cs
git commit -m "feat(locale): consume LocaleSettings everywhere (PackageMounts/Blaze), --locale flag, remove REST mock"
```

---

### Task 3: Manual verification (optional, user)

- [ ] **Step 1:** `dotnet run --project ReCap.Server -- --locale=pt-br` → startup log shows `Locale: pt-br`; LocaleStore loads from `Locale/pt-br/Text.package`.
- [ ] **Step 2:** Without `--locale` and with the client installed → startup log shows the locale read from the registry (matches the client). Run with `--locale=en-us` to switch.

---

## Self-Review

- **Spec coverage:** §3.1 LocaleSettings → Task 1. §3.2 resolution+registry seam → Task 1 (logic) + Task 2 Step 4 (wiring). §3.3 BlazeId derivation → Task 1 (DeriveBlazeId + test). §3.4 consumers: PackageMounts → Task 2 Step 1; Blaze → Step 2; REST (mock removal per reconciled spec row) → Step 3; LocaleStore unchanged (correct). §3.5 mock removal → Step 3. §6 testing → Task 1 tests. All covered.
- **Placeholders:** none — every code step shows full code; line numbers are "~" approximate with unambiguous anchor text to find the exact spot.
- **Type consistency:** `LocaleSettings.Resolve(string?, ILocaleSource)`, `.Current`, `.SetCurrent`, `.Code`, `.BlazeId`, `.TextPackageRelativePath`, `.Normalize`, `DeriveBlazeId`, `ILocaleSource.Read()`, `RegistryLocaleSource` — defined in Task 1, consumed in Task 2 exactly. `WellKnownPackage` ctor `new(string)` matches existing record. BlazeId test values (`0x656E5553`/`0x70744252`) match the derivation.
- **Registry availability:** `Microsoft.Win32.Registry` already used by `GameInstallLocator` (no csproj change). `OperatingSystem.IsWindows()` guards the call.
```
