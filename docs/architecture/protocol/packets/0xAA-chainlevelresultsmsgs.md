# 0xAA — ChainLevelResultsMsgs

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | ❓ unknown | [11 ChainCashOut](../../flow/phases/11-chaincashout.md) (presumed) | ❌ |

Declared in both enums, **zero implementation anywhere.** No C++ helper, no C# class, no parser/dispatch. The body layout is purely speculative — no real wire data exists to derive it from.

Likely intent (from the name): per-level result summary, dispatched after a Dungeon completes but before the cashout flow. Could carry medals, time, damage, kills, healing per player.

---

## Body layout

❓ **Entirely unknown.** No capture, no source.

Likely shapes (speculation):

- u8 `value` + per-player stat struct array
- u8 count + N× `LevelResultEntry`

---

## C++ writer

**Absent.** `ReCap.Cpp/darkspore_server/source/RakNet/Server.h:78`:

```cpp
constexpr MessageID ChainLevelResultsMsgs = 0xAA;
```

That's the only mention. `grep "SendChainLevelResults\|ChainLevelResultsMsgs" Server.cpp` returns zero results. No reader either.

---

## C# packet class

**Absent.** `Adapters/RakNet/PacketType.cs:47` declares `ChainLevelResultsMsgs = 0xAA`. `Adapters/RakNet/Packets/PacketActivator.cs:156-157` is an empty `case PacketType.ChainLevelResultsMsgs: break;` stub.

---

## Open audit items

1. **Capture-required.** Without a real session capture, this sheet is a placeholder. Reserve the opcode but defer implementation.
2. **Retail Maxis behaviour.** Was this packet ever sent in the official Darkspore? Or is it a reservation that shipped never-used?
3. **If implemented, likely sits between `ObjectivesComplete` and `ChainCashOutMsgs`** in the cashout flow. Document the sequence once it's clear.

---

## Related

- [Phase 11 ChainCashOut](../../flow/phases/11-chaincashout.md) — sibling terminal flow
- [0xAB ChainCashOutMsgs](0xAB-chaincashoutmsgs.md)
- [0xB9 ObjectivesComplete](0xB9-objectivescomplete.md) — likely precedes this
