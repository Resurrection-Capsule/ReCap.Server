using AutoMapper;
using ReCap.Server.Adapters.Rest.Contracts.Game;
using ReCap.Server.Models;

namespace ReCap.Server.Mappers;

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