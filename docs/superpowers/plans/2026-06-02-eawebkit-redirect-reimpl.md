# EAWebKit redirect reimplementation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reimplement Xackery's client-redirect mods directly in the EAWebKit source we build ourselves (no Detours), so Darkspore's web layer reaches a configurable ReCap server (local or remote) and accepts its self-signed cert.

**Architecture:** Three small edits to the DirtySDK package (DNS redirect, HTTP port remap, SSL verify bypass), each gated by a config file (`recap.cfg`) read once by a new `recapredirect` module. The DLL is the stock EAWebKit 1.21 tree (builds green in VS2008); we only touch DirtySDK sources + the `dirtysock.vcproj`.

**Tech Stack:** C (DirtySDK / EAWebKit), VS2008 (VC9) build, Win32 (`GetModuleFileNameA`). Parser unit-tested with MinGW gcc (`C:\Strawberry\c\bin\gcc.exe`).

**Spec:** `docs/superpowers/specs/2026-06-02-eawebkit-redirect-reimpl-design.md`

**Ground-truth paths (absolute):**
- DirtySDK core: `C:\CodingProjects\Personal\eawebkit\EAWebKitSupportPackages\DirtySDKEAWebKit\local\core`
- `.vcproj`: `C:\CodingProjects\Personal\eawebkit\projects\VS2008\DirtySDKEAWebKit\local\dirtysock.vcproj`
- Solution: `C:\CodingProjects\Personal\eawebkit\projects\VS2008\EAWebKit\1.21.00.darkspore\EAWebKit.sln`
- Built DLL output: `C:\CodingProjects\Personal\eawebkit\Distribution\pc\9.0.21022\dev-opt\bin\EAWebkit.dll`

---

## File Structure

| File | Responsibility |
|---|---|
| `…/core/include/recapredirect.h` (NEW) | Public C API: `RecapRedirectConfig`, `RecapRedirectGet()`, and the pure `RecapRedirectParse()` (testable, no Win32). |
| `…/core/source/dirtysock/recapredirect.c` (NEW) | `RecapRedirectParse()` (pure INI parse) + `RecapRedirectGet()` (Win32 module-path + file read + once-cache). |
| `…/core/source/dirtysock/pc/dirtynetwin.c` (EDIT) | `SocketLookupThread` → resolve to `cfg->uHostAddr`; `_MapAddress` → remap dest port 80 → `cfg->uHttpPort`. |
| `…/core/source/proto/protossl.c` (EDIT) | One-line gate at the server-cert check → honor `cfg->bSslBypass`. |
| `…/projects/VS2008/DirtySDKEAWebKit/local/dirtysock.vcproj` (EDIT) | Compile `recapredirect.c`. |
| `recap.cfg` (NEW template) | Ships next to `EAWebKit.dll`; `host`/`http_port`/`ssl_bypass`. |
| `…/eawebkit/tests/recapredirect_test.c` (NEW, dev-only) | gcc host test for `RecapRedirectParse()`. Not in the VS2008 build. |

All edits are in the `eawebkit` tree (NOT a git repo). Commit only the ReCap-repo artifacts (this plan / spec / memory). The eawebkit edits are tracked manually.

---

## Task 1: Config module — pure INI parser (TDD)

**Files:**
- Create: `C:\CodingProjects\Personal\eawebkit\EAWebKitSupportPackages\DirtySDKEAWebKit\local\core\include\recapredirect.h`
- Create: `C:\CodingProjects\Personal\eawebkit\EAWebKitSupportPackages\DirtySDKEAWebKit\local\core\source\dirtysock\recapredirect.c`
- Test: `C:\CodingProjects\Personal\eawebkit\tests\recapredirect_test.c`

- [ ] **Step 1: Write the header**

`recapredirect.h`:
```c
#ifndef RECAP_REDIRECT_H
#define RECAP_REDIRECT_H

#ifdef __cplusplus
extern "C" {
#endif

typedef struct RecapRedirectConfig
{
    unsigned int uHostAddr;   /* assembled (a<<24)|(b<<16)|(c<<8)|d, e.g. 0x7F000001 = 127.0.0.1 */
    unsigned int uHttpPort;   /* HTTP target port (default 8033) */
    int          bSslBypass;  /* nonzero = skip cert/host verification (default 1) */
} RecapRedirectConfig;

/* Pure parser: applies key=value lines from pText onto pCfg (which must already hold
   defaults). No I/O, no Win32 — unit-testable. Unknown keys / malformed lines ignored. */
void RecapRedirectParse(const char *pText, RecapRedirectConfig *pCfg);

/* Lazy-loads recap.cfg from the DLL's own folder on first call; cached thereafter.
   Never fails: on any error returns defaults (127.0.0.1 / 8033 / bypass=1). */
const RecapRedirectConfig *RecapRedirectGet(void);

#ifdef __cplusplus
}
#endif
#endif /* RECAP_REDIRECT_H */
```

- [ ] **Step 2: Write the failing test**

`tests/recapredirect_test.c`:
```c
#include <stdio.h>
#include <string.h>
#include "recapredirect.h"

static RecapRedirectConfig defaults(void)
{
    RecapRedirectConfig c;
    c.uHostAddr = 0x7F000001u;
    c.uHttpPort = 8033u;
    c.bSslBypass = 1;
    return c;
}

static int g_fail = 0;
#define CHECK(cond) do { if (!(cond)) { printf("FAIL: %s (line %d)\n", #cond, __LINE__); g_fail = 1; } } while (0)

int main(void)
{
    /* dotted IPv4 host */
    {
        RecapRedirectConfig c = defaults();
        RecapRedirectParse("host=192.168.0.50\n", &c);
        CHECK(c.uHostAddr == ((192u<<24)|(168u<<16)|(0u<<8)|50u));
    }
    /* http_port */
    {
        RecapRedirectConfig c = defaults();
        RecapRedirectParse("http_port=9000\n", &c);
        CHECK(c.uHttpPort == 9000u);
    }
    /* ssl_bypass off */
    {
        RecapRedirectConfig c = defaults();
        RecapRedirectParse("ssl_bypass=0\n", &c);
        CHECK(c.bSslBypass == 0);
    }
    /* comments, blanks, whitespace, CRLF */
    {
        RecapRedirectConfig c = defaults();
        RecapRedirectParse("# comment\r\n\r\n  host = 127.0.0.1 \r\nhttp_port=8033\r\n", &c);
        CHECK(c.uHostAddr == 0x7F000001u);
        CHECK(c.uHttpPort == 8033u);
    }
    /* unknown key ignored, defaults preserved */
    {
        RecapRedirectConfig c = defaults();
        RecapRedirectParse("bogus=whatever\n", &c);
        CHECK(c.uHostAddr == 0x7F000001u);
        CHECK(c.uHttpPort == 8033u);
        CHECK(c.bSslBypass == 1);
    }
    /* malformed dotted IPv4 leaves host untouched */
    {
        RecapRedirectConfig c = defaults();
        RecapRedirectParse("host=not.an.ip.addr\n", &c);
        CHECK(c.uHostAddr == 0x7F000001u);
    }
    if (!g_fail) printf("ALL PASS\n");
    return g_fail;
}
```

- [ ] **Step 3: Run the test — verify it fails (no implementation yet)**

Run (from `C:\CodingProjects\Personal\eawebkit`):
```
C:\Strawberry\c\bin\gcc.exe -I EAWebKitSupportPackages/DirtySDKEAWebKit/local/core/include -DRECAP_NO_WIN32 EAWebKitSupportPackages/DirtySDKEAWebKit/local/core/source/dirtysock/recapredirect.c tests/recapredirect_test.c -o tests/recapredirect_test.exe
```
Expected: **link error** — `undefined reference to RecapRedirectParse` (recapredirect.c not written yet).

- [ ] **Step 4: Write `recapredirect.c`**

```c
#include "recapredirect.h"
#include <stddef.h>

/* ---- pure parser (no I/O, no Win32) -------------------------------------- */

static int recap_streq_n(const char *a, const char *b, size_t n)
{
    size_t i;
    for (i = 0; i < n; ++i) { if (a[i] != b[i]) return 0; }
    return 1;
}

/* parse "a.b.c.d" -> assembled (a<<24)|(b<<16)|(c<<8)|d. Returns 1 on success. */
static int recap_parse_ipv4(const char *s, unsigned int *pOut)
{
    unsigned int parts[4]; int iPart = 0; unsigned int acc = 0; int seen = 0; const char *p;
    for (p = s; ; ++p)
    {
        if (*p >= '0' && *p <= '9') { acc = acc*10u + (unsigned int)(*p - '0'); seen = 1; if (acc > 255u) return 0; }
        else if (*p == '.' || *p == 0)
        {
            if (!seen || iPart >= 4) return 0;
            parts[iPart++] = acc; acc = 0; seen = 0;
            if (*p == 0) break;
        }
        else return 0;
    }
    if (iPart != 4) return 0;
    *pOut = (parts[0]<<24)|(parts[1]<<16)|(parts[2]<<8)|parts[3];
    return 1;
}

static unsigned int recap_parse_uint(const char *s, int *pOk)
{
    unsigned int v = 0; int seen = 0; const char *p;
    for (p = s; *p; ++p)
    {
        if (*p >= '0' && *p <= '9') { v = v*10u + (unsigned int)(*p - '0'); seen = 1; }
        else break;
    }
    *pOk = seen; return v;
}

/* copy one logical line [pBeg,pEnd) into buf, trimming leading/trailing spaces+tabs */
static void recap_trim_copy(const char *pBeg, const char *pEnd, char *buf, size_t cap)
{
    size_t n;
    while (pBeg < pEnd && (*pBeg == ' ' || *pBeg == '\t')) ++pBeg;
    while (pEnd > pBeg && (pEnd[-1] == ' ' || pEnd[-1] == '\t' || pEnd[-1] == '\r')) --pEnd;
    n = (size_t)(pEnd - pBeg);
    if (n >= cap) n = cap - 1;
    if (n) { size_t i; for (i = 0; i < n; ++i) buf[i] = pBeg[i]; }
    buf[n] = 0;
}

void RecapRedirectParse(const char *pText, RecapRedirectConfig *pCfg)
{
    const char *pLine = pText;
    if (!pText || !pCfg) return;
    while (*pLine)
    {
        const char *pNL = pLine; const char *pEq;
        char key[64]; char val[128];
        while (*pNL && *pNL != '\n') ++pNL;          /* end of this line */
        /* find '=' within [pLine, pNL) */
        for (pEq = pLine; pEq < pNL && *pEq != '='; ++pEq) ;
        if (pEq < pNL && pLine[0] != '#')
        {
            recap_trim_copy(pLine, pEq, key, sizeof(key));
            recap_trim_copy(pEq + 1, pNL, val, sizeof(val));
            if (recap_streq_n(key, "host", 5))
            {
                unsigned int a; if (recap_parse_ipv4(val, &a)) pCfg->uHostAddr = a;
            }
            else if (recap_streq_n(key, "http_port", 10))
            {
                int ok; unsigned int v = recap_parse_uint(val, &ok); if (ok) pCfg->uHttpPort = v;
            }
            else if (recap_streq_n(key, "ssl_bypass", 11))
            {
                int ok; unsigned int v = recap_parse_uint(val, &ok); if (ok) pCfg->bSslBypass = (v != 0);
            }
        }
        if (*pNL == 0) break;
        pLine = pNL + 1;
    }
}

/* ---- Win32 loader (excluded from the gcc parser test via RECAP_NO_WIN32) -- */
#ifndef RECAP_NO_WIN32
#include <windows.h>
#include <stdio.h>

static RecapRedirectConfig g_cfg;
static int g_loaded = 0;

const RecapRedirectConfig *RecapRedirectGet(void)
{
    char path[MAX_PATH]; HMODULE hMod = NULL; DWORD n; FILE *fp;

    if (g_loaded) return &g_cfg;

    /* defaults first — these stand if anything below fails */
    g_cfg.uHostAddr = 0x7F000001u;   /* 127.0.0.1 */
    g_cfg.uHttpPort = 8033u;
    g_cfg.bSslBypass = 1;
    g_loaded = 1;                    /* set early: never retry, never crash-loop */

    if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                            (LPCSTR)(void*)&RecapRedirectGet, &hMod))
        return &g_cfg;
    n = GetModuleFileNameA(hMod, path, sizeof(path));
    if (n == 0 || n >= sizeof(path)) return &g_cfg;
    /* strip filename -> directory */
    while (n > 0 && path[n-1] != '\\' && path[n-1] != '/') --n;
    path[n] = 0;
    if (n + 9 >= sizeof(path)) return &g_cfg; /* "recap.cfg" = 9 chars */
    /* append recap.cfg */
    { const char *f = "recap.cfg"; size_t i; for (i = 0; f[i]; ++i) path[n+i] = f[i]; path[n+i] = 0; }

    fp = fopen(path, "rb");
    if (fp)
    {
        char buf[2048]; size_t got = fread(buf, 1, sizeof(buf) - 1, fp); buf[got] = 0;
        fclose(fp);
        RecapRedirectParse(buf, &g_cfg);
    }
    return &g_cfg;
}
#endif /* RECAP_NO_WIN32 */
```

- [ ] **Step 5: Run the test — verify it passes**

Run (from `C:\CodingProjects\Personal\eawebkit`):
```
C:\Strawberry\c\bin\gcc.exe -I EAWebKitSupportPackages/DirtySDKEAWebKit/local/core/include -DRECAP_NO_WIN32 EAWebKitSupportPackages/DirtySDKEAWebKit/local/core/source/dirtysock/recapredirect.c tests/recapredirect_test.c -o tests/recapredirect_test.exe && tests\recapredirect_test.exe
```
Expected: `ALL PASS` (exit 0).

- [ ] **Step 6: Commit (ReCap repo: record progress note only)**

The eawebkit tree is not a git repo, so there is nothing to `git commit` there. Record the milestone in the ReCap repo at the end (Task 7). No commit this task.

---

## Task 2: DNS redirect — `SocketLookupThread`

**Files:**
- Modify: `…\core\source\dirtysock\pc\dirtynetwin.c` (function `SocketLookupThread`, ~line 798–822)

- [ ] **Step 1: Add the include**

At the top of `dirtynetwin.c`, after the existing DirtySDK includes (search for the last `#include` near the file head), add:
```c
#include "recapredirect.h"
```
(The DirtySDK include dir `…/core/include` is already on the project include path — that's where `dirtynet.h` lives.)

- [ ] **Step 2: Replace the resolution body**

Find (current `SocketLookupThread` body, ~lines 804–814):
```c
    winhost = gethostbyname(host->name);
    if (winhost != NULL)
    {
        ipaddr = winhost->h_addr_list[0];
        host->addr = (ipaddr[0]<<24)|(ipaddr[1]<<16)|(ipaddr[2]<<8)|(ipaddr[3]<<0);
        host->done = 1;
    }
    else
    {
        host->done = -1;
    }
```
Replace with:
```c
    /* ReCap redirect: bypass real DNS; resolve everything to the configured host.
       See docs/superpowers/specs/2026-06-02-eawebkit-redirect-reimpl-design.md */
    host->addr = RecapRedirectGet()->uHostAddr;
    host->done = 1;
    (void)winhost;
    (void)ipaddr;
```
(Leaving the `winhost`/`ipaddr` declarations in place; the `(void)` casts silence unused-variable warnings under `/WX`.)

- [ ] **Step 3: Verify the edit compiles in context (deferred to Task 6 full build)**

No standalone compile here (the file needs the whole DirtySDK include graph + winsock). Verification happens in the Task 6 solution build. Visually confirm: the `#include "recapredirect.h"` is present and the body matches above.

- [ ] **Step 4: Commit** — none (eawebkit tree, see Task 1 Step 6).

---

## Task 3: HTTP port remap — `_MapAddress`

**Files:**
- Modify: `…\core\source\dirtysock\pc\dirtynetwin.c` (function `_MapAddress`, ~line 219–274)

- [ ] **Step 1: Insert the redirect at the top of `_MapAddress`**

Find the function header and its first statements (~line 219–231):
```c
static const struct sockaddr *_MapAddress(struct sockaddr *pTemp, const struct sockaddr *pAddr)
{
    SocketStateT *pState = _Socket_pState;
    const SocketAddrMapT *pMap = pState->aSockAddrMap;
    uint32_t uAddr, uSrcPort, uDstPort;
    const SocketNameMapT *pNameMap;
    char strAddrText[16];
    int32_t iMap;

    uAddr = SockaddrInGetAddr(pAddr);
    uSrcPort = SockaddrInGetPort(pAddr);
```
Immediately AFTER the two `uAddr = … ; uSrcPort = … ;` assignments, insert:
```c
    /* ReCap redirect: force the configured host; remap HTTP (80) to the configured port.
       EAWebKit's DirtySDK only dials the game web backend, so forcing the address is safe;
       Blaze/RakNet are the exe's own sockets and never pass through here. */
    {
        const RecapRedirectConfig *pRecap = RecapRedirectGet();
        memcpy(pTemp, pAddr, sizeof(*pTemp));
        SockaddrInSetAddr(pTemp, pRecap->uHostAddr);
        if (uSrcPort == 80)
        {
            SockaddrInSetPort(pTemp, (int32_t)pRecap->uHttpPort);
        }
        return(pTemp);
    }
```
This returns early, bypassing the legacy name/addr-map loop (which the host never populates anyway). `memcpy`/`SockaddrInSetAddr`/`SockaddrInSetPort` are exactly the calls the legacy path already uses (dirtynetwin.c:266–268), so the sockaddr is well-formed.

- [ ] **Step 2: Confirm no unused-variable fallout**

Because we `return` early, `pMap`, `uDstPort`, `pNameMap`, `strAddrText`, `iMap` become unused → `/WX` would error.
Add, on the line right after the `int32_t iMap;` declaration:
```c
    (void)pMap; (void)uDstPort; (void)pNameMap; (void)strAddrText; (void)iMap;
```
(Place this BEFORE the inserted redirect block so the casts are reached on every call.)

- [ ] **Step 3: Verify** — deferred to Task 6 full build. Visually confirm the early-return block + the `(void)` casts are present.

- [ ] **Step 4: Commit** — none (eawebkit tree).

---

## Task 4: SSL verify bypass — `protossl.c`

**Files:**
- Modify: `…\core\source\proto\protossl.c` (~line 1713, in `_ServerCert`)

- [ ] **Step 1: Add the include**

At the top of `protossl.c`, after the existing includes, add:
```c
#include "recapredirect.h"
```

- [ ] **Step 2: Gate the server-cert check on the config flag**

Find (~line 1713):
```c
    if (!pState->bAllowAnyCert)
    {
        if (_WildcardMatchNoCase(pState->strHost, pSecure->Cert.Subject.strCommon) != 0)
```
Change the outer condition to also honor our bypass:
```c
    if (!pState->bAllowAnyCert && !RecapRedirectGet()->bSslBypass)
    {
        if (_WildcardMatchNoCase(pState->strHost, pSecure->Cert.Subject.strCommon) != 0)
```
When `ssl_bypass=1` (default), the entire host-match + `_VerifyCertificate` block is skipped → ReCap's self-signed cert is accepted. `ssl_bypass=0` restores stock verification.

- [ ] **Step 3: Verify** — deferred to Task 6 full build. Visually confirm the single-line condition change + the include.

- [ ] **Step 4: Commit** — none (eawebkit tree).

---

## Task 5: Add `recapredirect.c` to the DirtySDK project

**Files:**
- Modify: `…\projects\VS2008\DirtySDKEAWebKit\local\dirtysock.vcproj` (~after line 128, the `platformsocketapi.c` `<File>` block, before `<Filter Name="pc">` at line 129)

- [ ] **Step 1: Insert the `<File>` entry**

After the closing `</File>` of `platformsocketapi.c` (line 128) and BEFORE `<Filter Name="pc" Filter="">` (line 129), insert:
```xml
          <File RelativePath="..\..\..\..\EAWebKitSupportPackages\DirtySDKEAWebKit\local\core\source\dirtysock\recapredirect.c">
            <FileConfiguration Name="pc-vc-dev-debug|Win32">
              <Tool Name="VCCLCompilerTool" ObjectFile="pc-vc-dev-debug\build\dirtysock\vcproj\core\source\dirtysock\recapredirect.c.obj" />
            </FileConfiguration>
            <FileConfiguration Name="pc-vc-dev-opt|Win32">
              <Tool Name="VCCLCompilerTool" ObjectFile="pc-vc-dev-opt\build\dirtysock\vcproj\core\source\dirtysock\recapredirect.c.obj" />
            </FileConfiguration>
          </File>
```
(Mirrors the `platformsocketapi.c` entry exactly, with per-config `ObjectFile` paths so the two build configs don't collide.)

- [ ] **Step 2: Verify the XML is well-formed**

Run (from `C:\CodingProjects\Personal\eawebkit`):
```
C:\Strawberry\c\bin\perl.exe -MXML::Simple -e "XMLin('projects/VS2008/DirtySDKEAWebKit/local/dirtysock.vcproj'); print qq{OK\n}"
```
Expected: `OK` (no parse error). If `XML::Simple` is missing, instead open the file and confirm the inserted block is balanced (`<File> … </File>`).

- [ ] **Step 3: Commit** — none (eawebkit tree).

---

## Task 6: Full solution build (integration gate) — USER-RUN

**Files:** none (build only)

- [ ] **Step 1: Build the EAWebKit solution (dev-opt)**

In the VS2008 environment (Visual Studio 2008, or a VS2008 command prompt), build:
```
"%VS90COMNTOOLS%..\IDE\devenv.com" "C:\CodingProjects\Personal\eawebkit\projects\VS2008\EAWebKit\1.21.00.darkspore\EAWebKit.sln" /build "pc-vc-dev-opt|Win32"
```
(Or load the .sln in VS2008 and Build → Batch Build the `pc-vc-dev-opt` config, as was done for the baseline.)

- [ ] **Step 2: Verify the build succeeds**

Expected: `Build: NN succeeded, 0 failed`. Specifically the `dirtysock` and `EAWebkit` projects compile with our new/edited files.
Output refreshed: `C:\CodingProjects\Personal\eawebkit\Distribution\pc\9.0.21022\dev-opt\bin\EAWebkit.dll`.
- If `/WX` flags an unused variable in `dirtynetwin.c`/`_MapAddress`, re-check the `(void)` casts from Tasks 2–3.
- If `recapredirect.h` is not found, confirm `…/core/include` is on the dirtysock include path (it is — `dirtynet.h` resolves from there).

- [ ] **Step 3: Commit** — none (eawebkit tree).

---

## Task 7: Deploy + runtime gate + record milestone

**Files:**
- Create: `recap.cfg` template (next to the DLL when deployed)
- Update (ReCap repo): `memory/eawebkit-redirect-and-ports.md`, `MEMORY.md`

- [ ] **Step 1: Write the `recap.cfg` template**

Create `C:\CodingProjects\Personal\eawebkit\Distribution\pc\9.0.21022\dev-opt\bin\recap.cfg` (and ship a copy next to `Darkspore.exe` at deploy):
```ini
# ReCap EAWebKit redirect — point Darkspore's web layer at a ReCap server.
# host: dotted IPv4 of the server (127.0.0.1 = local). http_port: ReCap REST port.
# ssl_bypass: 1 = accept the server's self-signed cert (default).
host=127.0.0.1
http_port=8033
ssl_bypass=1
```

- [ ] **Step 2: Deploy (USER-RUN)**

1. Back up the working `EAWebKit.dll` next to `Darkspore.exe` (Xackery's DLL) to a safe name.
2. Copy `Distribution\pc\9.0.21022\dev-opt\bin\EAWebkit.dll` → next to `Darkspore.exe` as `EAWebKit.dll`.
3. Copy `recap.cfg` next to `Darkspore.exe`.
4. Start ReCap: `dotnet run --project ReCap.Server` (REST on 8033).

- [ ] **Step 3: Runtime gate (USER-RUN)**

Launch Darkspore.
- **PASS:** the login web UI loads (served from ReCap :8033), no cert rejection; the client reaches the login flow as it does with Xackery's DLL today.
- **Remote check:** set `host=<remote ReCap IP>` in `recap.cfg`, relaunch → connects to the remote server with the same install (validates VitorMM's local-or-remote requirement).
- **FAIL triage:** capture any client exception (`exception.txt`; image base 0x400000). If the web UI never dials, add `NetPrintf` traces in `SocketLookupThread`/`_MapAddress` and check the DirtySDK debug log. If TLS still fails, confirm `ssl_bypass=1` is being read (the file sits next to `EAWebKit.dll`).

- [ ] **Step 4: Record the milestone (ReCap repo)**

Update `memory/eawebkit-redirect-and-ports.md`: note that the redirect is now reimplemented in-source (no Detours), config-driven via `recap.cfg`, with `file:line` anchors (`SocketLookupThread`, `_MapAddress`, `protossl.c:1713`). Add a `MEMORY.md` pointer line if the summary changed. Then commit (ReCap repo):
```bash
git add docs/superpowers memory/ MEMORY.md
git commit -m "docs(eawebkit): redirect reimplemented in-source (config-driven, no Detours)"
```

---

## Self-review notes
- Spec coverage: config loader (T1), DNS force-all (T2), port 80→http_port (T3), SSL bypass gated (T4), vcproj (T5), build gate (T6), recap.cfg + runtime gate + local/remote check (T7). All spec sections mapped.
- Types consistent: `RecapRedirectConfig{uHostAddr,uHttpPort,bSslBypass}` + `RecapRedirectGet()`/`RecapRedirectParse()` used identically across T1–T4.
- No placeholders: every code/edit step shows full content and exact insertion points.
- Open item from spec resolved: dotted-IPv4 only for v1 (parser rejects hostnames, leaving the default) — matches "lean: dotted IPv4 only".
