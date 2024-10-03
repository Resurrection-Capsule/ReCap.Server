namespace HttpServer;

using ReCap.Server.Adapters.Rest.Contracts.Game.Models.Broadcast;

public class BroadcastService
{
    public static List<BroadcastContract> getBroadcastList() {
        var broadcast = new BroadcastContract{
            Id = 0x10,
            End = 0x11,
            Start = 0x12,
            Type = 0x13,
            Message = "Bananas for sale! Come get your bananas for only 50 bucks each!",
            Tokens = "12345678"
        };
        return new List<BroadcastContract>(){broadcast};
    }
}