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
        [ApiMethod(Name="api.account.auth")]
        public static byte[] loginPlayerAccount(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.account.getAccount")]
        public static byte[] getPlayerAccount(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.account.logout")]
        public static byte[] logoutPlayerAccount(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.account.searchAccounts")]
        public static byte[] searchPlayerAccounts(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.account.setSettings")]
        public static byte[] setPlayerAccountSettings(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.account.unlock")]
        public static byte[] unlockPlayerAccount(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.account.setNewPlayerStats")]
        public static byte[] setNewPlayerStats(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.creature.getCreature")]
        public static byte[] getCreature(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.creature.getTemplate")]
        public static byte[] getTemplate(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.creature.resetCreature")]
        public static byte[] resetCreature(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.creature.unlockCreature")]
        public static byte[] unlockCreature(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.creature.updateCreature")]
        public static byte[] updateCreature(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.deck.updateDecks")]
        public static byte[] updateDecks(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.game.exitGame")]
        public static byte[] exitGame(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.game.getGame")]
        public static byte[] getGame(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.game.getRandomGame")]
        public static byte[] getRandomGame(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.inventory.getPartList")]
        public static byte[] getPartList(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.inventory.getPartOfferList")]
        public static byte[] getPartOfferList(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.inventory.updatePartStatus")]
        public static byte[] updatePartStatus(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.inventory.vendorParts")]
        public static byte[] getVendorParts(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.leaderboard.getLeaderboard")]
        public static byte[] getLeaderboard(NameValueCollection parameters)
        {
            return null;
        }

        [ApiMethod(Name="api.status.getBroadcastList")]
        public static byte[] getBroadcastList(NameValueCollection parameters)
        {
            return null;
        }

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
    }
}
