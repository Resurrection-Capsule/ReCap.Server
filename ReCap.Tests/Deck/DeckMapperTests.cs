using System.Collections.Generic;
using ReCap.Server.Mappers;
using ReCap.Server.Models;
using Xunit;

namespace ReCap.Tests.Deck;

public class DeckMapperTests
{
    [Fact]
    public void Zero_slots_are_omitted_from_the_contract()
    {
        // C++ WriteSquadsAPI (User.cpp:589-620): zero-id slots are silently skipped.
        var deck = new DeckModel
        {
            ID = 1, Name = "Slot 1", Slot = 1, Category = "pve",
            AccountID = 42, Locked = false,
            CreatureIds = new List<ulong> { 5, 0, 0 }
        };
        var creatures = new List<CreatureModel>
        {
            new CreatureModel { ID = 5, Version = 1, TemplateID = 1001, TemplateName = "a" }
        };

        var contract = new DeckMapper().toContract(deck, creatures);

        Assert.Single(contract.Creatures);
        Assert.Equal(1, contract.Slot);
    }
}
