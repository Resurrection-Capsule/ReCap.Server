using AutoMapper;

using HttpServer;

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
            cfg.CreateMap<DeckModel, Deck>();
        });
        mapper = configuration.CreateMapper();
    }

    public Deck toDomain(DeckModel deck) {
        return mapper.Map<Deck>(deck);
    }
    
    public DeckModel toModel(Deck deck) {
        return mapper.Map<DeckModel>(deck);
    }

    public DeckContract toContract(Deck deck, List<Creature> creatures) {
        return new DeckContract{
            ID = deck.ID,
            Name = deck.Name,
            Category = deck.Category,
            Slot = deck.Slot,
            Locked = deck.Locked ? 1 : 0,
            Creatures = deck.CreatureIds.Select(creatureId => creatureMapper.toContract(creatures.Find(c => c.ID == creatureId))).ToList()
        };
    }
}