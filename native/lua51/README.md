# native/lua51 — Darkspore-compatible Lua 5.1.4 build

Lua 5.1.4 vendored and patched to produce bytecode compatible with Darkspore's
`ServerData.package` Lua chunks.

## Why this exists

Darkspore ships compiled Lua 5.1 bytecode built with a 32-bit toolchain where
`lua_Number = float`. All 1,042 chunks in `ServerData.package` carry this header:

```
1B 4C 75 61 51 00 01 04 04 04 04 00
```

Decoded per the Lua 5.1 header format:
| Byte | Value | Meaning                        |
|------|-------|-------------------------------|
| 0–3  | `\x1bLua` | Lua signature             |
| 4    | `0x51` | Version 5.1                  |
| 5    | `0x00` | Format 0 (official)          |
| 6    | `0x01` | Little-endian                |
| 7    | `0x04` | sizeof(int) = 4              |
| 8    | `0x04` | sizeof(size_t) = 4           |
| 9    | `0x04` | sizeof(Instruction) = 4      |
| 10   | `0x04` | sizeof(lua_Number) = 4 (float)|
| 11   | `0x00` | lua_Number is not integral   |

The client's header validation is at `0x00909cb0` in Darkspore.exe. A standard
x64 Lua build uses `double` (8 bytes) and native `size_t` (8 bytes on x64),
producing a mismatched header that the client rejects.

## Patches applied

Two patches over pristine Lua 5.1.4 (visible via `git diff 3e833ed HEAD`):

### `patches/0001-lua-number-float.patch` — `LUA_NUMBER = float`
- Removes `#define LUA_NUMBER_DOUBLE` (disabling the x87 `fistp` trick; the
  generic `(int)(d)` cast at the `#else` branch is used instead, correct on x64)
- Sets `LUA_NUMBER` to `float`
- Adjusts `LUA_NUMBER_SCAN` to `"%f"`, `LUA_NUMBER_FMT` to `"%.7g"`
- Adjusts `lua_str2number` to cast via `(lua_Number)strtod(...)` (avoids
  double→float implicit-conversion warning)
- `LUAI_UACNUMBER` stays `double` (variadic promotion, unchanged)

### `patches/0002-bytecode-size-u32.patch` — bytecode string sizes as u32
- `lundump.c / LoadString`: reads a `lu_int32` instead of `size_t`, so the
  loader always consumes exactly 4 bytes for each string size field regardless
  of host pointer width.
- `lundump.c / luaU_header`: forces the size_t header byte to `4` (constant)
  instead of `sizeof(size_t)`, so the header always claims 32-bit chunk format.
- `ldump.c / DumpString`: writes `lu_int32` for string sizes so our `luac.exe`
  produces chunks in the game format (round-trips correctly).

(`lu_int32` is `typedef LUAI_UINT32 lu_int32` in `llimits.h`.)

## Expected header

After building, `luac.exe -s` on any source file must start with:

```
1B 4C 75 61 51 00 01 04 04 04 04 00
```

## Prerequisites

- Windows 10/11
- Visual Studio Build Tools with the "Desktop development with C++" workload
  (includes MSVC x64 toolchain and vswhere.exe)

## Build

```powershell
powershell -File native\lua51\build.ps1
```

Produces:
- `native/lua51/out/recaplua51.dll` — Lua 5.1 runtime (for embedding in ReCap.Server via P/Invoke or similar)
- `native/lua51/out/luac.exe` — Lua 5.1 compiler producing Darkspore-format chunks

The `out/` directory is gitignored. Delete it and re-run `build.ps1` to rebuild
from a clean state (idempotent).

## Integration note

`dotnet build` for ReCap.Server should copy `recaplua51.dll` to the output
directory. A `<Content>` or post-build step for that copy has not been added yet
and is deferred to the integration task.
