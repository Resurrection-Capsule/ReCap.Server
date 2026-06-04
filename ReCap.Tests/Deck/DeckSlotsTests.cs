using System.Collections.Generic;
using ReCap.Server.Services;
using Xunit;

namespace ReCap.Tests.Deck;

public class DeckSlotsTests
{
    private static readonly HashSet<ulong> Owned = new() { 442, 475, 416 };

    [Fact]
    public void Client_nine_id_csv_compacts_to_three_slots()
    {
        // Real wire shape seen in logs: "442,475,416,0,0,0,0,0,0"
        var result = DeckService.BuildSlots(
            new List<ulong> { 442, 475, 416, 0, 0, 0, 0, 0, 0 }, Owned);
        Assert.Equal(new List<ulong> { 442, 475, 416 }, result);
    }

    [Fact]
    public void Foreign_ids_are_skipped()
    {
        var result = DeckService.BuildSlots(
            new List<ulong> { 442, 999, 416 }, Owned);
        Assert.Equal(new List<ulong> { 442, 416, 0 }, result);
    }

    [Fact]
    public void Interleaved_zeros_compact()
    {
        var result = DeckService.BuildSlots(
            new List<ulong> { 0, 442, 0, 475 }, Owned);
        Assert.Equal(new List<ulong> { 442, 475, 0 }, result);
    }

    [Fact]
    public void More_than_three_valid_caps_at_three()
    {
        var owned = new HashSet<ulong> { 1, 2, 3, 4 };
        var result = DeckService.BuildSlots(new List<ulong> { 1, 2, 3, 4 }, owned);
        Assert.Equal(new List<ulong> { 1, 2, 3 }, result);
    }

    [Fact]
    public void Nothing_valid_yields_empty_deck()
    {
        var result = DeckService.BuildSlots(new List<ulong> { 0, 999 }, Owned);
        Assert.Equal(new List<ulong> { 0, 0, 0 }, result);
    }
}
