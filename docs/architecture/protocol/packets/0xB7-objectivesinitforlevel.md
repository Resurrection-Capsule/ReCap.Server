# 0xB7 — ObjectivesInitForLevel

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | variable (1 + N×72 B) | [09 Dungeon](../../flow/phases/09-dungeon.md) | ⚠️ |

Sends the full objectives list at Dungeon entry. Each objective gets a fixed 72-byte block (`8 B id+value + 0x40 B debug padding`). Typical payload: 5 default objectives = 1 + 5×72 = 361 B body.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `count` | u8 | — | Number of objectives. |
| `0x01..` | `Objective::WriteTo` × `count` | raw 72 B each | mixed | Per-objective block — see below. |

### Per-objective block (`Objective::WriteTo`)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `id` | u32 | **BE** | FNV-1 hash of objective name. |
| `0x04` | `value` | u32 | **BE** | Default `1`. |
| `0x08..0x47` | debug padding | 0x40 B | — | C# fills with `0x01,0x23,0x45,0x67,0x89,0xAB,0xCD,0xEF` repeating. C++ likely similar; verify. |

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:2198-2212`:

```cpp
void Server::SendObjectivesInitForLevel(const ClientPtr& client) {
    // 100%
    BitStream outStream(8);
    outStream.Write(PacketID::ObjectivesInitForLevel);

    const auto& objectives = mGame.GetObjectives();
    uint8_t count = static_cast<uint8_t>(objectives.size());

    Write<uint8_t>(outStream, count);
    for (uint8_t i = 0; i < count; ++i) {
        objectives[i].WriteTo(outStream);
    }

    Send(outStream, client);
}
```

Default objectives initialized at `Game/Instance.cpp:62-77`:

```cpp
constexpr std::array<std::string_view, 5> objectiveTypes {
    "FinishLevelQuickly",
    "DoDamageOften",
    "TouchAllObelisks",
    "DefeatAllMonsters",
    "HugeDamage"
};

for (const auto& objectiveName : objectiveTypes) {
    decltype(auto) objective = mObjectives.emplace_back();
    objective.id = utils::hash_id(objectiveName);
    objective.value = 1;
    objective.medal = RakNet::ObjectiveMedal::Gold;
    objective.description = "Do some stuff bruh";
}
```

> ⚠️ `objective.medal` and `objective.description` are set but **not in the wire layout** per the per-objective block — they likely live in the `0x40` debug padding region or are simply unused by the wire format. Audit needed.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/ObjectivesInitForLevelPacket.cs`:

```csharp
public List<ObjectiveData> Objectives { get; } = new();

public void WriteTo(Stream stream)
{
    stream.WriteByte((byte)Objectives.Count);
    foreach (var obj in Objectives) obj.WriteTo(stream);
}

public static ObjectivesInitForLevelPacket CreateDefault() { /* 5 hardcoded objectives matching C++ */ }
```

`ObjectiveData.WriteTo`:

```csharp
public void WriteTo(Stream stream)
{
    using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
    writer.WriteBE(Id);                                          // u32 BE
    writer.WriteBE(Value);                                       // u32 BE
    stream.Write(DebugPadding, 0, DebugPadding.Length);          // 0x40 B fixed pattern
}
```

`FnvHash` (C#) matches C++ `utils::hash_id`: multiply-then-xor with seed `0x811C9DC5` × `0x01000193`, ASCII lowercase.

---

## Open audit items

1. **`Objective::WriteTo` byte layout on C++ side.** Verify the C# `0x40` debug padding matches C++ — could be `medal` / `description` / `category` packed there, not literal debug bytes.
2. **Verify `medal` is on the wire** or is server-only state. If on the wire, the C# `ObjectiveData` is missing the field.
3. **FNV-1 vs FNV-1a.** C# uses FNV-1 (multiply THEN xor). C++ `utils::hash_id` is the same. Match. Confirm via known hashes (`"FinishLevelQuickly"` etc.).

---

## Related

- [Phase 09 Dungeon](../../flow/phases/09-dungeon.md) — call site
- [0xB8 ObjectiveUpdated](0xB8-objectiveupdated.md) — per-objective delta
- [0xB9 ObjectivesComplete](0xB9-objectivescomplete.md) — terminal summary
