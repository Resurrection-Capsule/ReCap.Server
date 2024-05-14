using AutoMapper;

using HttpServer;

namespace HttpServer;

public class CreatureMapper
{
    private IMapper mapper;

    public CreatureMapper() {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Creature, CreatureModel>();
            cfg.CreateMap<CreatureModel, Creature>();
            cfg.CreateMap<Creature, CreatureContract>();
        });
        mapper = configuration.CreateMapper();
    }

    public Creature toDomain(CreatureModel creature) {
        return mapper.Map<Creature>(creature);
    }
    
    public CreatureModel toModel(Creature creature) {
        return mapper.Map<CreatureModel>(creature);
    }

    public CreatureContract toContract(Creature creature) {
        return mapper.Map<CreatureContract>(creature);
    }
}