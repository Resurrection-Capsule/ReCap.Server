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
            cfg.CreateMap<CreatureModel, CreatureContract>();
        });
        mapper = configuration.CreateMapper();
    }

    public CreatureModel toModel(Creature creature) {
        return mapper.Map<CreatureModel>(creature);
    }

    public CreatureContract toContract(CreatureModel creature) {
        return mapper.Map<CreatureContract>(creature);
    }
}