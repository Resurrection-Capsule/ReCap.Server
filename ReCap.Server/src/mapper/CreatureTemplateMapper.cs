using AutoMapper;

using HttpServer;

namespace HttpServer;

public class CreatureTemplateMapper
{
    private IMapper mapper;

    public CreatureTemplateMapper() {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CreatureTemplateModel, GetCreatureTemplateResponseContract>();
        });
        mapper = configuration.CreateMapper();
    }

    public GetCreatureTemplateResponseContract toContract(CreatureTemplateModel creatureTemplate) {
        return mapper.Map<GetCreatureTemplateResponseContract>(creatureTemplate);
    }
}