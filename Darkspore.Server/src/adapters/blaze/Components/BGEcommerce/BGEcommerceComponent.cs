namespace Darkspore.Server.Adapters.Blaze.Component.BGEcommerce;

using BlazeServer;

public class BGECommerceComponent : IComponent
{
    public ushort Id { get; } = 0x820;
    public Server? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x2:
                return HandleGetCatalog(client, packet);

            case 0x6:
                return HandleGetCommerceProfile(client, packet);

            case 0x8:
                return HandleGetBalance(client, packet);

            case 0xF:
                return HandleGetGameItems(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private static bool HandleGetCatalog(Client client, Packet packet)
    {
        var response = new GetCatalogResponse
        {
            CatalogVersion = "verylongcatalogname"
        };

        client.RespondTo(packet, response);
        return true;
    }

    private static bool HandleGetCommerceProfile(Client client, Packet packet)
    {
        var response = new GetCommerceProfileResponse();

        client.RespondTo(packet, response);
        return true;
    }

    private static bool HandleGetBalance(Client client, Packet packet)
    {
        var response = new GetBalanceResponse
        {
            ECBL = 69,
            PCBL = 420
        };

        client.RespondTo(packet, response);
        return true;
    }

    private static bool HandleGetGameItems(Client client, Packet packet)
    {
        var response = new GetGameItemsResponse();

        client.RespondTo(packet, response);
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x2 => "getCatalog",
            0x6 => "getCommerceProfile",
            0x8 => "getBalance",
            0xF => "getGameItems",
            _ => "<unknown>"
        };
    }

    public string GetNotificationName(ushort id)
    {
        return id switch
        {
            _ => "<unknown>"
        };
    }

    private static void Log(string message) => Console.WriteLine($"[BG ECommerce component]: {message}");
}

public class GetCatalogResponse : Tdf
{
    [TdfField("CATA")]
    public TdfStructVector<PurchaseableUnitTdf> Offers { get; } = [];

    [TdfField("VERS", "")]
    public string CatalogVersion { get; set; } = string.Empty;
}

public class PurchaseableUnitTdf : Tdf
{
    [TdfField("DESC", "")]
    public string Description { get; set; } = string.Empty;

    [TdfField("ENDT", 0)]
    public long ENDT { get; set; }

    [TdfField("ERNC")]
    public PurchaseOption ERNC { get; } = new();

    [TdfField("FEAT", 0)]
    public long FEAT { get; set; }

    [TdfField("FLGS", 0)]
    public long Flags { get; set; }

    [TdfField("ITMC")]
    public TdfPrimitiveVector<int> QuantitiesInOffer { get; } = [];

    [TdfField("ITMI")]
    public TdfPrimitiveVector<int> ItemsInOffer { get; } = [];

    [TdfField("PAIC")]
    public PurchaseOption PAIC { get; } = new();

    [TdfField("PUBD", 0)]
    public long PUBD { get; set; }

    [TdfField("PUID", "")]
    public string PUID { get; set; } = string.Empty;

    [TdfField("PUIF", false)]
    public bool PUIF { get; set; }

    [TdfField("PUII", "")]
    public string PUII { get; set; } = string.Empty;

    [TdfField("PUOT", "")]
    public string PUOT { get; set; } = string.Empty;

    [TdfField("SART", 0)]
    public long SART { get; set; }
}

public class PurchaseOption : Tdf
{
    [TdfField("AVIL", false)]
    public bool Available { get; set; }

    [TdfField("PAIC", 0)]
    public uint PAIC { get; set; }

    [TdfField("PAIS", "")]
    public string PAIS { get; set; } = string.Empty;
}

public class GetCommerceProfileResponse : Tdf
{
    [TdfField("GCPR")]
    public CommerceProfile Profile { get; } = new();
}

public class CommerceProfile : Tdf
{
    [TdfField("ASEC", 0)]
    public uint ASEC { get; set; }

    [TdfField("ASPC", 0)]
    public uint ASPC { get; set; }

    [TdfField("BLCC", 0)]
    public uint BLCC { get; set; }

    [TdfField("BLEC", 0)]
    public uint BLEC { get; set; }

    [TdfField("CTEC", 0)]
    public uint CTEC { get; set; }

    [TdfField("CTPC", 0)]
    public uint CTPC { get; set; }
}

public class GetBalanceResponse : Tdf
{
    [TdfField("CCBL", 0)]
    public uint CCBL { get; set; }

    [TdfField("ECBL", 0)]
    public uint ECBL { get; set; }

    [TdfField("PCBL", 0)]
    public uint PCBL { get; set; }
}

public class GetGameItemsResponse : Tdf
{
    [TdfField("ITMS")]
    public TdfStructVector<GameItem> Items { get; } = [];
}

public enum GameItemType
{
    Invalid = 0,
    Shaper = 1,
    ShaperSkin = 2,
    PerkGem = 3,
    PerkShape = 4,
    WardSkin = 5,
    AnnouncerPack = 6,
    PlayerAvatar = 7,
    KillBanner = 8,
    LoadoutPage = 9
}

public class GameItem : Tdf
{
    [TdfField("ENBL", false)]
    public bool Enable { get; set; }

    [TdfField("ITID", 0)]
    public uint ItemId { get; set; }

    [TdfField("RARE", 0)]
    public uint RARE { get; set; }

    [TdfField("STCK", 0)]
    public uint STCK { get; set; }

    [TdfField("TYPE", GameItemType.Invalid)]
    public GameItemType Type { get; set; } = GameItemType.Invalid;
}

public class BGEcommerceError : Tdf
{
    [TdfField("MSG", "")]
    public string Message { get; set; } = string.Empty;
}