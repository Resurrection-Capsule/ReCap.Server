using HttpServer;

namespace HttpServer;

class StatusService
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