# 0xA7 — PlayerCharacterDeploy

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 10 B | [09 Dungeon](../../flow/phases/09-dungeon.md), [10 Gameloop](../../flow/phases/10-gameloop.md) | ⚠️ |

Tells the client "player N just deployed creature M as objectId X." Used both during the Dungeon-entry burst (initial hero spawn) and at runtime when the player swaps to a different creature in the deck.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `playerId` | u8 | — | Session-local player index (`client->GetId()`). |
| `0x01` | `creatureIndex` | u32 | **BE** | Deck slot (0–2). |
| `0x05` | `objectId` | u32 | **BE** | The character object in the world. |

Total: 1 byte opcode + 9 byte body = **10 bytes**. C++ comment says "Packet size: 0x09" (body only).

> ⚠️ Earlier docs miscited this as "5 B" — that figure ignored the `creatureIndex` field. Master index has been updated.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1421-1440`:

```cpp
void Server::SendPlayerCharacterDeploy(const ClientPtr& client, const Game::PlayerPtr& player, uint32_t creatureIndex) {
    if (!player) return;

    const auto& characterObject = player->GetCharacterObject(creatureIndex);
    if (!characterObject) return;

    // Packet size: 0x09
    BitStream outStream(8);
    outStream.Write(PacketID::PlayerCharacterDeploy);

    Write<uint8_t>(outStream, player->GetId());
    Write<uint32_t>(outStream, creatureIndex);
    Write<uint32_t>(outStream, characterObject->GetId());

    Send(outStream, client);
}
```

Called from `Instance::SwapCharacter` (`Instance.cpp:533+`) after setting the current creature and emitting a `LabsPlayerUpdate`.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/PlayerCharacterDeployPacket.cs` — file exists. Sent inline at the end of `Game.OnPlayerStart` (`Game.cs:435`) for the initial hero deploy.

Verify the C# writer emits `(byte, uint32 BE, uint32 BE)` in this exact order — match required.

---

## Open audit items

1. **C# always sends `creatureIndex = Slot`** rather than the deck position. Confirm semantics: is `Slot` the player session id, or the deck slot? In C++ they're separate fields (`player->GetId()` for the player slot, `creatureIndex` for the deck).
2. **Runtime SwapCharacter** isn't implemented in C# (Phase 09/10 finding). Add the equivalent of `Instance::SwapCharacter` so subtype 5 of `ActionCommandMsgs` can fire `PlayerCharacterDeploy` correctly.
3. **Field order audit on C# side.** Earlier note in [Phase 09 docs](../../flow/phases/09-dungeon.md) said "5 B" — that's wrong. Reconfirm the C# packet's `WriteTo`.

---

## Related

- [Phase 09 Dungeon](../../flow/phases/09-dungeon.md) — initial deploy after `OnPlayerStart`
- [Phase 10 Gameloop](../../flow/phases/10-gameloop.md) — runtime swap (missing in C#)
- [0xA1 LabsPlayerUpdate](0xA1-labsplayerupdate.md) — paired with this on swap
- [0x9C ActionCommandMsgs](0x9C-actioncommandmsgs.md) — subtype 5 (SwitchCharacter) trigger
