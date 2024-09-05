using AutoMapper;

using HttpServer;

namespace HttpServer;

public class AccountMapper
{
    private IMapper mapper;

    public AccountMapper() {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Account, AccountModel>();
            cfg.CreateMap<AccountModel, AccountContract>()
                .ForMember(s => s.tutorialCompleted, opt => opt.MapFrom(src => src.tutorialCompleted ? "Y" : "N"));
        });
        mapper = configuration.CreateMapper();
    }

    public AccountModel toModel(Account account) {
        return mapper.Map<AccountModel>(account);
    }

    public AccountContract toContract(AccountModel account) {
        return mapper.Map<AccountContract>(account);
    }
}