using AutoMapper;

using HttpServer;

namespace HttpServer;

public class CreatureTemplateMapper
{
    private IMapper mapper;

    public CreatureTemplateMapper() {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<CreatureTemplate, CreatureTemplateModel>();
        });
        mapper = configuration.CreateMapper();
    }

    public CreatureTemplateModel toModel(CreatureTemplate creatureTemplate) {
        return mapper.Map<CreatureTemplateModel>(creatureTemplate);
    }
}