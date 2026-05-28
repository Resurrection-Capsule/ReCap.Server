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

## Next targets

- Locate the **in-game session** that registers gameplay message handlers (ObjectCreate,
  LabsPlayerUpdate, ActionCommandResponse, ModifierCreated, …) — same registrar pattern.
- Reverse the **ActionCommandMsgs (0x9C) serialize** on the client (send side) to fix our
  misaligned parse + movement reply (task #11): need exact `ActionCommandCommonData` layout.
- Reverse the message (de)serializers to verify our `WriteTo`/reflection byte-for-byte
  (feeds the catalog-driven packet serializer plan).
