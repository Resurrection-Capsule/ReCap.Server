using AutoMapper;
using ReCap.Server.Adapters.Rest.Contracts.Game.Models;
using ReCap.Server.Domain;
using ReCap.Server.Models;

namespace ReCap.Server.Mappers;

public class DeckMapper
{
    private IMapper mapper;

    private CreatureMapper creatureMapper;

    public DeckMapper() {
        creatureMapper = new CreatureMapper();
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Deck, DeckModel>();
        });
        mapper = configuration.CreateMapper();
    }

    public DeckModel toModel(Deck deck) {
        return mapper.Map<DeckModel>(deck);
    }

    public DeckContract toContract(DeckModel deck, List<CreatureModel> creatures) {
        return new DeckContract{
            ID = deck.ID,
            Name = deck.Name,
            Category = deck.Category,
            Slot = deck.Slot,
            Locked = deck.Locked ? 1 : 0,
            // Use the deck's actual creature ids (was a TODO hack that returned
            // creatures[slot*3..] by list index, ignoring the deck). Skip ids not found.
            Creatures = deck.CreatureIds
                .Select(id => creatures.Find(c => c.ID == id))
                .Where(c => c != null)
                .Select(c => creatureMapper.toContract(c!))
                .ToList()
        };
    }
}