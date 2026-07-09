# 0xCC — DebugPing

| Direction | Size | Phase | Status |
|---|---|---|---|
| both | 9 B | all phases | ⚠️ C# empty body |

Bidirectional keepalive / state-transition trigger. Used heavily throughout the gameplay flow — the client pings periodically (and after specific state changes), and the server pings the client at scripted points (e.g. after `GameStart`).

Per state, server-side `OnDebugPing` (`Server.cpp:1134-1206`) reacts differently:

| Client state | C++ response |
|---|---|
| `Spaceship` | flip `gameStateData.state = ChainVoting` (no packet sent) |
| `ChainVoting` | `SendChainVoteMessages(client, 0)` — the 0x151 buffer |
| `ChainCashOut` | `SendChainCashOutMessages(client, 1)` — the 0x2C8 cashout blob |
| `PreDungeon` | (commented out — historically would send LPU/Arena/Reconnect) |
| `Dungeon` | `SendDirectorState`, `SendQuickGame`, `OnPlayerStart`, `SwapCharacter(1)` |

> ⚠️ DebugPing is the **load-bearing state-transition trigger**. It is not just a keepalive. Misimplement it and entire phases stop progressing.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `time` | u64 | **BE** | Server: Unix seconds via `utils::get_unix_time()`. Client: ❓ probably also Unix seconds but unverified. |

Total: 1 byte opcode + 8 byte body = **9 bytes**.

---

## C++ reader (server side)

`ReCap.Cpp/darkspore_server/source/RakNet/Server.cpp:1134-1206`:

```cpp
void Server::OnDebugPing(const ClientPtr& client) {
    const auto& player = client->GetPlayer();
    if (!player) return;

    uint64_t time;
    Read<uint64_t>(mInStream, time);                             // BE wrapper

#ifdef _WIN32
    std::string timeString(0x20, '\0');
    if (const auto* timePtr = _gmtime64(reinterpret_cast<const __time64_t*>(&time))) {
        strftime(&timeString[0], 0x20, "%H:%M:%S", timePtr);
    } else {
        timeString = std::to_string(time);
    }
    std::cout << "-- DebugPing --" << std::endl;
    std::cout << timeString << ", 0x" << std::hex << time << std::dec << std::endl;
#endif

    switch (client->GetGameState()) {
        case GameState::Spaceship:    { /* flip to ChainVoting */ break; }
        case GameState::ChainVoting:  { SendChainVoteMessages(client, 0); break; }
        case GameState::ChainCashOut: { SendChainCashOutMessages(client, 1); break; }
        case GameState::PreDungeon:   { /* commented out */ break; }
        case GameState::Dungeon: { /* DirectorState + QuickGame + OnPlayerStart + SwapCharacter */ break; }
    }
}
```

## C++ writer (server side)

`Server.cpp:2425-2433`:

```cpp
void Server::SendDebugPing(const ClientPtr& client) {
    // 100%
    BitStream outStream(8);
    outStream.Write(PacketID::DebugPing);
    Write<uint64_t>(outStream, utils::get_unix_time());          // BE wrapper
    Send(outStream, client);
}
```

Comment "100%" = "verified, don't touch."

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/DebugPingPacket.cs`:

```csharp
public class DebugPingPacket : IRakNetPacket
{
    public PacketType Type => PacketType.DebugPing;
    public void ReadFrom(Stream stream) { }                      // ⚠️ time discarded
    public void WriteTo(Stream stream) { }                       // ⚠️ no body
}
```

**Both directions write 0 bytes of body.** That means:

- Server → Client: a 1-byte packet (opcode only) instead of the expected 9 bytes. If the client checks length, it rejects.
- Client → Server: time field is ignored. Harmless functionally (server-side `time` is only logged) but the parse path of `BinaryReader.ReadUInt64()` on an empty stream would throw if anyone wired it up.

Dispatched by `Game.HandlePacket` switch case → `HandleDebugPing` (`Game.cs:264-287`):

```csharp
private void HandleDebugPing(RakNetClient sender)
{
    switch (State)
    {
        case GameState.Initializing: State = GameState.ChainVoting; break;
        case GameState.ChainVoting:  sender.SendPacket(new ChainVoteMsgsPacket { Value = 0, ChainData = Chain }); break;
        case GameState.PreDungeon:   break;
        case GameState.Dungeon:      /* DirectorState + QuickGame + OnPlayerStart, no SwapCharacter */ break;
    }
}
```

Missing arms vs C++: `ChainCashOut`.

---

## Open audit items

1. **Add the 8-byte `Timestamp` body** to `DebugPingPacket.WriteTo` / `ReadFrom`. The empty-body form might survive on the wire (client may accept variable length) but it diverges from C++ and trips any length validator on the client.
2. **Add the `ChainCashOut` arm** to `Game.HandleDebugPing` — see [Phase 11](../../flow/phases/11-chaincashout.md) audit item #5.
3. **Confirm server-sent body is consumed.** The client may use the `time` for clock sync; if not, the field is purely log padding.
4. **Cadence audit.** How often does the official client send DebugPing during gameplay? Capture and quantify; if it's per-tick the server-side log is going to be noisy.

---

## Related

- [Phase 06 Spaceship](../../flow/phases/06-spaceship.md) — implicit DebugPing after PartyMergeComplete
- [Phase 07 ChainVoting](../../flow/phases/07-chainvote.md) — DebugPing → ChainVoteMsgs(0)
- [Phase 09 Dungeon](../../flow/phases/09-dungeon.md) — DebugPing → big setup burst
- [Phase 11 ChainCashOut](../../flow/phases/11-chaincashout.md) — DebugPing → ChainCashOutMsgs(1)
- [0xA9 ChainVoteMsgs](0xA9-chainvotemsgs.md) / [0xAB ChainCashOutMsgs](0xAB-chaincashoutmsgs.md) — typical responses
