using AutoMapper;

using HttpServer;

namespace HttpServer;

public class CreaturePartMapper
{
    private IMapper mapper;

    public CreaturePartMapper() {
        var configuration = new MapperConfiguration(cfg =>
        {
            // cfg.CreateMap<CreaturePart, CreaturePartModel>();
            cfg.CreateMap<CreaturePartModel, CreaturePartContract>();
        });
        mapper = configuration.CreateMapper();
    }

    // public CreaturePartModel toModel(CreaturePart creaturePart) {
    //     return mapper.Map<CreaturePartModel>(creaturePart);
    // }

    public CreaturePartContract toContract(CreaturePartModel creaturePart) {
        return mapper.Map<CreaturePartContract>(creaturePart);
    }
}