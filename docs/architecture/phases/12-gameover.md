# Phase 12 — GameOver (`state = 0x0D`)

Terminal failure lane parallel to [Phase 11 ChainCashOut](11-chaincashout.md). Reached when the party wipes mid-dungeon (all three characters dead, no resurrect orb consumed). The server signals "mission failed" via `ChainGameMsgs(state=1)`, the client transitions itself into `GameOver`, then the only legal exit is back to `Spaceship`.

> **Important caveat.** **This lane is not exercised in either the C++ reference or the C# port.** C++ has the helper (`Server::SendChainGame`) and the state (`GameState::GameOver`) declared, but nothing actually invokes it. The doc captures the spec so the lane can be implemented later, not because it's working today.

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant R as RakNet server
    participant G as Game / Instance
    participant Obj as ObjectManager

    Note over C: party wipes (3 chars dead, no resurrect)

    G->>G: detect wipe (TODO — neither C++ nor C# has the trigger)
    G-->>C: 0xAD ChainGameMsgs(state=1)   "mission failed"
    Note over C: client transitions itself to GameOver (0x0D)
    G-->>C: 0xAE ChainGameOverMsgs (declared but never invoked in C++; body TBD)

    Note over C: client shows defeat UI, waits for input

    alt back to spaceship
        G-->>C: 0x81 ReconnectPlayer (newState=Spaceship)
    else hypothetical retry
        Note over C,G: no retry path documented; ChainGameMsgs has no "retry" state
    end
```

---

## State machine recap

C++ `IsValidStateChange` (`Client.cpp:12-49`) on the failure lane:

| From | To | Note |
|---|---|---|
| `Dungeon` | (any) | Dungeon is fall-through. Failure path goes via the client's own GameOver transition. |
| `GameOver` | `Spaceship` only | `Client.cpp:24-26` — no other legal transitions. |

> Same trick as ChainCashOut: the server never writes `0x0D` into `gameStateData.state`. The client raises its own transition once it sees `ChainGameMsgs(state=1)` arrive while in `Dungeon`.

---

## C++ — `recap_server`

### `Server::SendChainGame` (`Server.cpp:2334-2348`)

```cpp
void Server::SendChainGame(const ClientPtr& client, uint8_t state) {
    BitStream outStream(8);
    outStream.Write(PacketID::ChainGameMsgs);

    // 9500
    /*
        state:
            0 = unk (fade to black) // [set state ChainVote]
            1 = mission failed // [set state GameOver]
            2 = observer? (seems to do nothing)
    */
    Write<uint8_t>(outStream, state);

    Send(outStream, client);
}
```

Body is **2 bytes total**: `u8 packetId(0xAD) + u8 state`.

### Call sites (commented out)

`Server.cpp` references `SendChainGame` only in commented-out lines:

| Location | Original intent |
|---|---|
| `Server.cpp:1164` | `// SendChainGame(client, 0);` — Spaceship→ChainVoting transition burst |
| `Server.cpp:1188` | `// SendChainGame(client, 2);` — Dungeon-entry burst |
| `Server.cpp:1199` | `// SendChainGame(client, 2);` — also Dungeon-entry burst |
| `Server.cpp:2105` | comment: "same function as SendChainGame with value 2, nothing" |

> ⚠️ **No active code path invokes `SendChainGame` in the reference.** The function is dead but declared.

### `ChainGameOverMsgs (0xAE)`

Declared in `Server.h:82` (`constexpr MessageID ChainGameOverMsgs = 0xAE;`) and `Types.h:71`. **No `SendChainGameOver` helper, no handler, no call sites.** Body layout is unknown — must be derived from a real wire capture if the failure lane is ever implemented.

### `GameState::GameOver` (`0x0D`)

Declared at `Client.h:29` and `Server.cpp:145`. Used only in:
- `Client.cpp:24` — listed in the `from-state` cluster that may transition to `Spaceship`
- `Client.cpp:67` — `to_string` entry

No assignment to `gameStateData.state = GameOver`. The wire value is reached purely by the client raising its own transition.

### What's missing in C++

1. **Wipe trigger.** No code detects "all characters dead, no resurrect available."
2. **`SendChainGameOver` helper.** Declared but unimplemented.
3. **`OnPlayerStatusUpdate`** for any "defeat-acknowledged" status code.
4. **Reward roll on defeat.** Cashout body is intentionally skipped on failure; whether a partial DNA/medal payout is awarded is unspecified.

---

## C# — `ReCap.Server`

### Current state

- `Domain/Gameplay/GameplayState.cs` — **`GameOver` enum value is missing**. Last entry is `ChainCashOut`.
- `Adapters/RakNet/PacketType.cs` — `ChainGameMsgs (0xAD)` and `ChainGameOverMsgs (0xAE)` declared.
- `Adapters/RakNet/Packets/PacketActivator.cs:166,169` — empty case stubs for both. No packet classes.
- `Domain/Gameplay/Game.cs` — no wipe detection, no `ChainGameMsgs` send.
- `Domain/Gameplay/Player.cs` — no death tracking on the three Character slots (the C++ side has it via `Character` HP fields, but the resurrect-orb consumption / "all dead" predicate is unimplemented on both sides).

### Work to wire the lane

```csharp
// Domain/Gameplay/GameplayState.cs
public enum GameState
{
    // ...existing...
    ChainCashOut = 0x0C,
    GameOver     = 0x0D,
    Quit         = 0x0E,
}
```

```csharp
// Adapters/RakNet/Packets/ChainGameMsgsPacket.cs
public class ChainGameMsgsPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ChainGameMsgs;
    public byte State { get; set; }   // 0=fade-to-vote, 1=mission-failed, 2=observer-noop
    public void ReadFrom(Stream s) => State = (byte)s.ReadByte();
    public void WriteTo(Stream s) => s.WriteByte(State);
}
```

```csharp
// Domain/Gameplay/Game.cs (sketch — no wipe detection wired)
private void OnPartyWipe(RakNetClient sender)
{
    sender.SendPacket(new ChainGameMsgsPacket { State = 1 });
    // GameState will be raised to GameOver by the client itself.
}
```

> No further packets are sent by the server on the failure lane until the client transitions to `GameOver` and (presumably) sends a `DebugPing` from there. The DebugPing branch for `GameOver` is **not** in `Server.cpp:1159-1205` — meaning the client likely transitions straight from `GameOver` → `Spaceship` without further server input, via `SendReconnectPlayer(Spaceship)`. Verify with capture before wiring.

---

## Parity table (Phase 12)

| Item | C++ | C# | Status |
|---|---|---|---|
| `GameState.GameOver = 0x0D` | `Client.h:29` / `Server.cpp:145` | absent from `GameplayState.cs` | ❌ |
| `GameState.Quit = 0x0E` | `Client.h:30` | absent | ❌ |
| State transition rules `GameOver → Spaceship` only | `Client.cpp:24-26` | n/a (no enum value) | ❌ |
| `SendChainGame(state)` (0xAD) | `Server.cpp:2334-2348` (helper exists, never invoked) | absent | ❌ |
| `ChainGameMsgsPacket (0xAD)` class | n/a — written inline in helper | `PacketActivator` empty stub at line 166 | ❌ |
| State values `0/1/2` meaning | inline comments in helper | n/a | ❓ |
| `ChainGameOverMsgsPacket (0xAE)` | declared (`Server.h:82`), never invoked | enum + `PacketActivator` empty stub at line 169 | ❓ |
| Wipe-detection trigger | absent | absent | ❌ |
| `SendChainGameOver` helper | absent (declared in enum only) | absent | ❌ |
| `DebugPing` GameOver branch | not present in `Server.cpp:1159-1205` switch | not present in `Game.HandleDebugPing` | ❓ |
| Reward roll on failure | unspecified | unspecified | ❓ |

---

## Open audit items

1. **Capture a real wipe.** Until a wire capture exists, the lane is purely spec-from-comments. Confirm:
   - whether `ChainGameMsgs(1)` is the only S→C packet on wipe
   - whether `ChainGameOverMsgs (0xAE)` is ever sent (body layout)
   - whether `GameOver → Spaceship` is server- or client-initiated
   - whether DNA / medals are awarded on failure
2. **Add `GameState.GameOver` + `GameState.Quit`** to `Domain/Gameplay/GameplayState.cs`. Even without the lane wired, the enum values are required by [`STATE_MACHINE.md`] (Milestone 2).
3. **Implement `ChainGameMsgsPacket`** — trivial 2-byte body.
4. **Decide failure detection policy.** Three options once gameplay logic exists:
   - A) `Character.IsDead` + `ResurrectOrb.Consumed` tracking, server-side wipe check
   - B) Client-driven via `PlayerStatusUpdate` with a defeat status code (no such code observed in C++ `OnPlayerStatusUpdate` switch)
   - C) Hybrid — server tracks, client confirms via `ActionCommandMsgs`
5. **Resolve `ChainGameMsgs(state=0)` semantics.** The "fade to black → ChainVote" path is also unimplemented anywhere. Could be useful for boss-cinematic transitions; treat as separate audit.
6. **Resolve `ChainGameMsgs(state=2)` semantics.** Comment says "observer? (seems to do nothing)". If the lane survives review, document; otherwise drop.

---

## Files referenced

C++:
- `recap_server_develop/darkspore_server/source/RakNet/Client.h` (`GameState` enum)
- `recap_server_develop/darkspore_server/source/RakNet/Client.cpp` (`IsValidStateChange`, `to_string`)
- `recap_server_develop/darkspore_server/source/RakNet/Server.h` (`PacketID` 0xAD / 0xAE)
- `recap_server_develop/darkspore_server/source/RakNet/Server.cpp` (`SendChainGame`, commented call sites at 1164/1188/1199/2105)
- `recap_server_develop/darkspore_server/source/RakNet/Types.h` (PacketID enum)

C#:
- `ReCap.Server/Domain/Gameplay/GameplayState.cs` (missing `GameOver`, `Quit`)
- `ReCap.Server/Adapters/RakNet/PacketType.cs` (`ChainGameMsgs`, `ChainGameOverMsgs`)
- `ReCap.Server/Adapters/RakNet/Packets/PacketActivator.cs:166-170` (empty stubs)
- `ReCap.Server/Domain/Gameplay/Game.cs` (no wipe path)
