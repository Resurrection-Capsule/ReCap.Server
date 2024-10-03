namespace ReCap.Server.Service.Status;

using ReCap.Server.Adapters.Rest.Contracts.Game.Models.Status;
using ReCap.Server.Adapters.Rest.Contracts.Game.Models.StatusApi;
using ReCap.Server.Adapters.Rest.Contracts.Game.Models.StatusBlaze;
using ReCap.Server.Adapters.Rest.Contracts.Game.Models.StatusGame;
using ReCap.Server.Adapters.Rest.Contracts.Game.Models.StatusGms;
using ReCap.Server.Adapters.Rest.Contracts.Game.Models.StatusNucleus;

public class StatusService
{
    public static StatusContract getStatus() {
        return new StatusContract{
            Api = new StatusApiContract{Health=1, Revision=1, Version=1},
            Blaze = new StatusBlazeContract{Health=1},
            Gms = new StatusGmsContract{Health=1},
            Nucleus = new StatusNucleusContract{Health=1},
            Game = new StatusGameContract{Health=1, Countdown=90, Open=1, Throttle=1, Vip=1}
        };
    }
}