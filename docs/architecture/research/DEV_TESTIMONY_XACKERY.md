# Xackery testimony + EAWebKit redirection — research notes

Curated from the `darkspore-research-xackery` Discord channel (442 msgs, 2025-08-05 → 2025-08-09) plus our own read of the EAWebKit source at `C:\CodingProjects\Personal\eawebkit`. **Xackery** is an external researcher (not an official ReCap team member, anonymous for legal caution re: an ongoing private-server lawsuit / Stop Killing Games) who rebuilt Darkspore client+server mods from scratch and shared findings.

> **Status:** Xackery's last message (2025-08-09) said he "may stop this research" pending a thumbs-up. Treat as a frozen snapshot. His repos are private; he offered the team free cherry-picking. The pre-built `EAWebKit.dll` he shared is what Jean currently uses to run ReCap.

---

## 🔑 Headline findings

1. **The modern client→server redirect is a recompiled `EAWebKit.dll` with Detours hooks — no launcher, no exe patching.** It is a drop-in replacement: back up the stock `EAWebKit.dll`, drop Xackery's in, double-click `Darkspore.exe`. This supersedes `ReCap.Launcher` (which patched the cert + parts of the executable).
2. **The redirect works by hooking, inside the DLL: SSL cert validation (bypass) + `gethostbyname`/`connect` (force `127.0.0.1`) + per-service port remapping.** This is why our read of the EAWebKit *source* found nothing custom — the source tree is **stock open-source EAWebKit 1.21**; the mod is **Microsoft Detours** code added in Xackery's private build.
3. **We now have the client's native server port map** (see table) — the hardcoded EA endpoints Darkspore dials, and what they must be redirected to.
4. **The Darkspore launcher is a webview** driven by the `getConfigs` bootstrap API; confirms the `Spark`/`UI_WebKit`/`cWebEngine` chain. A JS↔exe bridge (`Client.*`) exists.
5. **Xackery independently built a "headless client"** for backend testing — the same idea behind our `ConsoleClient`/console-test direction.

---

## EAWebKit.dll redirection mechanism

**Build:** EAWebKit is GPL/open-source; Xackery recompiled it (VS2008) and added **Microsoft Detours** (downgraded to 3.0). The `C:\CodingProjects\Personal\eawebkit` tree is the **stock base** he compiles — the Detour/hook logic lives in his private repo, not in that tree. Build target: the `1.21.00.darkspore` project; defines confirm `WTF_USE_DIRTYSDK=1` / `WTF_USE_CURL=0` (networking via EA **DirtySDK** ProtoHttp/ProtoSSL), not curl.

**Hooks installed at startup** (from his verbose log, msg 7):
```
- Attaching hook ssl_ctx_set_verify
- Attaching hook ssl_get_verify_result
- Attaching hook WildcardMatchNoCase
- Attaching hook VerifyCertificate
VerifyCertificate called, returning 0     <- cert validation bypassed
Gethostnamebyname set to: addr=16777343   <- 0x0100007F = 127.0.0.1, forced
Connect called. Address: 127.0.0.1, Port: 80 / 42127 / 10041 / 17502 ...
```
- **Cert bypass:** the four SSL hooks make `VerifyCertificate`/`WildcardMatchNoCase` always succeed, so the client accepts ReCap's self-signed cert. (ReCap server already uses a self-signed cert; this is the client-side counterpart to trusting it.)
- **Host force:** `gethostbyname` is hooked to resolve everything to `127.0.0.1`.
- **Port remap:** `connect` is hooked to rewrite destination ports (table below).
- **Hook resolution:** Detours finds functions by **byte signature**, with a **fallback to a fixed pointer address** when the signature isn't found. Signatures are version-specific (built for **demo 5.3.0.103**, not retail 127). Under **Wine** the signatures didn't match (Wine's code differs) → fallback addresses required.
- Bypasses the game's TOML config loader; values hardcoded.

**Relevant stock EAWebKit hooks we confirmed in source:**
- `EAWebKit.cpp:1498` `LoadSSLCertificate()` → `ProtoSSLSetCACert()` — the supported API to add a CA cert into ProtoSSL (an alternative to the cert-bypass hook).
- `EAWebKitPlatformSocketAPI.h` — the host app (Darkspore.exe) injects the socket functions (`connect`, `gethostbyname`, `dnslookup`, …) into EAWebKit via a `PlatformSocketAPI` struct of function pointers. This is the seam Detours hooks.

---

## Client native port map (and redirect targets)

The ports Darkspore's client dials by default, and Xackery's consolidated remap (msg 437). `ssl` = raw TCP with an SSL handshake; `udp` = raw UDP; `http` = HTTP.

| Service | Native port | Proto | Xackery remap | Notes |
|---|---|---|---|---|
| HTTP API (bootstrap + game) | **80** | http | 53001 | port 80 needs admin → main reason to remap |
| HTTP telemetry | 8080 | http | 53001 | |
| Blaze redirector | **42127** | ssl/tcp | 53000 | |
| Blaze (main/lobby) | **10041** | ssl/tcp | 53000 | |
| Blaze tick | 8999 | ssl/tcp | 53000 | |
| Blaze pss | 8443 | ssl/tcp | 53000 | |
| Blaze telemetry | 9988 | ssl/tcp | 53000 | |
| QoS (ssl) | **17502** | ssl/tcp | 53000 | |
| QoS (udp) | 3659 | udp | 53000 | only UDP service |

**Canonical EA endpoints** (recovered by string-diffing a host-patched exe vs retail — see Dev.exe note below) that the client dials by default:
- `gosredirector.ea.com` (+ `.online.ea.com`, `.scert.ea.com`, `.stest.ea.com`) — **Blaze redirector** ("gos" = Game Online Services; `scert`/`stest` = cert/test environments) on port 42127.
- `config.darkspore.com/bootstrap/api?version=1` — the **bootstrap config** endpoint (HTTP 80).
- `api.darkspore.com`, `content.darkspore.com`, `darkspore.com` — web/content/API.

> Xackery consolidated his server to **two ports** (53000 = all SSL/raw TCP+UDP, 53001 = HTTP). **ReCap uses a different scheme** (42127 redirector, 42125 lobby, 42000 RakNet UDP, 8033 HTTP). The DLL Xackery shared *for ReCap* therefore mainly **redirects HTTP 80 → 8033** (msg 364: "set `SERVER_HTTP_PORT` to 8033 in config.xml"); ReCap already binds 42127 natively so Blaze needs no remap. RakNet gameplay is UDP 42000 (ReCap) vs the client's native 3659 QoS — distinct concern.

---

## SSL / cipher story

- Darkspore's native SSL (DirtySDK ProtoSSL) uses a **very old, insecure cipher** that modern stacks (e.g. Go) have removed. Xackery's plan: drive the client to **plaintext**, then upgrade to a modern cipher (or ship our own) later.
- Finding the exact SSL-negotiation function is hard amid DirtySDK noise; `RecvWithBufferFallback` is too low-level to cleanly intercept.
- **Debug trick:** a `plaintext.txt` sentinel file — if present, the hook sends packets in plaintext (easy to sniff); delete it to use normal SSL. Planned to move this to a `-rc-enabled` / `-loglevel` command-line flag.

---

## Launcher = webview (bootstrap chain)

Confirms our `Spark`/`UI_WebKit` findings. The Darkspore "launcher" is an embedded webview:
- Bootstrap flow observed: `GET /bootstrap/api?version=1&method=api.config.getConfigs&build=5.3.0.103&include_patches=true&include_settings=true`, then `GET /bootstrap/launcher?version=…`, then `GET /?version=…`, then the **game/api** (many methods).
- `getConfigs` can point the launcher at a **remote UI** instead of the in-package HTML. ReCap exploits this: `res/data/www/static/bootstrap/launcher/` has `index.html` (ReCap's custom page, the single-player button) + `wrapper.html` (near-original, hidden, iframes index.html).
- **JS↔exe bridge:** the page calls `Client.minCurrentApp()`, `Client.playCurrentApp()`, etc. To expose new native functions to the launcher UI, extend that `Client` instance. (VitorMM: the page↔app relationship is fragile — small edits to the original page break everything.)

**Terminology (settle this):**
- **bootstrap launcher** = the HTML endpoint recap_server serves at game start.
- **recap_launcher** = the standalone exe that hooks Darkspore + boots recap_server. "Launcher" unqualified usually means this one.

---

## Headless client + tooling (relevant to our console work)

- Xackery has a **headless client** system (and offered to transplant it) to test the backend without launching the game — the same goal as our `ConsoleClient`/console-test exploration ([[console-system-telnet-server]]).
- He has tooling to **emulate the client/server exchange, do playbacks, and run integration tests**, and a third-party packet sniffer to green-field payloads.
- Cross-link: the Detours-in-DLL technique is also the realistic lever to **enable the gated in-client `ConsoleServer`** we found ([[console-system-telnet-server]]) — same in-process foothold.

---

## Context on Xackery's own server (not ReCap code)

- Written in **Go** (faster for him to iterate), monolithic for now, mirrors `darkspore_server`'s structure. Renamed to avoid EA terms for legal caution: RakNet→`rn`, SporeNet→`sn`, Blaze→`command` ("it's really a command server"). Considered gRPC between services for future horizontal scaling.
- **Multiplayer-first** focus (lobby/chat/rooms = his "hello world"); single-player "can be lit up easily after."
- Replaced node-based XML builder with **struct bindings + a custom XML encoder** (payloads are mostly static, not dynamic).
- Dev setup: remote beefy Linux box (`g16`, 64 GB) running a **dev container**, SSH + port-forward to local; game runs locally via **Wine 6.0.3 on Mint** (no winetricks needed); clean `recap_server` build ≈ 54 s.

---

## Misc leads worth chasing

- **`Darkspore - Dev.exe` is NOT a dev build — RESOLVED 2026-05-27 by string-diff.** It is retail `Darkspore.exe` with the **EA hostnames hex-patched to `127.0.0.1`** (the old static-redirect method, predating the launcher/EAWebKit-DLL approaches). String diff vs local retail: only **1 dev-only** string (`http://127.0.0.1/bootstrap/api?version=1`) and **8 ret-only** (the EA endpoints above). All console markers (`ConsoleServer.cpp`, `TelnetTransport`, `AppConsole`, `Spark HTTP Server`, `SP_App/CheatManager`, `TCPInterface.cpp`) are present in **both** and identical → the console is still compiled-but-orphaned, same as retail. **This exe does not help enable the console.** (It did hand us the canonical EA endpoint list above.)
- **Skip intro movies** without a mod: property `playOpeningMovie` → `boolProp playOpeningMovie false` in `ConfigManager.txt` (`Properties.txt`: `property playOpeningMovie (hash(playOpeningMovie)) bool`).
- **Goliath Alpha** won't unlock — ancient unsolved bug (resurrection-capsule issue #8).
- Xackery saw `kGms` commands in IDA strings (abilities use `kGms`); has a "127 idb" (IDA database).

---

## Provenance & caution

- Discord testimony + our static read of the EAWebKit base tree. Xackery's actual hook code is **private** and not in the local `eawebkit` tree (which is stock EAWebKit 1.21). The port map and mechanism are from his logs/messages, not from disassembling his DLL.
- He is anonymous by choice; do not attribute publicly. Repos private; team may cherry-pick if shared.
- Slim transcript generated at `%TEMP%\xackery_slim.txt` (not committed).
