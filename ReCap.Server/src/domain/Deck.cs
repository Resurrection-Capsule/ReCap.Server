using HttpServer;

namespace HttpServer;

public class Deck
{
    public ulong ID { get; set; }
    public required string Name { get; set; }
    public required int Slot { get; set; }
    public required string? Category { get; set; } // "pvp" or "pve"

    public required ulong AccountID { get; set; }

    public required bool Locked { get; set; } = false;
    public required List<ulong> CreatureIds { get; set; }
}
