# Client Network Architecture (nSporeNet / GMS)

Reverse-engineered from the retail `Darkspore.exe` loaded in Ghidra (see
[command matrix](COMMAND_MATRIX.md) for the per-message table). This documents how the
**client** talks to the gameplay server (GMS = Game Management Server, our RakNet adapter
on port 42000), so we can verify our packets and port the gaps.

## Stack

```
cClientSession  (nSporeNet)               <- snclient_session.cpp
   ├─ transport (RakNet)                   <- nSporeNet::cTransport*
   ├─ 4 event callbacks   tTransportEventCallback<cClientSession>
   │     OnConnected / OnConnectFailed / OnConnectionLost / OnConnectionClosed
   ├─ N message handlers  tTransportMessageFunctor<cClientSession>
   │     OnGms<Name>  (one per kGms message)
   └─ session state machine
         kSessionStateIdle → Connecting → Connected → Authenticated
```

## GMS message identity — the key mapping

The client registers handlers keyed by the **kGms enum index (0-based)**, and the wire
byte is **`0x7F + index`**:

| kGms index | kGms name | wire byte | our PacketType |
|-----------:|-----------|:---------:|----------------|
| 0 | kGmsHelloReq | 0x7F | HelloPlayerRequest |
| 1 | kGmsHelloPlayer | 0x80 | HelloPlayer |
| 3 | kGmsConnected | 0x82 | Connected |
| 5 | kGmsPlayerJoined | 0x84 | PlayerJoined |
| 7 | kGmsPlayerDeparted | 0x86 | PlayerDeparted |
| … | … | 0x7F+idx | … (full list: COMMAND_MATRIX.md) |

- **kGms name table**: `0x01036410+` (in index order, `kGmsHelloReq` … `kGmsDebugPing`).
- This confirms our `PacketType` enum (0x7F–0xCC) matches the client 1:1.

## Session bring-up — `cClientSession::ConnectAndRegisterMessages` (0x00a92ef0)

1. Connect to GMS `[addr][port]`.
2. Register the 4 transport event callbacks (stored at `this[0xF..0x12]`).
3. Register message handlers via the message-manager singleton (`FUN_00a92e20`, vtable+0x2C),
   one `tTransportMessageFunctor<cClientSession>` per handled message index.
   - This session (lobby/connect phase) registers idx 1, 3, 5, 7.
   - Gameplay-phase messages (ObjectCreate, LabsPlayerUpdate, ActionCommandResponse, …) are
     registered by the in-game session — **not yet located** (next target).

## Mapped functions (named in Ghidra, namespace `nSporeNet::cClientSession`)

| Address | Name | Evidence |
|---------|------|----------|
| 0x00a92ef0 | ConnectAndRegisterMessages | "initializing and registering messages" |
| 0x00a936d0 | Close | "Closing client session" |
| 0x00a942d0 | SetSessionState | "state %s => %s" + kSessionState* names |
| 0x00a944a0 | OnConnected | "OnConnected [peer=%s]" |
| 0x00a94570 | OnConnectFailed | "OnConnectFailed [peer=%s]" |
| 0x00a94700 | OnConnectionLost | "OnConnectionLost [peer=%s]" |
| 0x00a94890 | OnConnectionClosed | "OnConnectionClosed [peer=%s]" |
| 0x00a93b50 | OnGmsConnected | "OnGmsConnected [peer=%s]" |
| 0x00a93d50 | OnGmsHelloPlayer | "OnGmsHelloPlayer [type=%d][gameplayIndex=%u]" |
| 0x00a93f50 | OnGmsPlayerJoined | "OnGmsPlayerJoined [gameplayIndex=%u]" |
| 0x00a94040 | OnGmsPlayerDeparted | "OnGmsPlayerDeparted [gameplayIndex=%u]" |

## RE method (Rosetta Stone)

Mirrors the Lua/AssetData/Simulation approach:
1. **kGms string table** (0x01036410+) gives the message names in index order.
2. **`OnGms*` debug strings** (each handler logs its own name) directly name the handler funcs.
3. **The registrar** (`ConnectAndRegisterMessages` + the in-game equivalent) maps index→handler.
4. Rename `FUN_*` → `nSporeNet::cClientSession::OnGms<Name>`; plate-comment the registrar.

## ActionCommandMsgs (0x9C) — client→server input (RESOLVED)

Verified vs C++ `OnActionCommandMsgs` (Server.cpp:688) and a live client packet.
Ghidra Data Types created: `ActionCommandCommonData` (40B), `ActionCommandMovementData` (24B).

`ActionCommandCommonData` (40 bytes, raw LE, `#pragma pack(1)`):

| off | field | type |
|----:|-------|------|
| 0x00 | type | u8 (ActionCommand enum) |
| 0x01 | unk | u8×3 |
| 0x04 | inputSyncStamp | u32 |
| 0x08 | objectId | u32 |
| 0x0C | position | vec3 (f32×3) |
| 0x18 | orientation | quat (f32×4) |

Command-specific follows. `ActionCommandMovementData` (24B, Movement=3 / Stop=4):
`u32 unk, vec3 goalPosition, u32 goalFlags, u32 unk2`. SwitchCharacter=5: `u32 slotIndex`.

ActionCommand enum: Movement=3, StopMovement=4, SwitchCharacter=5, UseCharacterAbility=7,
UseSquadAbility=8, CatalystPickup=9, Cancel=10, UseInteractableObject=11, Dance=12, Taunt=13.

Server reply to Movement: broadcast `ObjectPlayerMove (0x91)` (LE wire per our convention).
goalFlags bit 0x020 = teleport → `ObjectTeleport (0x90)` instead.

> Our previous `ReadFrom` read objectId first (misaligned, objectId came out as 0x3F000003).
> Fixed in `ActionCommandMsgsPacket` + `Game.HandleActionCommand` now replies with the move.

## Next targets

- Locate the **in-game session** that registers gameplay message handlers (ObjectCreate,
  LabsPlayerUpdate, ActionCommandResponse, ModifierCreated, …). The lobby `cClientSession`
  registers only idx 1/3/5/7 via the msg-manager singleton (FUN_00a92e20); gameplay handlers
  use a different path. The **message registry table is at data `0x0118b488`** (references the
  kGms name array base 0x01036410) — start there.
- Reverse the message (de)serializers to verify our `WriteTo`/reflection byte-for-byte
  (feeds the catalog-driven packet serializer plan).
