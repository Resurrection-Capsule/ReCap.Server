using HttpServer;

namespace HttpServer;

public class Deck
{
    public ulong ID { get; set; }
    public string Name { get; set; }
    public int Slot { get; set; }
    public string? Category { get; set; } // "pvp" or "pve"

    public ulong AccountID { get; set; }

    public bool Locked { get; set; } = false;
    public List<ulong> CreatureIds { get; set; }
}
