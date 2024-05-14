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
            cfg.CreateMap<CreatureTemplateModel, CreatureTemplate>();
        });
        mapper = configuration.CreateMapper();
    }

    public CreatureTemplate toDomain(CreatureTemplateModel creatureTemplate) {
        return mapper.Map<CreatureTemplate>(creatureTemplate);
    }
    
    public CreatureTemplateModel toModel(CreatureTemplate creatureTemplate) {
        return mapper.Map<CreatureTemplateModel>(creatureTemplate);
    }
}