# Developer Testimony — foehammer (David Lee Swenson)

Distilled from a Discord conversation (server `Resurrection Capsule`, `#darkspore-discussion`, **2025-08-03 → 2025-08-06**, 599 messages, 243 from foehammer). foehammer = **David Lee Swenson (DLS)**, **lead engineer on Darkspore** (rendering engine + content pipeline), also worked on Spore, Dawngate, SimCity, and others.

> **Provenance & caveats.** This is primary-source dev testimony, volunteered publicly. He did **not** post game code (server rule), but freely described architecture, file formats, and struct layouts from memory + while looking at his personal copy of the source. Memory is imperfect ("several dozen codebases in my noggin… sometimes I channel the wrong one") — he repeatedly corrected himself (Darkspore vs Dawngate). Treat struct layouts as **high-confidence-but-verify**. `#N` cites the message index in `foehammer_transcript.md` (compact extract of the JSON dump).

---

## 1. People & roles (original Darkspore team)

| Person | Role | Reachable? | Knows |
|---|---|---|---|
| **foehammer / David Lee Swenson (DLS)** | **Lead engineer** — rendering engine + content/asset pipeline | spoke 2025-08 (since gone — "the dalkon effect") | client, assets, rendering, build |
| **Trevor** | **Network lead** (RakNet + Blaze) | no — "tired of the gaming biz, went off to ride dirt bikes" | networking/protocol |
| **Lauren** | **Gameplay lead** (also Dawngate; helped Dawngate RE folks) | foehammer *could* point her here (never confirmed) | **the server / gameplay** |
| **Alec, Ryan** | Rendering engine (Ryan = runtime, foehammer = pipeline) | no | rendering |

> A robot in Darkspore bears foehammer's initials (**DLS**) — coincidentally the project's original working title abbreviation (#212–213).

---

## 2. Engine lineage (#114, #121, #346)

**Spore → Darkspore → Dawngate → SimCity** (rendering engine ported forward). Same codebase reused for ~5–6 years; pre-Unreal/Unity era. Consequences relevant to RE:
- **RenderWare at the very bottom** — the final `DrawIndexedPrimitive` is a RenderWare call, used only for the unified vertex buffer (#346).
- **VFX system + web-based debug UIs come from Spore** (#346).
- Lots of "leftovers" from Spore in both Darkspore and Dawngate — unused assets/UI are common (#342–348).
- Darkspore was built on Spore; **`.noun`/property system, FNV hashing, UTFWin all originate in Spore.**

---

## 3. UI architecture — answers the "3 UI stacks" question (#19, #23, #66–79, #251, #271)

There were **four** UI systems across the game's life (matches the `UI_*` folders in the source tree):

| UI | Origin | Status in shipped Darkspore |
|---|---|---|
| EA internal UI | EA-written | **ripped out** early |
| **UTFWin** | from Spore | **still used by the hero/creature editor UI** — the Scaleform migration was never completed (#66) |
| **Scaleform** (GFx) | added by foehammer | **most of the UI**; likely top-level draw order (#271) |
| **WebKit** | — | the **TOSS** (Terms of Service) and possibly server-talking/web UI (#19, #271) |

- The **UI team was laid off mid-development** → rushed the Scaleform switch (#74, #251). "I have 3 UI systems Bob!" (#256).
- foehammer put Scaleform in but didn't work on the UI afterward; unsure of exact draw order (#68, #271).

> Resolves `ARCHITECTURE_OPEN_QUESTIONS.md` Q16. `GFx*` namespaces in Ghidra = Scaleform = the main UI, confirming this.

---

## 4. Server & networking (#199, #206, #222, #267, #277–283; switches #587)

- **Networking = RakNet + EA Blaze**, both **not open-source at the time** — the reason Darkspore/Dawngate couldn't be open-sourced (#199, #206). Trevor (gone) was the network lead.
- **A local command-line server existed** — "we used to have a server that ran locally off a command line… not sure if that was added in Darkspore or Dawngate… the crappy thing is we probably **compiled that out**" (#267). Anti-cheat priority over preservation in the F2P era.
- Command-line switches confirm server/headless modes exist in the client: **`noServer`**, **`headless`**, **`noMP`**, **`netTest`**, `LogNetworkStats`, `telemetryHTTPLog`, `pollinatorHTTPLog`, `pollinLogin`, `launcherURL`, `launcherAction` (#587).
- **Darkspore was client-server but NOT authoritative** / weak anti-cheat; Dawngate was much safer. Darkspore's **patching system was "crap"** (foehammer wrote it) and didn't aggressively enforce package integrity — *which is why adding new packages/mods works* (#277–284).

> Relevant to ReCap: `noServer`/`headless` confirm the client can run without EA's servers; the original local CLI server is the conceptual ancestor of ReCap's server + dalkon's `ConsoleClient`/`ServerWin32`.

---

## 5. Asset & property system — KEY confirmation (#406–409, #408)

- **`.noun` files (`AssetData_Binary`) are the "property system"** — explicitly **distinct** from the model (`bmdl`) MemoryImage/fixup system (#408).
- jean asked if the bmdl fixup/pointer-offset operation applies to `key` types inside `.noun`; foehammer: **"No, I believe the .noun files were part of the property system. That's different."** (#406, #408).

> This directly confirms our Ghidra map: the AssetData reflection runtime (`AssetType`/`AssetData`/`DeserializeObject`, header + blob with indicators) is a **separate system** from the model loader's pointer-fixup MemoryImage. The `AssetData.Parser` design (no fixups; header offsets + sequential blob) is the correct model for `.noun`. The property system originates in Spore.

---

## 6. FNV-1a hashing — confirmed by the original dev (#446, #457, #542)

```c
#define offset_basis 0x811C9DC5
#define fnv_prime    0x1000193
```
- **Case-insensitive, lowercased** before hashing (#446, #457). Names carry a 32-bit FNV-1a hash alongside them everywhere (model names, material NVPairs, bone names).

> Exactly what `AssetData.Parser` (`DbpfReader.FnvHash`) and our Ghidra `AssetType::FNV1a_Hash` use. **Dev-confirmed.** (Consolidate the 3 C# FNV copies — see AssetData.Parser redesign.)

---

## 7. Model format `bmdl` / `bskl` / `banm` (#382–534)

Not the `.noun` path, but documented in detail for the modeling/RE side. Same loader for all three (model / skeleton / animation), separate files.

**MemoryImageHeader** (all `uint32_t`):
```
version            // = 1
fileType           // 4cc: 'bmdl' | 'bskl' | 'banm'
fileVersion        // Darkspore: 2 (#397); mesh struct = TModelDataMeshv4; v5 (Dawngate) adds blend targets
rootObjectOffset   // added to data start → offset to data
objectGraphSize    // size of fixups region in bytes
alignment          // ~unused; intended 16-byte
isFixed            // 0 on disk, set to 1 after fixup
numFixups          // entries in fixup table
Fixups[1]          // extensible; each = offset from image start, monotonically increasing
```
Load: read header → check version/type/fileVersion → seek `rootObjectOffset` → alloc `numFixups*4 + objectGraphSize` → read graph → apply fixups (pointer relocation; conditional 32/64-bit) (#413–435).

**Top-level `TBMDL`:** `TModelDataModel`, `TModelDataSkeleton`, `int32 numAnimations`, `TModelDataAnimation` (#438–442).
**Structures** (pointers are 4 bytes on disk; `bbox[8]` = center.xyz, radius, extents.xyz, radius²):
- `TModelDataModel`: bbox[8], name, hash(fnv), numMaterials, materials, numMeshes, mesh(es), numInstances, inst, audio_collision? (#443–455).
- `TModelDataMeshv4`: bbox[8], name, hash, flags, pitch(/4), vertexDescriptor, vb, ib, sizevb, sizeib (#463).
- Vertex element: stream(u16), offset(u16), type(u8), method(u8 unused), usage(u8), index(u8); list terminated by `stream == 0xff` (#465, #546). Usage: Position=0,Normal=1,Tangent=2,Binormal=3,Texcoord=4,Color=5,BlendWeights=6,BlendIndices=7. Type: Float1-4=0-3, UByte4=5, Short2=6, Short4=7, UByte4N=8, half2=15, half4=16 (#530–533).
- `TModelDataMaterial`: name,nameHash,inst,instHash,flags,numParamDesc,paramDesc,numParamData,paramData,numTextures,texture,numStreams,stream (#540).
- `TModelDataNVPair`: name,nameHash,value,valueHash (all 4-byte) (#457). `TModelDataMaterialParam`: name,nameHash,offset,size (#458).
- `TModelDataInst`: bbox[8], imesh(index), numRenderables, renderable (#467–470).
- **DS `TModelDataRenderable` (differs from Dawngate!):** bbox[8], imat(int32), start, count — **no bone_count / bone_palette** in Darkspore (#520–524).
- `TModelDataSkeleton`: numBones, numSkinnedBones, bones (#474).
- **DS `TModelDataBone`:** name, hash, parentIndex(-1=root), pad, `bindPose` matrix only (DS lacks the full invBindPose/bindRotation/translation/scale set Dawngate has → ~80 bytes/bone in DS vs 128) (#487–488, #510–511).
- `TModelDataAnimation`: index, channel_type (Morph=0,Translation=1,Rotation=2,Scale=3), num_key_frames, float* times (ascending), void* key_frames (1/3/4 floats per frame for morph/scale-translation/rotation) (#500–509). **DS has no note tracks** (Dawngate does) (#527).
- Audio collision = triangle tree, depth-first pre-order; likely unused in DS (#473, #512–517).
- **Compression: Snappy** (#365). Matrices **row-major**, **left-handed (DirectX9)**, likely Y-up, quaternions **(x,y,z,w)** (#492–495, #541).

---

## 8. Locale system (#585–591)

- Dev builds: `-locale:en-us` / `-l` switch. **Removed in Release** ("only installing one locale for movies/audio") (#585).
- **Release reads registry `HKxx\Software\Electronic Arts\Darkspore\Locale`** — string like `en-us`, `sv-sc` (#590).
- `Data\Locale` folders: `de-de`, `en-us`, `fr-fr`, `pl-pl`, `ru-ru` (#580). Locale code comes from Spore (#591).

---

## 9. Command-line switches (literal list, #587)

Self-described "DS switches" list (literals in the binary). High-value subset for ReCap:

- **Server/MP:** `noServer`, `headless`, `noMP`, `netTest`, `LogNetworkStats`, `telemetryHTTPLog`, `pollinatorHTTPLog`, `pollinLogin`, `notelemetry`
- **Launcher:** `launcherURL`, `launcherAction`, `launcherTestError`, `nolauncher`, `elevate`, `upgrading`, `patch`, `testPatch`
- **Data/assets:** `noAssets`, `assets`, `compileData`, `builder`, `preload`, `dataDir`, `userDataDir`, `dataBaseDir`, `dataOverlayDir`, `writeableData`, `ddfList`, `packedUI`, `packGraphicsCache`
- **Scripting:** `runlua`, `runluaquit`, `noScripts`, `noMaterialScripts`, `noEffectScripts`, `noDevEffects`
- **Dev/test:** `dev`/`noDev`, `automation`, `runtest`, `runtestandquit`, `testlog_min_output`, `loggingUILayouts`, `multipleInstances`, `state`, `demo`/`noDemo`
- **Audio:** `noSound`, `noaudiothread`, `nosamplecache`, `verboseaudio`, `mono`/`stereo`/`4.0`/`5.1`, `nodefaultvox`
- **Graphics:** `shaderClamp`, `minShaders`, `card`, `safe`, `vSync`/`noVSync`, `dumpShaders`, `pix/pixe/pixm`, `dumpAsm`
- **Locale:** `locale`, `l`, `spore`

> Full list in transcript #587. `noServer` + `headless` + `runlua` are the most ReCap-relevant.

---

## 10. EA open source (#594–599)

EA satisfies LGPL by publishing some libs at **https://gpl.ea.com/** ("woefully under-advertised"). Includes **EAText / EAWebKit** etc. — useful reference for the WebKit UI + text/locale layers.

---

## 11. What this testimony answers / confirms

| Topic | Status |
|---|---|
| FNV-1a constants (0x811C9DC5 / 0x1000193, case-insensitive) | ✅ **dev-confirmed** (was inferred) |
| `.noun` = property system, separate from model fixup system | ✅ **dev-confirmed** — validates AssetData reflection design |
| UI stacks (UTFWin/Scaleform/WebKit + ripped EA UI) | ✅ **answered** (Q16) |
| Local command-line server existed (likely compiled out); `noServer`/`headless` switches | ✅ **confirmed it existed** |
| Networking = RakNet + Blaze, not open-source | ✅ confirmed (why no open-source) |
| Engine lineage Spore→Darkspore→Dawngate→SimCity | ✅ |
| Model format (bmdl/bskl/banm) full layout | ✅ documented (verify vs files) |
| Locale via registry in Release | ✅ |

> **Note:** foehammer is now unreachable ("the dalkon effect"). This transcript + the source-tree screenshot ([`ORIGINAL_SOURCE_TREE.md`](ORIGINAL_SOURCE_TREE.md)) are the last primary-source inputs we will get. The compact transcript lives at `Documents/foehammer_transcript.md` (extracted from the 644 KB JSON dump).
