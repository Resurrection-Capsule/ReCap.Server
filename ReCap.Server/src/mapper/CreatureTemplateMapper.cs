namespace ReCap.Server.Mapper.CreatureTemplate;

using AutoMapper;

using ReCap.Server.Adapters.Rest.Contracts.Game.GetCreatureTemplateResponse;
using ReCap.Server.Model.CreatureTemplate;

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