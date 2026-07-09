# 0x9C — ActionCommandMsgs

| Direction | Size | Phase | Status |
|---|---|---|---|
| C→S | variable | [10 Gameloop](../../flow/phases/10-gameloop.md) | ⚠️ |

Client-driven gameplay command. Dispatches by `ActionCommand` subtype to: move, stop, swap hero, use ability, pick up catalyst, cancel, interact, dance, taunt. The most-traffic-heavy C→S packet during dungeons.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `ActionCommandCommonData` | struct | BE | Fields: `type` (u8), `objectId` (u32 BE), `position` (vec3 BE), plus 3 padding/timestamp bytes. |
| `0x??` | subtype-specific payload | varies | BE | See subtype table. |

---

## Subtype dispatch

`RakNet/Types.h:105-118`:

| `type` | Name | Subtype payload |
|---|---|---|
| `3` | Movement | `ActionCommandMovementData` (goalPosition, goalFlags, etc.) |
| `4` | StopMovement | same shape as Movement (data ignored) |
| `5` | SwitchCharacter | `value` (u32 BE) = creature index |
| `7` | UseCharacterAbility | ability id + targeting data |
| `8` | UseSquadAbility | squad ability id + targeting data |
| `9` | CatalystPickup | catalyst object id |
| `10` | Cancel | action id to cancel |
| `11` | UseInteractableObject | object id |
| `12` | Dance | (signal only) |
| `13` | Taunt | (signal only) |

Subtypes `1`, `2`, `6` are unused/unknown.

---

## C++ reader

`ReCap.Cpp/darkspore_server/source/RakNet/Server.cpp:688-984` — large switch. Excerpt:

```cpp
void Server::OnActionCommandMsgs(const ClientPtr& client) {
    const auto& player = client->GetPlayer();
    if (!player) return;

    ActionCommandData command {};
    Read<ActionCommandCommonData>(mInStream, command.data);

    const auto& object = mGame.GetObjectManager().Get(command.data.objectId);
    if (!object) return;

    switch (command.data.type) {
        case ActionCommand::Movement: {
            const auto& locomotionData = object->GetLocomotionData();
            if (!locomotionData) return;
            Read<ActionCommandMovementData>(mInStream, command.movement);
            locomotionData->SetGoalPosition(command.movement.goalPosition);
            locomotionData->SetPartialGoalPosition(command.data.position);
            locomotionData->SetGoalFlags(locomotionData->GetGoalFlags() | command.movement.goalFlags);
            mGame.MoveObject(object, *locomotionData);
            break;
        }
        case ActionCommand::StopMovement: { /* locomotionData->Stop() */ break; }
        case ActionCommand::SwitchCharacter: { mGame.SwapCharacter(player, command.value); break; }
        case ActionCommand::UseCharacterAbility: { /* Lua ability tick */ break; }
        // ... etc
    }
}
```

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/ActionCommandMsgsPacket.cs` — file exists. Dispatched by `Game.HandleActionCommand`.

C# subtype coverage (`Game.cs`):
- `3 Movement` → emits `ObjectPlayerMovePacket`
- `4 StopMovement` → emits `ObjectPlayerMovePacket` with `goalFlags=0x020`
- `5 SwitchCharacter` → `Console.WriteLine` only, no `SwapCharacter` invocation
- `7-13` → absent

---

## Open audit items

1. **Implement `SwitchCharacter` (subtype 5)** properly — currently logs and drops.
2. **Implement abilities (subtype 7-8).** Blocked by Lua VM port (out of scope).
3. **Implement `CatalystPickup` (subtype 9).** Required for loot interaction.
4. **Implement `Cancel` / `Interact` / `Dance` / `Taunt`** (subtypes 10-13).
5. **`ActionCommandCommonData` byte schema.** Document field order and padding bytes precisely — `Read<ActionCommandCommonData>` in C++ may include reserved bytes the C# reader needs to consume.

---

## Related

- [Phase 10 Gameloop](../../flow/phases/10-gameloop.md) — full action handler audit
- [0x91 ObjectPlayerMove](0x91-objectplayermove.md) — Movement subtype response
- [0xA8 ActionCommandResponse](0xA8-actioncommandresponse.md) — server reply
