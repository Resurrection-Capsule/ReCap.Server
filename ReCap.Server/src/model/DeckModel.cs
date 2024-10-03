namespace ReCap.Server.Model.Deck;

public class DeckModel
{
    public required ulong ID { get; set; }
    public required string Name { get; set; }
    public required int Slot { get; set; }
    public required string? Category { get; set; }

    public required ulong AccountID { get; set; }

    public required bool Locked { get; set; }
    public required List<ulong> CreatureIds { get; set; }
}
