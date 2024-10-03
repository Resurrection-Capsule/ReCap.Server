using AutoMapper;

using HttpServer;

using ReCap.Server.Domain.Deck;
using ReCap.Server.Model.Creature;
using ReCap.Server.Model.Deck;

namespace HttpServer;

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
        var deckContract = new DeckContract{
            ID = deck.ID,
            Name = deck.Name,
            Category = deck.Category,
            Slot = deck.Slot,
            Locked = deck.Locked ? 1 : 0,
            Creatures = deck.CreatureIds.Select(creatureId => creatureMapper.toContract(creatures.Find(c => c.ID == creatureId))).ToList()
        };
        // TODO: Temporary code
        deckContract.Creatures = [
            creatureMapper.toContract(creatures[deck.Slot*3]),
            creatureMapper.toContract(creatures[deck.Slot*3 + 1]),
            creatureMapper.toContract(creatures[deck.Slot*3 + 2])
        ];
        return deckContract;
    }
}