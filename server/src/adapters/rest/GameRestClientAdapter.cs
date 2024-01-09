using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer
{
    class GameRestClientAdapter
    {
        [ApiMethod(Name="api.account.auth")]
        public static byte[] loginPlayerAccount(NameValueCollection parameters)
        {
            string key = parameters.Get("key");
            if (key != null) {
                string[] keyParts = key.Split(' ');
                string authToken = keyParts[0];

                
            }
            
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
            var response = new StatusResponseContract{
                Stat = "ok",
                Version = ServerConfig.GetDarksporeVersion(),
                Timestamp = 1,
                ExecTime = 1,
                Broadcasts = BroadcastService.getBroadcastList()
            };

            return XmlUtils.Serialize(response);
        }

        [ApiMethod(Name="api.status.getStatus")]
        public static byte[] getStatus(NameValueCollection parameters)
        {
            bool includeBroadcasts = parameters.Get("include_broadcasts") == "true";

            var response = new StatusResponseContract{
                Stat = "ok",
                Version = ServerConfig.GetDarksporeVersion(),
                Timestamp = 1,
                ExecTime = 1,
                Status = StatusService.getStatus()
            };

            if (includeBroadcasts)
            {
                response.Broadcasts = BroadcastService.getBroadcastList();
            }

            return XmlUtils.Serialize(response);
        }
    }
}
