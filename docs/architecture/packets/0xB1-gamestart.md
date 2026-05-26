# 0xB1 — GameStart

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 5 B | [09 Dungeon](../phases/09-dungeon.md) | ⚠️ |

Server signals the client to enter active gameplay. Sent after `PlayerStatusUpdate(status=8)` completes the loading sequence. C++ enforces a state-machine guard (`PreDungeon → Dungeon`); C# does not.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `levelIndex` | u32 | **BE** | `chainData.GetLevelIndex()` on C++ side. |

Total: 1 byte opcode + 4 byte body = **5 bytes**.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:2071-2087`:

```cpp
void Server::SendGameStart(const ClientPtr& client) {
    if (client->GetGameState() != GameState::PreDungeon || !client->SetGameState(GameState::Dungeon)) {
        std::cout << "Could not start game, wrong state." << std::endl;
        return;
    }

    const auto& chainData = mGame.GetChainData();

    BitStream outStream(8);
    outStream.Write(PacketID::GameStart);
    Write<uint32_t>(outStream, chainData.GetLevelIndex());        // BE wrapper

    Send(outStream, client);
}
```

`SetGameState(Dungeon)` validates the transition via `IsValidStateChange` (`Client.cpp:38-40`: `PreDungeon → Dungeon` is legal). If the player is not in `PreDungeon` the packet is refused.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/GameStartPacket.cs`:

```csharp
public byte Unk1 { get; set; }

public GameStartPacket(byte unk1) => Unk1 = unk1;
public GameStartPacket() { }

public void WriteTo(Stream stream)
{
    using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
    writer.Write(Unk1);                                          // ⚠️ u8 only
}
```

**Diverges from C++.** Body is a single byte (`u8 Unk1`) instead of a u32 BE `levelIndex`. Called from `Game.cs:351` as `new GameStartPacket(0)` — sends a single zero byte instead of the 4-byte level index.

---

## Open audit items

1. **Fix the body type.** Change `Unk1` (u8) to `LevelIndex` (u32 BE). Use `writer.WriteBE(LevelIndex)`.
2. **Pass the real level index.** Currently hardcoded `0` in `Game.HandlePlayerStatusUpdate` (`Game.cs:351`). Replace with `Chain.LevelIndex`.
3. **Add the state guard.** C++ refuses to send if not in `PreDungeon`. C# accepts blindly. Mirror the guard once [STATE_MACHINE.md](../STATE_MACHINE.md) work lands.
4. **Verify packet length match.** Client may reject a 2-byte packet (opcode + 1 byte) when expecting 5 bytes. This is plausibly part of the Phase 08 stall.

---

## Related

- [Phase 09 Dungeon](../phases/09-dungeon.md) — call site + flow
- [0x88 PlayerStatusUpdate](0x88-playerstatusupdate.md) — `status=8` triggers this
- [0xCC DebugPing](0xCC-debugping.md) — sent immediately after `GameStart`
- [STATE_MACHINE.md](../STATE_MACHINE.md) — `PreDungeon → Dungeon` rule
