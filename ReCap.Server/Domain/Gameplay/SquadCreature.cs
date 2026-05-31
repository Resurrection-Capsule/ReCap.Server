namespace ReCap.Server.Domain.Gameplay;

// One resolved squad slot: the creature the player actually owns and placed in the
// selected deck. Mirrors C++ Player::SetSquad reading user->GetCreatureById(id) ->
// noun/version/type/gearScore. Never hardcoded; resolved from the persisted deck.
public sealed record SquadCreature(
    uint Noun,
    int Version,
    uint CreatureType,
    float GearScore,
    float GearScoreFlattened,
    float MaxHealth,
    float MaxMana);

// Darkspore element types as written to mCreatureType on the wire. Values mirror the
// C++ SporeNet::CreatureType enum (Creature.cpp from_string); the client expects these
// exact ordinals. The DB/template stores the element as a string (BIO/CYBER/...), so we
// resolve it here instead of hardcoding a neutral 0 (which forced every hero to Bio).
public enum CreatureElementType : uint
{
    Bio = 0,
    Cyber = 1,
    Plasma = 2,
    Necro = 3,
    Chrono = 4,
    All = 5,
    Unknown = 6
}

public static class CreatureElement
{
    // Mirrors C++ from_string(CreatureType): case-insensitive, unknown -> Unknown.
    public static uint ToWireType(string? elementType) => (uint)(elementType?.Trim().ToLowerInvariant() switch
    {
        "bio" => CreatureElementType.Bio,
        "cyber" => CreatureElementType.Cyber,
        "plasma" => CreatureElementType.Plasma,
        "necro" => CreatureElementType.Necro,
        "chrono" => CreatureElementType.Chrono,
        "all" => CreatureElementType.All,
        _ => CreatureElementType.Unknown
    });
}
