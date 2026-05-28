# 0xAE — ChainGameOverMsgs

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | ❓ unknown | [12 GameOver](../../flow/phases/12-gameover.md) | ❌ |

Declared in both enums, **no implementation anywhere.** No helper, no parser, no call sites in C++. C# has an empty activator stub.

---

## Body layout

❓ **Entirely unknown.** No capture, no source.

Likely intent (from the name): final game-over payload after `ChainGameMsgs(state=1)` transitions the client into `GameOver`. Probably carries failure stats — time survived, kills, etc.

---

## C++ writer

**Absent.** Declared at `RakNet/Server.h:82`:

```cpp
constexpr MessageID ChainGameOverMsgs = 0xAE;
```

And in `RakNet/Types.h:71`. `grep "SendChainGameOver\|ChainGameOverMsgs" Server.cpp` returns zero results.

---

## C# packet class

**Absent.** `Adapters/RakNet/PacketType.cs:51` declares `ChainGameOverMsgs = 0xAE`. `Adapters/RakNet/Packets/PacketActivator.cs:169-170` is an empty stub.

---

## Open audit items

1. **Capture-required.** Without a real wipe capture, this sheet is a placeholder. Reserve the opcode but defer.
2. **Retail Maxis behaviour.** Was this packet ever sent? Or is it a reservation that shipped never-used (same as [0xAA ChainLevelResultsMsgs](0xAA-chainlevelresultsmsgs.md))?
3. **If sent, sequence is probably:** `ChainGameMsgs(state=1)` → client transitions to `GameOver` → server sends `ChainGameOverMsgs` → client shows defeat UI → `ReconnectPlayer(Spaceship)` returns to lobby.

---

## Related

- [Phase 12 GameOver](../../flow/phases/12-gameover.md)
- [0xAD ChainGameMsgs](0xAD-chaingamemsgs.md) — `state=1` trigger
- [0xAA ChainLevelResultsMsgs](0xAA-chainlevelresultsmsgs.md) — sibling never-used opcode
