# Lua Scripting System P0–P2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute the original compiled Lua 5.1 chunks from ServerData.package inside ReCap.Server — native float lua51 build, sandboxed runtime, package VFS + require, full boot of all script groups with stub-first API, and game-folder auto-detection.

**Architecture:** Vendored Lua 5.1.4 C source (patched: `lua_Number=float`, bytecode `size_t→u32`) built once via MSVC into `recaplua51.dll`; C# talks to it through `[LibraryImport]` (`LuaNative`) wrapped by `LuaRuntime` (sandbox per spec F9). `ScriptVfs` resolves `"Group!Name.ext"` over ServerData.package via a central `PackageMounts` adapter. `ScriptEngine` replays the client's `LuaSystem::Initialize` boot (10 group hashes, F7). Unimplemented n* API = logging stubs (metatable fallback). `GameInstallLocator` resolves the game root (CLI → persisted → registry → probe).

**Tech Stack:** .NET 9, xUnit, MSVC (cl.exe via vswhere), Lua 5.1.4, AssetData.Parser (`DbpfReader`, `WireHash`), Ghidra MCP (one extraction task).

**Spec:** `docs/superpowers/specs/2026-06-05-lua-scripting-system-design.md` (decisions D1–D6, facts F1–F12).

**Hard rules for the executor:**
- NEVER add `Co-Authored-By` or "Generated with" to commits (standing user order).
- No comments in C# code except verified wire/contract cites (CLAUDE.md style).
- Logging only via `ReCap.Server.Util.Logging.Log` facade.
- Every Ghidra-verified value gets a `file:line`/address cite where the spec demands one.

---

## File Map

| Path | Responsibility |
|---|---|
| `native/lua51/lua-5.1.4/src/*` | vendored official source, patches applied in-tree |
| `native/lua51/patches/0001-lua-number-float.patch`, `0002-bytecode-size-u32.patch` | reproducibility record of the in-tree edits |
| `native/lua51/build.ps1` | vswhere → cl.exe → `out/recaplua51.dll` + `out/luac.exe` |
| `native/lua51/README.md` | why float / why u32 / header bytes / rebuild steps |
| `ReCap.Server/Adapters/Scripting/Native/LuaNative.cs` | raw `[LibraryImport]` surface + constants |
| `ReCap.Server/Adapters/Scripting/Native/LuaStateHandle.cs` | SafeHandle owning `lua_State*` |
| `ReCap.Server/Adapters/Scripting/LuaRuntime.cs` | state factory, sandbox (F9), traceback pcall, chunk load |
| `ReCap.Server/Adapters/Scripting/ScriptVfs.cs` | `(group,instance)` index over ServerData, `Group!Name.ext` resolution |
| `ReCap.Server/Adapters/Scripting/Api/LuaApiModule.cs` | `RegisterNamespace` (replica of 0x008f3620) + stub metatable fallback |
| `ReCap.Server/Adapters/Scripting/Api/NUtilModule.cs` | real: SPID, ToGUID, GetAsset |
| `ReCap.Server/Adapters/Scripting/Api/NMathUtilModule.cs` | real: pure math fns |
| `ReCap.Server/Adapters/Persistence/PackageMounts.cs` | central lazy `DbpfReader` table |
| `ReCap.Server/Services/Scripting/ScriptEngine.cs` | chunk cache, client group order, boot |
| `ReCap.Server/Services/GameInstallLocator.cs` | detection chain + persistence |
| `ReCap.Server/Config/ServerConfigOptions.cs` | add `GameRoot`/`DataDir` |
| `ReCap.Server/Program.cs` | `--game-path`, `--lua-smoke`, locator wiring |
| `ReCap.Tests/Scripting/*` | interop, sandbox, vfs, require, boot tests |
| `ReCap.Tests/TestSupport/LuaFixtures.cs` | compiles fixture `.lua` via `native/lua51/out/luac.exe` |
| `docs/architecture/research/LUA_REGISTRAR_TABLES.md` | Ghidra extraction output (Task 2) |

---

### Task 1: Vendor Lua 5.1.4, apply float + u32 patches, build script

**Files:**
- Create: `native/lua51/lua-5.1.4/src/*` (vendored), `native/lua51/build.ps1`, `native/lua51/README.md`, `native/lua51/patches/0001-lua-number-float.patch`, `native/lua51/patches/0002-bytecode-size-u32.patch`
- Modify: `.gitignore` (add `native/lua51/out/`)

- [ ] **Step 1: Vendor the official source**

```powershell
New-Item -ItemType Directory -Force native\lua51 | Out-Null
Invoke-WebRequest https://www.lua.org/ftp/lua-5.1.4.tar.gz -OutFile $env:TEMP\lua-5.1.4.tar.gz
tar -xzf $env:TEMP\lua-5.1.4.tar.gz -C native\lua51
```

Result: `native/lua51/lua-5.1.4/src/*.c|*.h` (+ `doc/`, `etc/` — keep, small). Commit this pristine state FIRST (so patch diffs are visible in history):

```powershell
git add native/lua51 .gitignore
git commit -m "chore(native): vendor pristine lua-5.1.4 source"
```

(`.gitignore` gets `native/lua51/out/` in this commit.)

- [ ] **Step 2: Patch 1 — `lua_Number = float` in `luaconf.h`**

In `native/lua51/lua-5.1.4/src/luaconf.h`, find the `@@ LUA_NUMBER` block (~line 490) and replace:

```c
#define LUA_NUMBER_DOUBLE
#define LUA_NUMBER	double
```
with:
```c
/* ReCap: Darkspore chunks are float (header byte 10 = 4, client 0x00909cb0) */
#define LUA_NUMBER	float
```

Then in the same region replace the dependent macros:

```c
#define LUA_NUMBER_SCAN		"%f"
#define LUA_NUMBER_FMT		"%.7g"
#define lua_str2number(s,p)	((lua_Number)strtod((s), (p)))
```

(`LUAI_UACNUMBER` stays `double`. Removing `LUA_NUMBER_DOUBLE` automatically disables the x87 `lua_number2int` trick — the generic cast path is used, correct on x64.)

- [ ] **Step 3: Patch 2 — bytecode string sizes as u32**

`native/lua51/lua-5.1.4/src/lundump.c` — `LoadString`:

```c
static TString* LoadString(LoadState* S)
{
 lu_int32 size32;  /* ReCap: chunks are 32-bit (header size_t=4); stream sizes fixed at 4 bytes */
 size_t size;
 LoadVar(S,size32);
 size=(size_t)size32;
 if (size==0)
  return NULL;
 else
 {
  char* s=luaZ_openspace(S->L,S->b,size);
  LoadBlock(S,s,size);
  return luaS_newlstr(S->L,s,size-1);
 }
}
```

Same file — `luaU_header`, force the size_t byte to 4:

```c
 *h++=(char)sizeof(int);
 *h++=(char)4;	/* ReCap: stream size_t forced to 4 (32-bit chunk format) */
 *h++=(char)sizeof(Instruction);
```

`native/lua51/lua-5.1.4/src/ldump.c` — `DumpString` writes the same u32 (so our `luac.exe` fixtures match the game format):

```c
static void DumpString(const TString* s, DumpState* D)
{
 if (s==NULL || getstr(s)==NULL)
 {
  lu_int32 size=0;
  DumpVar(size,D);
 }
 else
 {
  lu_int32 size=(lu_int32)(s->tsv.len+1);
  DumpVar(size,D);
  DumpBlock(getstr(s),(size_t)size,D);
 }
}
```

(`lu_int32` is already defined in `llimits.h`.)

- [ ] **Step 4: Generate the patch files**

```powershell
git diff -- native/lua51/lua-5.1.4/src/luaconf.h | Set-Content -Encoding utf8 native\lua51\patches\0001-lua-number-float.patch
git diff -- native/lua51/lua-5.1.4/src/lundump.c native/lua51/lua-5.1.4/src/ldump.c | Set-Content -Encoding utf8 native\lua51\patches\0002-bytecode-size-u32.patch
```

- [ ] **Step 5: Write `native/lua51/build.ps1`**

```powershell
$ErrorActionPreference = 'Stop'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw "MSVC not found. Install VS Build Tools with C++ workload." }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'
$src = Join-Path $PSScriptRoot 'lua-5.1.4\src'
$out = Join-Path $PSScriptRoot 'out'
New-Item -ItemType Directory -Force $out | Out-Null
Push-Location $out
try {
    $core = (Get-ChildItem $src -Filter *.c | Where-Object { $_.Name -notin 'lua.c','luac.c','print.c' } | ForEach-Object FullName) -join ' '
    cmd /c "`"$vcvars`" >nul && cl /nologo /O2 /W3 /MD /D_CRT_SECURE_NO_DEPRECATE /DLUA_BUILD_AS_DLL $core /link /DLL /OUT:recaplua51.dll"
    if ($LASTEXITCODE -ne 0) { throw "DLL build failed" }
    cmd /c "`"$vcvars`" >nul && cl /nologo /O2 /W3 /MD /D_CRT_SECURE_NO_DEPRECATE $core `"$src\luac.c`" `"$src\print.c`" /Fe:luac.exe"
    if ($LASTEXITCODE -ne 0) { throw "luac build failed" }
} finally { Pop-Location }
Write-Host "OK: $out\recaplua51.dll + $out\luac.exe"
```

- [ ] **Step 6: Build and verify the header bytes**

```powershell
powershell -File native\lua51\build.ps1
'return (2^24 + 1) == 2^24' | Set-Content $env:TEMP\probe.lua
& native\lua51\out\luac.exe -s -o $env:TEMP\probe.luac $env:TEMP\probe.lua
Format-Hex $env:TEMP\probe.luac | Select-Object -First 2
```

Expected first 12 bytes: `1B 4C 75 61 51 00 01 04 04 04 04 00` — identical to the 1,042 ServerData chunks (spec F2). If byte 10 ≠ `04` the float patch didn't take; if byte 8 ≠ `04` the u32 patch didn't take. STOP and fix before continuing.

- [ ] **Step 7: Write `native/lua51/README.md`**

Content: what this is (Darkspore-compatible Lua 5.1.4), the two patches and why (cite spec F2/F4 + client address `0x00909cb0`), the expected header bytes, build prereqs (VS Build Tools C++), `build.ps1` usage, note that `out/` is gitignored and `dotnet build` only copies.

- [ ] **Step 8: Commit**

```powershell
git add native/lua51 .gitignore
git commit -m "feat(native): lua51 float build - LUA_NUMBER=float + bytecode u32 patches + build.ps1"
```

---

### Task 2: Ghidra extraction — registrar name tables, group order, ID representation

Read-only Ghidra MCP work (load tools via ToolSearch `select:mcp__ghidra__...` first). Program: Darkspore.exe (base 0x400000).

**Files:**
- Create: `docs/architecture/research/LUA_REGISTRAR_TABLES.md`
- Modify: `docs/architecture/VERIFIED_FACTS.md` (append new facts with addresses)

- [ ] **Step 1: Extract the 10-group order array**

Decompile `LuaSystem::Initialize` @`0x00a0c810`. Locate the hardcoded group-hash array (known members: `0x3681d755, 0xda09176b, 0xfc0ff8f5, 0xd2fcb262, 0x7153bbb1, 0xd79fa88c, 0xc130a42a, 0xb2a79c5c, 0xee84d09a, 0x24f78aa1`). Record the EXACT iteration order and the array's data address. Try FNV name resolution for the 6 unresolved hashes against candidate words (`conditions, objectives, affixes, ai, npcs, agents, events, directives, spawners, tuning, scenarios, levels, threads, objectiveevents, npctypes, animation, animationselection` — FNV-1 multiply-then-XOR, lowercase, basis 0x811C9DC5, prime 0x1000193; same fn as `WireHash.Fnv1a`).

- [ ] **Step 2: Dump every registrar's name table**

For each registrar (known: `nAbility 0x00a435c0`, `nGameObject 0x00a08bc0`, `nThread 0x00a0ab00`, `nThreadData 0x009f9d60`, `nLocomotion 0x00a07bc0`, `nObjectManager 0x00a0bff0`, `nPlayer 0x00a06890`, `nEvent 0x00a0c2e0`, `nAgent 0x00a05a90`, `nMathUtil 0x00a09740`, `nGameDirector 0x00a00600`, `nGameSimulator 0x00a071f0`, `nClient 0x00a01bd0`, plus every other `RegisterLuaNamespace` call site found from `LuaFunctions::RegisterLuaFunctions 0x00a0c600`): each calls `RegisterLuaNamespace(L, "name", LuaModuleReg* array, count)` @`0x008f3620`; the array is 8 bytes/entry (`char* name`, `code* fn`). Read each array from memory, resolve the name strings. Output: complete `namespace → [fn names]` listing.

- [ ] **Step 3: Verify ID representation (CRITICAL for bindings)**

`lua_Number=float` cannot represent uint32 hashes exactly (24-bit mantissa). Decompile the client impls of `nUtil.SPID` and `nUtil.ToGUID` (find them in the nUtil registrar array from Step 2) and determine what they push: `lua_pushlightuserdata`? string? boxed userdata? Do the same for one ID-consuming fn (e.g. `nObjectManager.GetObject`) to see how IDs are read back. Record the exact push/read API used — Task 9's bindings MUST match it.

- [ ] **Step 4: Write the research doc + VERIFIED_FACTS entries**

`LUA_REGISTRAR_TABLES.md`: group order table, full per-namespace fn name tables (with array addresses), ID-representation finding (decompile snippets). Append to `VERIFIED_FACTS.md`: group order fact, ID representation fact (cite addresses).

- [ ] **Step 5: Commit**

```powershell
git add docs/architecture/research/LUA_REGISTRAR_TABLES.md docs/architecture/VERIFIED_FACTS.md
git commit -m "docs(research): Lua registrar name tables + group order + ID representation (Ghidra)"
```

---

### Task 3: LuaNative interop + first chunk execution

**Files:**
- Create: `ReCap.Server/Adapters/Scripting/Native/LuaNative.cs`, `ReCap.Server/Adapters/Scripting/Native/LuaStateHandle.cs`, `ReCap.Tests/TestSupport/LuaFixtures.cs`
- Test: `ReCap.Tests/Scripting/LuaInteropTests.cs`
- Modify: `ReCap.Server/ReCap.Server.csproj` (unsafe + DLL copy + guard), `ReCap.Tests/ReCap.Tests.csproj` (DLL copy)

- [ ] **Step 1: csproj wiring**

`ReCap.Server.csproj` — inside the main `<PropertyGroup>` add `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`. Then add:

```xml
<ItemGroup>
  <None Include="..\native\lua51\out\recaplua51.dll" Link="recaplua51.dll" CopyToOutputDirectory="PreserveNewest" Condition="Exists('..\native\lua51\out\recaplua51.dll')" />
</ItemGroup>
<Target Name="RecapLuaGuard" BeforeTargets="Build">
  <Error Condition="!Exists('..\native\lua51\out\recaplua51.dll')" Text="recaplua51.dll missing - run native/lua51/build.ps1 once (requires VS Build Tools C++)." />
</Target>
```

Same `<None Include>` ItemGroup in `ReCap.Tests.csproj` (path `..\native\lua51\out\recaplua51.dll`).

- [ ] **Step 2: `LuaStateHandle.cs`**

```csharp
using System.Runtime.InteropServices;

namespace ReCap.Server.Adapters.Scripting.Native;

internal sealed class LuaStateHandle : SafeHandle
{
    public LuaStateHandle() : base(IntPtr.Zero, ownsHandle: true) { }
    public override bool IsInvalid => handle == IntPtr.Zero;
    protected override bool ReleaseHandle()
    {
        LuaNative.lua_close(handle);
        return true;
    }
}
```

- [ ] **Step 3: `LuaNative.cs`** — full binding surface. NOTE: `lua_Number` is **`float`** in every signature.

```csharp
using System.Runtime.InteropServices;

namespace ReCap.Server.Adapters.Scripting.Native;

internal static partial class LuaNative
{
    private const string Dll = "recaplua51";

    public const int LUA_REGISTRYINDEX = -10000;
    public const int LUA_ENVIRONINDEX = -10001;
    public const int LUA_GLOBALSINDEX = -10002;
    public const int LUA_MULTRET = -1;

    public const int LUA_TNIL = 0;
    public const int LUA_TBOOLEAN = 1;
    public const int LUA_TLIGHTUSERDATA = 2;
    public const int LUA_TNUMBER = 3;
    public const int LUA_TSTRING = 4;
    public const int LUA_TTABLE = 5;
    public const int LUA_TFUNCTION = 6;

    public const int LUA_OK = 0;
    public const int LUA_YIELD = 1;
    public const int LUA_ERRRUN = 2;
    public const int LUA_ERRSYNTAX = 3;
    public const int LUA_ERRMEM = 4;
    public const int LUA_ERRERR = 5;

    public const int LUA_MASKCOUNT = 8;

    [LibraryImport(Dll)] internal static partial nint luaL_newstate();
    [LibraryImport(Dll)] internal static partial void lua_close(nint L);
    [LibraryImport(Dll)] internal static partial nint lua_newthread(nint L);
    [LibraryImport(Dll)] internal static partial nint lua_atpanic(nint L, nint panicf);

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int luaL_loadbuffer(nint L, ReadOnlySpan<byte> buff, nuint sz, string name);
    [LibraryImport(Dll)] internal static partial int lua_pcall(nint L, int nargs, int nresults, int errfunc);
    [LibraryImport(Dll)] internal static partial void lua_call(nint L, int nargs, int nresults);
    [LibraryImport(Dll)] internal static partial int lua_resume(nint L, int narg);
    [LibraryImport(Dll)] internal static partial int lua_status(nint L);

    [LibraryImport(Dll)] internal static partial int lua_gettop(nint L);
    [LibraryImport(Dll)] internal static partial void lua_settop(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_pushvalue(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_remove(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_insert(nint L, int idx);
    [LibraryImport(Dll)] internal static partial int lua_checkstack(nint L, int sz);
    [LibraryImport(Dll)] internal static partial void lua_xmove(nint from, nint to, int n);

    [LibraryImport(Dll)] internal static partial int lua_type(nint L, int idx);
    [LibraryImport(Dll)] internal static partial float lua_tonumber(nint L, int idx);
    [LibraryImport(Dll)] internal static partial int lua_toboolean(nint L, int idx);
    [LibraryImport(Dll)] internal static partial nint lua_tolstring(nint L, int idx, out nuint len);
    [LibraryImport(Dll)] internal static partial nuint lua_objlen(nint L, int idx);
    [LibraryImport(Dll)] internal static partial nint lua_touserdata(nint L, int idx);

    [LibraryImport(Dll)] internal static partial void lua_pushnil(nint L);
    [LibraryImport(Dll)] internal static partial void lua_pushnumber(nint L, float n);
    [LibraryImport(Dll)] internal static partial void lua_pushinteger(nint L, nint n);
    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void lua_pushstring(nint L, string s);
    [LibraryImport(Dll)] internal static partial void lua_pushlstring(nint L, ReadOnlySpan<byte> s, nuint len);
    [LibraryImport(Dll)] internal static partial void lua_pushboolean(nint L, int b);
    [LibraryImport(Dll)] internal static partial void lua_pushcclosure(nint L, nint fn, int n);
    [LibraryImport(Dll)] internal static partial void lua_pushlightuserdata(nint L, nint p);

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void lua_getfield(nint L, int idx, string k);
    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void lua_setfield(nint L, int idx, string k);
    [LibraryImport(Dll)] internal static partial void lua_gettable(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_settable(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_rawget(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_rawset(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_rawgeti(nint L, int idx, int n);
    [LibraryImport(Dll)] internal static partial void lua_rawseti(nint L, int idx, int n);
    [LibraryImport(Dll)] internal static partial void lua_createtable(nint L, int narr, int nrec);
    [LibraryImport(Dll)] internal static partial int lua_setmetatable(nint L, int idx);
    [LibraryImport(Dll)] internal static partial int lua_getmetatable(nint L, int idx);
    [LibraryImport(Dll)] internal static partial int lua_next(nint L, int idx);

    [LibraryImport(Dll)] internal static partial int lua_error(nint L);
    [LibraryImport(Dll)] internal static partial int lua_gc(nint L, int what, int data);
    [LibraryImport(Dll)] internal static partial int lua_sethook(nint L, nint func, int mask, int count);
    [LibraryImport(Dll)] internal static partial int luaL_ref(nint L, int t);
    [LibraryImport(Dll)] internal static partial void luaL_unref(nint L, int t, int @ref);

    [LibraryImport(Dll)] internal static partial int luaopen_base(nint L);
    [LibraryImport(Dll)] internal static partial int luaopen_table(nint L);
    [LibraryImport(Dll)] internal static partial int luaopen_string(nint L);
    [LibraryImport(Dll)] internal static partial int luaopen_math(nint L);
    [LibraryImport(Dll)] internal static partial int luaopen_debug(nint L);

    internal static string? ToManagedString(nint L, int idx)
    {
        var ptr = lua_tolstring(L, idx, out var len);
        return ptr == 0 ? null : Marshal.PtrToStringUTF8(ptr, (int)len);
    }
}
```

- [ ] **Step 4: `LuaFixtures.cs` test helper**

```csharp
using System.Diagnostics;

namespace ReCap.Tests.TestSupport;

public static class LuaFixtures
{
    public static byte[] Compile(string luaSource)
    {
        var root = FindRepoRoot();
        var luac = Path.Combine(root, "native", "lua51", "out", "luac.exe");
        if (!File.Exists(luac))
            throw new InvalidOperationException($"luac.exe missing at {luac} - run native/lua51/build.ps1");
        var src = Path.Combine(Path.GetTempPath(), $"fix_{Guid.NewGuid():N}.lua");
        var outFile = Path.ChangeExtension(src, ".luac");
        File.WriteAllText(src, luaSource);
        try
        {
            var p = Process.Start(new ProcessStartInfo(luac, $"-s -o \"{outFile}\" \"{src}\"") { RedirectStandardError = true })!;
            p.WaitForExit();
            if (p.ExitCode != 0)
                throw new InvalidOperationException($"luac failed: {p.StandardError.ReadToEnd()}");
            return File.ReadAllBytes(outFile);
        }
        finally
        {
            File.Delete(src);
            if (File.Exists(outFile)) File.Delete(outFile);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "native", "lua51")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
```

- [ ] **Step 5: Write the failing interop test**

`ReCap.Tests/Scripting/LuaInteropTests.cs`:

```csharp
using ReCap.Server.Adapters.Scripting.Native;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class LuaInteropTests
{
    [Fact]
    public void LoadsAndRunsCompiledChunk()
    {
        var chunk = LuaFixtures.Compile("return 21 * 2");
        var L = LuaNative.luaL_newstate();
        try
        {
            Assert.Equal(LuaNative.LUA_OK, LuaNative.luaL_loadbuffer(L, chunk, (nuint)chunk.Length, "fixture"));
            Assert.Equal(LuaNative.LUA_OK, LuaNative.lua_pcall(L, 0, 1, 0));
            Assert.Equal(42f, LuaNative.lua_tonumber(L, -1));
        }
        finally { LuaNative.lua_close(L); }
    }

    [Fact]
    public void ChunkHeaderMatchesDarksporeFormat()
    {
        var chunk = LuaFixtures.Compile("return 0");
        Assert.Equal(new byte[] { 0x1B, 0x4C, 0x75, 0x61, 0x51, 0x00, 0x01, 0x04, 0x04, 0x04, 0x04, 0x00 },
                     chunk.Take(12).ToArray());
    }
}
```

- [ ] **Step 6: Run, expect fail (types don't exist), implement Steps 2–4 files, run again**

```powershell
dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~LuaInteropTests"
```
Expected after implementation: 2 passed. (First run fails at compile — that's the TDD red.)

- [ ] **Step 7: Commit**

```powershell
git add ReCap.Server ReCap.Tests
git commit -m "feat(scripting): LuaNative LibraryImport interop loads Darkspore-format chunks"
```

---

### Task 4: LuaRuntime — float semantics lock + protected calls with traceback

**Files:**
- Create: `ReCap.Server/Adapters/Scripting/LuaRuntime.cs`
- Test: `ReCap.Tests/Scripting/LuaRuntimeTests.cs`
- Modify: `ReCap.Server/Util/Logging/Log.cs` (add `Lua` category — mirror the existing per-category property pattern, e.g. how `RakNet` is declared)

- [ ] **Step 1: Failing tests**

```csharp
using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class LuaRuntimeTests
{
    [Fact]
    public void NumbersAreFloat_NotDouble()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return (2^24 + 1) == 2^24")));
    }

    [Fact]
    public void ScriptErrorSurfacesAsExceptionWithTraceback()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var ex = Assert.Throws<LuaScriptException>(
            () => rt.Execute(LuaFixtures.Compile("local f = function() error('boom') end f()"), "errchunk"));
        Assert.Contains("boom", ex.Message);
        Assert.Contains("errchunk", ex.Message);
    }
}
```

`(2^24+1)==2^24` is true only under 32-bit float — locks F2 forever.

- [ ] **Step 2: Run, expect FAIL (LuaRuntime missing)**

```powershell
dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~LuaRuntimeTests"
```

- [ ] **Step 3: Implement `LuaRuntime.cs`**

```csharp
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting;

public sealed class LuaScriptException(string message) : Exception(message);

public sealed class LuaRuntime : IDisposable
{
    private readonly LuaStateHandle _handle;
    private int _tracebackRef;
    internal nint L { get; }

    private LuaRuntime(nint state, LuaStateHandle handle)
    {
        L = state;
        _handle = handle;
    }

    public static LuaRuntime CreateSandboxedState()
    {
        var L = LuaNative.luaL_newstate();
        var handle = new LuaStateHandle();
        System.Runtime.InteropServices.Marshal.InitHandle(handle, L);
        var rt = new LuaRuntime(L, handle);
        rt.OpenSandboxedLibraries();
        return rt;
    }

    private void OpenSandboxedLibraries()
    {
        OpenLib(LuaNative.luaopen_base, "");
        OpenLib(LuaNative.luaopen_table, "table");
        OpenLib(LuaNative.luaopen_string, "string");
        OpenLib(LuaNative.luaopen_math, "math");
        OpenLib(LuaNative.luaopen_debug, "debug");
        LuaNative.lua_getfield(L, LuaNative.LUA_GLOBALSINDEX, "debug");
        LuaNative.lua_getfield(L, -1, "traceback");
        _tracebackRef = LuaNative.luaL_ref(L, LuaNative.LUA_REGISTRYINDEX);
        LuaNative.lua_settop(L, 0);
    }

    private void OpenLib(Func<nint, int> opener, string name)
    {
        unsafe
        {
            delegate* unmanaged[Cdecl]<nint, int> p = &SandboxOpeners.Invoke;
        }
        opener(L);
        LuaNative.lua_settop(L, 0);
    }

    public void Execute(byte[] chunk, string chunkName)
    {
        LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, _tracebackRef);
        var status = LuaNative.luaL_loadbuffer(L, chunk, (nuint)chunk.Length, chunkName);
        if (status != LuaNative.LUA_OK)
            ThrowTop(chunkName);
        status = LuaNative.lua_pcall(L, 0, 0, -2);
        if (status != LuaNative.LUA_OK)
            ThrowTop(chunkName);
        LuaNative.lua_settop(L, 0);
    }

    public bool EvalBool(byte[] chunk)
    {
        LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, _tracebackRef);
        if (LuaNative.luaL_loadbuffer(L, chunk, (nuint)chunk.Length, "eval") != LuaNative.LUA_OK)
            ThrowTop("eval");
        if (LuaNative.lua_pcall(L, 0, 1, -2) != LuaNative.LUA_OK)
            ThrowTop("eval");
        var result = LuaNative.lua_toboolean(L, -1) != 0;
        LuaNative.lua_settop(L, 0);
        return result;
    }

    private void ThrowTop(string chunkName)
    {
        var msg = LuaNative.ToManagedString(L, -1) ?? "unknown lua error";
        LuaNative.lua_settop(L, 0);
        throw new LuaScriptException($"[{chunkName}] {msg}");
    }

    public void Dispose() => _handle.Dispose();
}
```

Note: `OpenLib` simplification — the 5.1 `luaopen_*` functions must run on the Lua stack; calling them directly from C# works for these five (they don't yield). Remove the unused unsafe block if the compiler flags it; it's shown only to anchor where `[UnmanagedCallersOnly]` pointers will plug in (Task 9). Drop `SandboxOpeners` reference — direct `opener(L)` is the implementation.

- [ ] **Step 4: Run tests until green, then commit**

```powershell
dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~LuaRuntimeTests"
git add ReCap.Server ReCap.Tests
git commit -m "feat(scripting): LuaRuntime protected execution + float semantics locked by test"
```

---

### Task 5: Sandbox contract (F9)

**Files:**
- Modify: `ReCap.Server/Adapters/Scripting/LuaRuntime.cs` (extend `OpenSandboxedLibraries`)
- Test: `ReCap.Tests/Scripting/LuaSandboxTests.cs`

- [ ] **Step 1: Failing tests — exact F9 contract**

```csharp
using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class LuaSandboxTests
{
    private static bool GlobalIsNil(string name)
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        return rt.EvalBool(LuaFixtures.Compile($"return {name} == nil"));
    }

    [Theory]
    [InlineData("debug")]
    [InlineData("loadstring")]
    [InlineData("dofile")]
    [InlineData("loadfile")]
    [InlineData("loadlib")]
    [InlineData("package")]
    [InlineData("module")]
    public void BannedGlobalsAreNil(string name) => Assert.True(GlobalIsNil(name));

    [Theory]
    [InlineData("coroutine.resume")]
    [InlineData("string.format")]
    [InlineData("table.insert")]
    [InlineData("math.floor")]
    [InlineData("pcall")]
    [InlineData("pairs")]
    public void AllowedLibsArePresent(string expr)
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile($"return {expr} ~= nil")));
    }

    [Fact]
    public void PrintIsStubbedNotMissing()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        rt.Execute(LuaFixtures.Compile("print('hello from sandbox')"), "printtest");
    }
}
```

- [ ] **Step 2: Run, expect FAIL** (`debug` currently non-nil; `print` writes to stdout instead of log)

- [ ] **Step 3: Extend `OpenSandboxedLibraries`** — after the traceback ref capture:

```csharp
        foreach (var banned in new[] { "debug", "loadstring", "dofile", "loadfile", "loadlib", "package", "module" })
        {
            LuaNative.lua_pushnil(L);
            LuaNative.lua_setfield(L, LuaNative.LUA_GLOBALSINDEX, banned);
        }
        unsafe
        {
            LuaNative.lua_pushcclosure(L, (nint)(delegate* unmanaged[Cdecl]<nint, int>)&LuaStubs.Print, 0);
        }
        LuaNative.lua_setfield(L, LuaNative.LUA_GLOBALSINDEX, "print");
```

New static class in the same file:

```csharp
internal static class LuaStubs
{
    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static int Print(nint L)
    {
        var top = LuaNative.lua_gettop(L);
        var parts = new List<string>(top);
        for (var i = 1; i <= top; i++)
            parts.Add(LuaNative.ToManagedString(L, i) ?? LuaNative.lua_type(L, i).ToString());
        ReCap.Server.Util.Logging.Log.Lua.Debug($"[print] {string.Join("\t", parts)}");
        return 0;
    }
}
```

(`Log.Lua` added in Task 4. `ToManagedString` on non-string values returns null in raw API — acceptable for a stub; numbers print via type id. Improve only if a real script needs it.)

- [ ] **Step 4: Run until green, commit**

```powershell
dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~LuaSandboxTests"
git add ReCap.Server ReCap.Tests
git commit -m "feat(scripting): F9 sandbox - banned globals nil, print stubbed to Log.Lua"
```

---

### Task 6: PackageMounts

**Files:**
- Create: `ReCap.Server/Adapters/Persistence/PackageMounts.cs`
- Modify: `ReCap.Server/Adapters/Persistence/GameStorageAdapter.cs:41`, `ReCap.Server/Adapters/Persistence/LocaleStore.cs:58` (consume mounts instead of opening files)
- Test: `ReCap.Tests/Scripting/PackageMountsTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using ReCap.Server.Adapters.Persistence;

namespace ReCap.Tests.Scripting;

public class PackageMountsTests
{
    [Fact]
    public void UnknownDataDirYieldsNullReader()
    {
        var mounts = new PackageMounts(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.Null(mounts.Get(WellKnownPackage.ServerData));
    }

    [Fact]
    public void PackagePathsAreDataRelative()
    {
        Assert.Equal("ServerData.package", WellKnownPackage.ServerData.RelativePath);
        Assert.Equal(Path.Combine("Locale", "en-us", "Text.package"), WellKnownPackage.LocaleTextEnUs.RelativePath);
    }
}
```

- [ ] **Step 2: Run, expect FAIL; implement `PackageMounts.cs`**

```csharp
using AssetData.Parser.Core;

namespace ReCap.Server.Adapters.Persistence;

public sealed record WellKnownPackage(string RelativePath)
{
    public static readonly WellKnownPackage AssetDataBinary = new("AssetData_Binary.package");
    public static readonly WellKnownPackage ServerData = new("ServerData.package");
    public static readonly WellKnownPackage Web = new("Web.package");
    public static readonly WellKnownPackage LocaleTextEnUs = new(Path.Combine("Locale", "en-us", "Text.package"));
}

public sealed class PackageMounts(string dataDir)
{
    private readonly Dictionary<string, DbpfReader?> _open = [];
    private readonly Lock _gate = new();

    public string DataDir { get; } = dataDir;

    public DbpfReader? Get(WellKnownPackage package)
    {
        lock (_gate)
        {
            if (_open.TryGetValue(package.RelativePath, out var cached))
                return cached;
            var path = Path.Combine(DataDir, package.RelativePath);
            DbpfReader? reader = null;
            if (File.Exists(path))
                reader = new DbpfReader(path);
            else
                Util.Logging.Log.Assets.Warn($"Package not found: {path}");
            _open[package.RelativePath] = reader;
            return reader;
        }
    }
}
```

(Adjust the `DbpfReader` namespace import to the actual one in `lib/AssetData.Parser/src/Core/DbpfReader.cs` — check the file's namespace declaration.)

- [ ] **Step 3: Migrate consumers**

- `GameStorageAdapter` (lines 33–57): replace the lazy `new DbpfReader(webPackagePath)` block with a `PackageMounts` instance (constructor-injected or static-set from `Program.cs` like `ServerConfig`) → `_mounts.Get(WellKnownPackage.Web)`.
- `LocaleStore` (lines 40–58): same with `WellKnownPackage.LocaleTextEnUs`.
- `Program.cs`: construct `var mounts = new PackageMounts(Path.GetDirectoryName(ServerConfig.GamePath)!)` where AssetDatabase is built (guarded by the same GamePath null-check) and hand it to both consumers. `AssetDatabase` keeps its own reader for now (it owns parse state) — migrating it is cosmetic, skip (YAGNI).

- [ ] **Step 4: Run full suite (no regressions), commit**

```powershell
dotnet test ReCap.Tests/ReCap.Tests.csproj
git add ReCap.Server ReCap.Tests
git commit -m "feat(persistence): PackageMounts central package table; Web/Locale consumers migrated"
```

---

### Task 7: ScriptVfs — Group!Name.ext resolution

**Files:**
- Create: `ReCap.Server/Adapters/Scripting/ScriptVfs.cs`
- Test: `ReCap.Tests/Scripting/ScriptVfsTests.cs`

- [ ] **Step 1: Failing tests (golden FNV vectors from the probe — spec F5/F6)**

```csharp
using ReCap.Server.Adapters.Scripting;

namespace ReCap.Tests.Scripting;

public class ScriptVfsTests
{
    [Theory]
    [InlineData("Abilities", 0x7153BBB1u)]
    [InlineData("Modifiers", 0xFC0FF8F5u)]
    [InlineData("Lua", 0x3681D755u)]
    [InlineData("behaviors", 0xC130A42Au)]
    public void GroupHashMatchesVerifiedVectors(string group, uint expected) =>
        Assert.Equal(expected, ScriptVfs.Hash(group));

    [Fact]
    public void BareNameHashMatchesProbeVector() =>
        Assert.Equal(0xAD3290E1u, ScriptVfs.Hash("Affix_EnemyHealthRegen"));

    [Fact]
    public void ParsesGroupBangNameForm()
    {
        var key = ScriptVfs.ParseReference("Lua!GlobalDefinitions.lua");
        Assert.Equal(0x3681D755u, key.GroupId);
        Assert.Equal(ScriptVfs.Hash("GlobalDefinitions"), key.InstanceId);
        Assert.Equal(0x3681D755u, key.TypeId);
    }

    [Fact]
    public void ParsesBareNameAgainstSearchGroups()
    {
        var key = ScriptVfs.ParseReference("Affix_EnemyHealthRegen");
        Assert.Equal(0xAD3290E1u, key.InstanceId);
        Assert.Equal(0u, key.GroupId);
    }
}
```

- [ ] **Step 2: Run, expect FAIL; implement `ScriptVfs.cs`**

```csharp
using ReCap.Server.Adapters.Persistence;

namespace ReCap.Server.Adapters.Scripting;

public readonly record struct ScriptKey(uint GroupId, uint InstanceId, uint TypeId);

public sealed class ScriptVfs(PackageMounts mounts)
{
    private Dictionary<(uint Group, uint Instance), byte[]>? _chunks;
    private readonly Lock _gate = new();

    public static uint Hash(string s) => AssetData.Parser.Core.TypeModel.WireHash.Fnv1a(s);

    public static ScriptKey ParseReference(string reference)
    {
        var bang = reference.IndexOf('!');
        var group = bang >= 0 ? reference[..bang] : null;
        var rest = bang >= 0 ? reference[(bang + 1)..] : reference;
        var dot = rest.LastIndexOf('.');
        var name = dot >= 0 ? rest[..dot] : rest;
        var ext = dot >= 0 ? rest[(dot + 1)..] : "lua";
        return new ScriptKey(group is null ? 0u : Hash(group), Hash(name), Hash(ext));
    }

    public byte[]? GetChunk(ScriptKey key)
    {
        var chunks = EnsureIndex();
        if (chunks is null) return null;
        if (key.GroupId != 0)
            return chunks.TryGetValue((key.GroupId, key.InstanceId), out var exact) ? exact : null;
        foreach (var ((g, i), bytes) in chunks)
            if (i == key.InstanceId) return bytes;
        return null;
    }

    public IReadOnlyList<(uint Instance, byte[] Bytes)> GetGroup(uint groupId)
    {
        var chunks = EnsureIndex();
        if (chunks is null) return [];
        return chunks.Where(kv => kv.Key.Group == groupId)
                     .OrderBy(kv => kv.Key.Instance)
                     .Select(kv => (kv.Key.Instance, kv.Value))
                     .ToList();
    }

    private Dictionary<(uint, uint), byte[]>? EnsureIndex()
    {
        lock (_gate)
        {
            if (_chunks is not null) return _chunks;
            var reader = mounts.Get(WellKnownPackage.ServerData);
            if (reader is null) return null;
            var index = new Dictionary<(uint, uint), byte[]>();
            foreach (var (_, entry) in reader.ListAssetsByType("lua"))
            {
                var bytes = reader.ReadEntry(entry);
                if (bytes is not null)
                    index[(entry.GroupId, entry.InstanceId)] = bytes;
            }
            Util.Logging.Log.Assets.Info($"ScriptVfs indexed {index.Count} lua chunks from ServerData.package");
            _chunks = index;
            return _chunks;
        }
    }
}
```

Adjust `WireHash.Fnv1a` namespace + `DbpfEntry.GroupId/InstanceId` property names to the submodule's actuals (`lib/AssetData.Parser/src/Core/DbpfReader.cs`, `.../TypeModel/WireHash.cs`). If `ListAssetsByType` doesn't expose GroupId, enumerate the reader's entry table by TypeId `0x3681D755` instead — same data, the probe confirmed the fields exist on the index entries. Note: chunk-name ordering inside a group uses ascending InstanceId (deterministic; client catalog order unknown — spec §11 open question).

- [ ] **Step 3: Run until green (vector tests run without the game installed), commit**

```powershell
dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~ScriptVfsTests"
git add ReCap.Server ReCap.Tests
git commit -m "feat(scripting): ScriptVfs Group!Name.ext resolution over ServerData (F5/F6/F8 vectors)"
```

---

### Task 8: Custom `require` (F8)

**Files:**
- Modify: `ReCap.Server/Adapters/Scripting/LuaRuntime.cs` (install require; needs a `ScriptVfs` + loaded-cache)
- Test: `ReCap.Tests/Scripting/LuaRequireTests.cs`

Design: `LuaRuntime.CreateSandboxedState(Func<string, byte[]?>? chunkResolver = null)` — runtime stays VFS-agnostic (testable without the game); `ScriptEngine` passes the real resolver later. Loaded-cache = registry table `"recap.loaded"` keyed by the literal require string; a chunk runs once, subsequent requires return `true` (client semantics: execute-once guard via `FUN_008f42e0`; return-value passthrough not evidenced — keep `true`, revisit if a real script consumes require's return).

- [ ] **Step 1: Failing tests**

```csharp
using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class LuaRequireTests
{
    [Fact]
    public void RequireLoadsThroughResolverOnce()
    {
        var loads = 0;
        var dep = LuaFixtures.Compile("DepLoaded = (DepLoaded or 0) + 1");
        using var rt = LuaRuntime.CreateSandboxedState(name =>
        {
            Assert.Equal("Lua!Dep.lua", name);
            loads++;
            return dep;
        });
        rt.Execute(LuaFixtures.Compile("require('Lua!Dep.lua') require('Lua!Dep.lua')"), "main");
        Assert.Equal(1, loads);
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return DepLoaded == 1")));
    }

    [Fact]
    public void RequireUnknownChunkRaisesLuaError()
    {
        using var rt = LuaRuntime.CreateSandboxedState(_ => null);
        Assert.Throws<LuaScriptException>(
            () => rt.Execute(LuaFixtures.Compile("require('Lua!Missing.lua')"), "main"));
    }
}
```

- [ ] **Step 2: Run, expect FAIL; implement**

In `LuaRuntime`: store the resolver in a static `ConcurrentDictionary<nint, Func<string, byte[]?>>` keyed by `L` (registered at create, removed on dispose — `[UnmanagedCallersOnly]` can't capture instance state). Install:

```csharp
        unsafe
        {
            LuaNative.lua_pushcclosure(L, (nint)(delegate* unmanaged[Cdecl]<nint, int>)&LuaStubs.Require, 0);
        }
        LuaNative.lua_setfield(L, LuaNative.LUA_GLOBALSINDEX, "require");
```

`LuaStubs.Require`:

```csharp
    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static int Require(nint L)
    {
        var name = LuaNative.ToManagedString(L, 1);
        if (name is null) return Fail(L, "require: string expected");
        LuaNative.lua_getfield(L, LuaNative.LUA_REGISTRYINDEX, "recap.loaded");
        if (LuaNative.lua_type(L, -1) != LuaNative.LUA_TTABLE)
        {
            LuaNative.lua_settop(L, 1);
            LuaNative.lua_createtable(L, 0, 32);
            LuaNative.lua_pushvalue(L, -1);
            LuaNative.lua_setfield(L, LuaNative.LUA_REGISTRYINDEX, "recap.loaded");
        }
        LuaNative.lua_getfield(L, -1, name);
        if (LuaNative.lua_toboolean(L, -1) != 0)
        {
            LuaNative.lua_pushboolean(L, 1);
            return 1;
        }
        if (!LuaRuntime.TryResolveChunk(L, name, out var chunk) || chunk is null)
            return Fail(L, $"require: chunk not found: {name}");
        if (LuaNative.luaL_loadbuffer(L, chunk, (nuint)chunk.Length, name) != LuaNative.LUA_OK)
            return LuaNative.lua_error(L);
        if (LuaNative.lua_pcall(L, 0, 0, 0) != LuaNative.LUA_OK)
            return LuaNative.lua_error(L);
        LuaNative.lua_getfield(L, LuaNative.LUA_REGISTRYINDEX, "recap.loaded");
        LuaNative.lua_pushboolean(L, 1);
        LuaNative.lua_setfield(L, -2, name);
        LuaNative.lua_pushboolean(L, 1);
        return 1;
    }

    private static int Fail(nint L, string message)
    {
        LuaNative.lua_pushstring(L, message);
        return LuaNative.lua_error(L);
    }
```

`LuaRuntime.TryResolveChunk(nint L, string name, out byte[]? chunk)` reads the static resolver map. **Exception discipline:** nothing may throw across the `UnmanagedCallersOnly` boundary — wrap the resolver call in try/catch, convert to `Fail(L, ...)`.

- [ ] **Step 3: Run until green, commit**

```powershell
dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~LuaRequireTests"
git add ReCap.Server ReCap.Tests
git commit -m "feat(scripting): VFS-backed require with execute-once cache (F8)"
```

---

### Task 9: API modules — RegisterNamespace, stub fallback, real nUtil/nMathUtil

**Files:**
- Create: `ReCap.Server/Adapters/Scripting/Api/LuaApiModule.cs`, `.../Api/NUtilModule.cs`, `.../Api/NMathUtilModule.cs`, `.../Api/StubNamespaces.cs`
- Test: `ReCap.Tests/Scripting/LuaApiModuleTests.cs`

ID representation: implement exactly what Task 2 Step 3 found (lightuserdata vs string vs other). The code below assumes **lightuserdata carrying the uint32 in the pointer** — if Task 2 found otherwise, follow Task 2's evidence and adjust `PushId`/`ReadId` only (single seam).

- [ ] **Step 1: Failing tests**

```csharp
using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class LuaApiModuleTests
{
    [Fact]
    public void UnknownNamespaceFunctionIsCallableStub()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        rt.Execute(LuaFixtures.Compile("nGameObject.TotallyUnknownFn(1, 2, 3)"), "stubtest");
    }

    [Fact]
    public void NUtilSpidMatchesFnvVector()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile(
            "return nUtil.SPID('Affix_EnemyHealthRegen') == nUtil.SPID('affix_enemyhealthregen')")));
    }

    [Fact]
    public void NamespacesExistAfterRegistration()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile(
            "return nAbility ~= nil and nGameObject ~= nil and nThread ~= nil and nModifier ~= nil")));
    }
}
```

(SPID equality test works for any ID representation — it only requires determinism + case-folding.)

- [ ] **Step 2: Run, expect FAIL; implement `LuaApiModule.cs`**

```csharp
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class LuaApiModule
{
    public static void RegisterNamespace(nint L, string name,
        params (string Name, delegate* unmanaged[Cdecl]<nint, int> Fn)[] entries)
    {
        LuaNative.lua_getfield(L, LuaNative.LUA_GLOBALSINDEX, name);
        if (LuaNative.lua_type(L, -1) != LuaNative.LUA_TTABLE)
        {
            LuaNative.lua_settop(L, -2);
            LuaNative.lua_createtable(L, 0, entries.Length);
            LuaNative.lua_pushvalue(L, -1);
            LuaNative.lua_setfield(L, LuaNative.LUA_GLOBALSINDEX, name);
        }
        foreach (var (fnName, fn) in entries)
        {
            LuaNative.lua_pushstring(L, fnName);
            LuaNative.lua_pushcclosure(L, (nint)fn, 0);
            LuaNative.lua_rawset(L, -3);
        }
        AttachStubFallback(L, name);
        LuaNative.lua_settop(L, -2);
    }

    private static void AttachStubFallback(nint L, string nsName)
    {
        LuaNative.lua_createtable(L, 0, 1);
        LuaNative.lua_pushstring(L, "__index");
        LuaNative.lua_pushstring(L, nsName);
        LuaNative.lua_pushcclosure(L, (nint)(delegate* unmanaged[Cdecl]<nint, int>)&StubIndex, 1);
        LuaNative.lua_rawset(L, -3);
        LuaNative.lua_setmetatable(L, -2);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int StubIndex(nint L)
    {
        var ns = LuaNative.ToManagedString(L, LuaUpvalueIndex(1)) ?? "?";
        var key = LuaNative.ToManagedString(L, 2) ?? "?";
        StubTelemetry.RecordLookup(ns, key);
        LuaNative.lua_pushstring(L, ns);
        LuaNative.lua_pushstring(L, key);
        LuaNative.lua_pushcclosure(L, (nint)(delegate* unmanaged[Cdecl]<nint, int>)&StubCall, 2);
        return 1;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int StubCall(nint L)
    {
        var ns = LuaNative.ToManagedString(L, LuaUpvalueIndex(1)) ?? "?";
        var key = LuaNative.ToManagedString(L, LuaUpvalueIndex(2)) ?? "?";
        StubTelemetry.RecordCall(ns, key);
        return 0;
    }

    internal static int LuaUpvalueIndex(int i) => LuaNative.LUA_GLOBALSINDEX - i;
}

internal static class StubTelemetry
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _seen = [];

    public static void RecordLookup(string ns, string key)
    {
        if (_seen.TryAdd($"{ns}.{key}", 0))
            Util.Logging.Log.Lua.Warn($"unimplemented {ns}.{key} (first lookup)");
    }

    public static void RecordCall(string ns, string key)
    {
        if (_seen.TryAdd($"{ns}.{key}:called", 0))
            Util.Logging.Log.Lua.Warn($"unimplemented {ns}.{key} CALLED - returns nothing");
    }

    public static IReadOnlyCollection<string> Snapshot() => _seen.Keys.OrderBy(k => k).ToList();
}
```

- [ ] **Step 3: Implement `StubNamespaces.cs`**

`RegisterAll(nint L)` registers EVERY namespace from F11 with zero real entries (fallback covers them): `nAbility, nModifier, nCondition, nGameObject, nThread, nThreadData, nPhysics, nUtil, nBit, nAttribute, nLocomotion, nObjectManager, nPlayer, nEvent, nAgent, nDebug, nMathUtil, nGameDirector, nGameSimulator, nLevel, nObjective, nAffix, nJuggernaut, nTuning, nTimeManager, nBehaviorTree, nScenarioManager, nClient, nNPCType, nAbilityAnimationSelection` — plus any extra namespaces Task 2 discovered. Then `NUtilModule.Register(L)` / `NMathUtilModule.Register(L)` overwrite their tables' real entries (RegisterNamespace is get-or-create, so order: stubs first, real modules after).

- [ ] **Step 4: Implement `NUtilModule.cs`**

Real entries: `SPID` (FNV via `ScriptVfs.Hash`, push per Task 2's ID representation), `ToGUID` (per Task 2 evidence — 64-bit: representation finding applies doubly), `GetAsset` (stub-log for now — returns nothing until AssetDatabase XML serving is designed; record in telemetry). `NMathUtilModule.cs`: implement the 6 pure functions from the C++ survey signatures (`RotateVectorByAxisAngle`, `TransformVector`, `DistanceToLine`, `ClosestPointOnLine`, `GetQuaternionFromFacingAndPosition`, `CircleIntersectsArc`) only if vec3 representation (table? 3 numbers?) is known from decompiled scripts — otherwise register the namespace and leave the fallback logging; defer real math to P3 when a script demands it. (Vector.lua in the boot set may define Lua-side vectors — the telemetry will say.)

- [ ] **Step 5: Wire registration into `LuaRuntime.CreateSandboxedState`** (after sandbox, before returning): `StubNamespaces.RegisterAll(L); NUtilModule.Register(L); NMathUtilModule.Register(L);` plus `math.random` native override:

```csharp
        LuaNative.lua_getfield(L, LuaNative.LUA_GLOBALSINDEX, "math");
        LuaNative.lua_pushstring(L, "random");
        unsafe { LuaNative.lua_pushcclosure(L, (nint)(delegate* unmanaged[Cdecl]<nint, int>)&LuaStubs.MathRandom, 0); }
        LuaNative.lua_rawset(L, -3);
        LuaNative.lua_settop(L, -2);
```

`MathRandom`: 0 args → `[0,1)` float; 1 arg `m` → integer `[1,m]`; 2 args → `[m,n]`; backed by a static `Random` (`Random.Shared`).

- [ ] **Step 6: Run until green, commit**

```powershell
dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~LuaApiModuleTests"
git add ReCap.Server ReCap.Tests
git commit -m "feat(scripting): n* namespaces stub-first with telemetry + nUtil.SPID real"
```

---

### Task 10: ScriptEngine — boot in client group order + smoke gate

**Files:**
- Create: `ReCap.Server/Services/Scripting/ScriptEngine.cs`
- Test: `ReCap.Tests/Scripting/ScriptEngineBootTests.cs` (integration, self-skipping)
- Modify: `ReCap.Server/Program.cs` (`--lua-smoke` flag)

- [ ] **Step 1: Implement `ScriptEngine.cs`**

```csharp
using ReCap.Server.Adapters.Scripting;

namespace ReCap.Server.Services.Scripting;

public sealed class ScriptEngine(ScriptVfs vfs)
{
    private static readonly uint[] BootGroupOrder =
    [
        // exact array + order extracted from LuaSystem::Initialize 0x00a0c810 (Task 2);
        // replace with Task 2's verbatim result if it differs:
        0x3681D755, 0xDA09176B, 0xFC0FF8F5, 0xD2FCB262, 0x7153BBB1,
        0xD79FA88C, 0xC130A42A, 0xB2A79C5C, 0xEE84D09A, 0x24F78AA1,
    ];

    public BootReport ExecuteBootScripts(LuaRuntime runtime)
    {
        var report = new BootReport();
        foreach (var group in BootGroupOrder)
        {
            foreach (var (instance, bytes) in vfs.GetGroup(group))
            {
                report.Total++;
                try
                {
                    runtime.Execute(bytes, $"0x{group:X8}!0x{instance:X8}");
                }
                catch (LuaScriptException ex)
                {
                    report.Failures.Add(ex.Message);
                    Util.Logging.Log.Lua.Error(ex.Message);
                }
            }
        }
        return report;
    }

    public LuaRuntime CreateBootedRuntime()
    {
        var runtime = LuaRuntime.CreateSandboxedState(name => vfs.GetChunk(ScriptVfs.ParseReference(name)));
        ExecuteBootScripts(runtime);
        return runtime;
    }
}

public sealed class BootReport
{
    public int Total { get; set; }
    public List<string> Failures { get; } = [];
}
```

- [ ] **Step 2: Integration test (skips without the game)**

```csharp
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Services.Scripting;

namespace ReCap.Tests.Scripting;

public class ScriptEngineBootTests
{
    private static string? FindDataDir()
    {
        var env = Environment.GetEnvironmentVariable("RECAP_GAME_DATA");
        if (env is not null && File.Exists(Path.Combine(env, "ServerData.package"))) return env;
        var known = @"C:\CodingProjects\Personal\Darkspore\Data";
        return File.Exists(Path.Combine(known, "ServerData.package")) ? known : null;
    }

    [Fact]
    public void BootsAllServerDataScripts()
    {
        var dataDir = FindDataDir();
        if (dataDir is null) return;
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(dataDir)));
        using var rt = LuaRuntime.CreateSandboxedState(n => null);
        var report = engine.ExecuteBootScripts(rt);
        Assert.True(report.Total > 900, $"expected ~1042 chunks, indexed {report.Total}");
        Assert.True(report.Failures.Count < report.Total / 10,
            $"boot failures {report.Failures.Count}/{report.Total}:\n{string.Join("\n", report.Failures.Take(20))}");
    }
}
```

(First run WILL surface failures — scripts requiring chunks, hitting stubs that must return values, etc. The `<10%` threshold is the P1 ratchet; tighten to 0 as stubs gain real impls. Wire the real resolver: replace `n => null` with a `vfs`-backed resolver as in `CreateBootedRuntime` — kept explicit here so the test exercises the same path.)

- [ ] **Step 3: `--lua-smoke` flag in `Program.cs`**

Follow the existing arg-constant pattern (`Program.cs:25-32`): `_LUA_SMOKE_ARG = "--lua-smoke"`. When set AND assets configured: build `PackageMounts`/`ScriptVfs`/`ScriptEngine`, run `CreateBootedRuntime()`, log `report.Total`, each failure, and `StubTelemetry.Snapshot()` counts, then continue normal startup. This output IS the P3 backlog.

- [ ] **Step 4: Run gate, iterate obvious quick fixes, commit**

```powershell
dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~ScriptEngineBootTests"
dotnet run --project ReCap.Server -- "--assetdata-path=C:\CodingProjects\Personal\Darkspore\Data\AssetData_Binary.package" --lua-smoke
```

Expected: `ScriptVfs indexed 1042 lua chunks`, boot report printed. Quick fixes allowed in this task: stubs that must return a value to unblock many scripts (telemetry shows them) MAY be given minimal real returns ONLY if pure (e.g. `nThread.GetValue` → nil is automatic). Anything touching game state is P3 — leave failing, the ratchet documents it.

```powershell
git add ReCap.Server ReCap.Tests
git commit -m "feat(scripting): ScriptEngine boot in client group order + --lua-smoke gate (P1)"
```

---

### Task 11: GameInstallLocator (P2)

**Files:**
- Create: `ReCap.Server/Services/GameInstallLocator.cs`
- Test: `ReCap.Tests/GameInstall/GameInstallLocatorTests.cs`
- Modify: `ReCap.Server/Config/ServerConfigOptions.cs` (add `GameRoot`, `DataDir`), `ReCap.Server/Program.cs` (`--game-path`, alias, persistence, wiring)

- [ ] **Step 1: Failing tests (chain is pure given injected probes)**

```csharp
using ReCap.Server.Services;

namespace ReCap.Tests.GameInstall;

public class GameInstallLocatorTests
{
    private static string MakeFakeInstall()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ds_{Guid.NewGuid():N}");
        var data = Path.Combine(root, "Data");
        Directory.CreateDirectory(data);
        File.WriteAllBytes(Path.Combine(data, "AssetData_Binary.package"), [0]);
        File.WriteAllBytes(Path.Combine(data, "ServerData.package"), [0]);
        return root;
    }

    [Fact]
    public void CliPathWinsAndNormalizesRootDataOrFile()
    {
        var root = MakeFakeInstall();
        foreach (var input in new[]
        {
            root,
            Path.Combine(root, "Data"),
            Path.Combine(root, "Data", "AssetData_Binary.package"),
        })
        {
            var result = GameInstallLocator.Normalize(input);
            Assert.NotNull(result);
            Assert.Equal(Path.Combine(root, "Data"), result!.DataDir);
        }
    }

    [Fact]
    public void InvalidPathYieldsNull() =>
        Assert.Null(GameInstallLocator.Normalize(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

    [Fact]
    public void PersistedPathRoundTrips()
    {
        var root = MakeFakeInstall();
        var store = Path.Combine(Path.GetTempPath(), $"gp_{Guid.NewGuid():N}.json");
        GameInstallLocator.Persist(store, root);
        var resolved = GameInstallLocator.Resolve(cliPath: null, persistencePath: store);
        Assert.NotNull(resolved);
        Assert.Equal(Path.Combine(root, "Data"), resolved!.DataDir);
    }

    [Fact]
    public void ChainFallsThroughToNullWithClearReasons()
    {
        var store = Path.Combine(Path.GetTempPath(), $"gp_{Guid.NewGuid():N}.json");
        var resolved = GameInstallLocator.Resolve(cliPath: null, persistencePath: store, probePaths: []);
        Assert.Null(resolved);
    }
}
```

- [ ] **Step 2: Run, expect FAIL; implement `GameInstallLocator.cs`**

```csharp
using System.Text.Json;

namespace ReCap.Server.Services;

public sealed record GameInstall(string Root, string DataDir);

public static class GameInstallLocator
{
    public static GameInstall? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = Path.GetFullPath(path.Trim('"', '\''));
        if (File.Exists(path))
            path = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(path)) return null;
        foreach (var candidate in new[] { path, Path.Combine(path, "Data"), Path.GetDirectoryName(path) ?? path })
        {
            var dataDir = Path.GetFileName(candidate).Equals("Data", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : Path.Combine(candidate, "Data");
            if (File.Exists(Path.Combine(dataDir, "AssetData_Binary.package")) &&
                File.Exists(Path.Combine(dataDir, "ServerData.package")))
                return new GameInstall(Path.GetDirectoryName(dataDir)!, dataDir);
        }
        return null;
    }

    public static GameInstall? Resolve(string? cliPath, string persistencePath, IReadOnlyList<string>? probePaths = null)
    {
        var fromCli = Normalize(cliPath);
        if (fromCli is not null) { Persist(persistencePath, fromCli.Root); return fromCli; }

        if (File.Exists(persistencePath))
        {
            var saved = JsonSerializer.Deserialize<PersistedPath>(File.ReadAllText(persistencePath));
            var fromSaved = Normalize(saved?.Root);
            if (fromSaved is not null) return fromSaved;
        }

        var fromRegistry = Normalize(ProbeRegistry());
        if (fromRegistry is not null) { Persist(persistencePath, fromRegistry.Root); return fromRegistry; }

        foreach (var probe in probePaths ?? DefaultProbePaths())
        {
            var hit = Normalize(probe);
            if (hit is not null) { Persist(persistencePath, hit.Root); return hit; }
        }
        return null;
    }

    public static void Persist(string persistencePath, string root) =>
        File.WriteAllText(persistencePath, JsonSerializer.Serialize(new PersistedPath(root)));

    private static string? ProbeRegistry()
    {
        if (!OperatingSystem.IsWindows()) return null;
        foreach (var keyPath in new[]
        {
            @"SOFTWARE\WOW6432Node\Electronic Arts\Darkspore",
            @"SOFTWARE\Electronic Arts\Darkspore",
        })
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
            if (key?.GetValue("InstallDir") is string dir) return dir;
        }
        using var uninstall = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        foreach (var sub in uninstall?.GetSubKeyNames() ?? [])
        {
            using var k = uninstall!.OpenSubKey(sub);
            if (k?.GetValue("DisplayName") is string n && n.Contains("Darkspore", StringComparison.OrdinalIgnoreCase)
                && k.GetValue("InstallLocation") is string loc && loc.Length > 0)
                return loc;
        }
        return null;
    }

    private static IReadOnlyList<string> DefaultProbePaths() =>
    [
        @"C:\Program Files (x86)\Origin Games\Darkspore",
        @"C:\Program Files (x86)\Electronic Arts\Darkspore",
        @"C:\Program Files (x86)\Steam\steamapps\common\Darkspore",
        @"C:\Games\Darkspore",
    ];

    private sealed record PersistedPath(string Root);
}
```

(Steam `libraryfolders.vdf` parsing: skipped — uninstall-key scan covers Steam installs too; add only if a real install proves otherwise. YAGNI.)

- [ ] **Step 3: Wire into `Program.cs`**

- New arg constant `_GAME_PATH_ARG = "--game-path="` parsed like the others (`Program.cs:26-89`).
- `--assetdata-path` keeps working: its value feeds `GameInstallLocator.Normalize` too (it's a file path inside `Data/` → normalizes to the same root). Log a deprecation note when used.
- Persistence path: `Path.Combine(ServerConfig.ServerDatabaseDirectory, "game-path.json")`.
- Resolution replaces the current `File.Exists(gPath)` block: `var install = GameInstallLocator.Resolve(cliGamePath ?? cliAssetDataPath, persistencePath);`
  - found → set `serverOpts.GameRoot = install.Root`, `serverOpts.DataDir = install.DataDir`, and keep `serverOpts.GamePath = Path.Combine(install.DataDir, "AssetData_Binary.package")` so every existing consumer is untouched.
  - not found → `Log.Server.Warn` with the exact chain tried and the fix (`--game-path=<folder do jogo>`); server continues degraded (current behavior).
- `ServerConfigOptions.cs`: add `public string GameRoot { get; set; } = string.Empty;` and `public string DataDir { get; set; } = string.Empty;` following the existing property style; mirror in `ServerConfig` static accessors.

- [ ] **Step 4: Run everything + manual gate, commit**

```powershell
dotnet test ReCap.Tests/ReCap.Tests.csproj
dotnet run --project ReCap.Server -- --lua-smoke
```

Manual gate (P2): second command runs with NO path flag on this machine — first run resolves via probe/persisted (this machine needs one `--game-path=C:\CodingProjects\Personal\Darkspore` priming run since its location is non-standard), subsequent runs resolve from `game-path.json`.

```powershell
git add ReCap.Server ReCap.Tests
git commit -m "feat(config): GameInstallLocator chain + persistence; --game-path with --assetdata-path alias (P2)"
```

---

### Task 12: Docs + memory closeout

**Files:**
- Modify: `CLAUDE.md` (Build & Run: native build step + `--game-path`/`--lua-smoke`), `docs/architecture/VERIFIED_FACTS.md` (any facts confirmed during execution, e.g. boot order), `docs/architecture/planning/PORTING_MATRIX.md` (Lua system row: status)

- [ ] **Step 1: Update CLAUDE.md Build & Run**

Add after the dotnet build lines: one-time `powershell -File native/lua51/build.ps1` (requires VS Build Tools C++), and the new flags `--game-path=<folder>` (auto-detected/persisted afterwards) + `--lua-smoke`.

- [ ] **Step 2: Update PORTING_MATRIX + VERIFIED_FACTS** with what execution actually confirmed (boot count, group order verbatim, ID representation).

- [ ] **Step 3: Commit**

```powershell
git add CLAUDE.md docs
git commit -m "docs: lua scripting P0-P2 closeout (build steps, verified facts, matrix)"
```

---

## Self-Review (done at planning time)

- **Spec coverage:** D1 (T1,T3), D2 (T1, csproj guard T3), D3 (T11), D4 partial — per-Game contexts arrive in P3 (boot runtime proves the path; `CreateBootedRuntime` is the per-Game factory-to-be), D5 (T9), D6 (T6); F7 order (T2→T10), F8 (T7,T8), F9 (T5), F2 locked by test (T4). Scheduler/F10 = P3 plan (out of scope here by design).
- **Placeholders:** none — every step has code/commands. Two intentional evidence-dependent seams are explicit: ID representation (T2→T9) and boot-order array (T2→T10).
- **Type consistency:** `LuaRuntime.CreateSandboxedState(Func<string,byte[]?>?)` introduced T4, optional param added T8 — executor adds the overload in T8, default null keeps T4/T5 tests compiling. `ScriptVfs.Hash`/`ParseReference`/`GetChunk`/`GetGroup` used consistently in T7/T8/T10. `Log.Lua` created T4, used T5/T9/T10.
