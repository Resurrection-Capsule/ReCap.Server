using AutoMapper;

using HttpServer;

namespace HttpServer;

public class PartMapper
{
    private IMapper mapper;

    public PartMapper() {
        var configuration = new MapperConfiguration(cfg =>
        {
            // cfg.CreateMap<Part, PartModel>();
            cfg.CreateMap<PartModel, PartContract>();
        });
        mapper = configuration.CreateMapper();
    }

    // public PartModel toModel(Part part) {
    //     return mapper.Map<PartModel>(part);
    // }

    public PartContract toContract(PartModel part) {
        return mapper.Map<PartContract>(part);
    }
}