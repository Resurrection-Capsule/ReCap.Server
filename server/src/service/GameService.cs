using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer
{
    class GameService
    {
        [ApiMethod(Name="api.status.getStatus")]
        public static byte[] getStatus(NameValueCollection parameters)
        {
            bool includeBroadcasts = parameters.Get("include_broadcasts") == "true";

            string darksporeVersion = ServerConfig.GetDarksporeVersion();

            var status = new StatusContract{
                Api = new StatusApiContract{Health=1, Revision=1, Version=1},
                Blaze = new StatusBlazeContract{Health=1},
                Gms = new StatusGmsContract{Health=1},
                Nucleus = new StatusNucleusContract{Health=1},
                Game = new StatusGameContract{Health=1, Countdown=90, Open=1, Throttle=1, Vip=1}
            };

            var response = new StatusResponseContract{
                Stat = "ok",
                Version = darksporeVersion,
                Timestamp = 1,
                ExecTime = 1,
                Status = status
            };

            if (includeBroadcasts)
            {
                var broadcast = new BroadcastContract{
                    Id = 0x10,
                    End = 0x11,
                    Start = 0x12,
                    Type = 0x13,
                    Message = "Bananas for sale! Come get your bananas for only 50 bucks each!",
                    Tokens = "12345678"
                };
                response.Broadcasts = new List<BroadcastContract>(){broadcast};
            }

            return XmlUtils.Serialize(response);
        }

        [ApiMethod(Name="api.account.auth")]
        public static byte[] authenticateAccount(NameValueCollection parameters)
        {
            return null;
        }
    }
}
