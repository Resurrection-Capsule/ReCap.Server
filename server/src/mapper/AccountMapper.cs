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
            cfg.CreateMap<AccountModel, Account>();
        });
        mapper = configuration.CreateMapper();
    }

    public Account toDomain(AccountModel account) {
        return mapper.Map<Account>(account);
    }
    
    public AccountModel toModel(Account account) {
        return mapper.Map<AccountModel>(account);
    }
}