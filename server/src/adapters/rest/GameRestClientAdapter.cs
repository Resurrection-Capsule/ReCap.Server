using System;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer;

public class GameRestClientAdapter
{
    [ApiMethod(Name="api.account.auth")]
    public byte[] loginPlayerAccount(NameValueCollection parameters)
    {
        string key = parameters.Get("key");
        if (key != null) {
            // key = auth_token::0
            string[] keyParts = key.Split(' ');
            string authToken = keyParts[0];

            
        }
        
        return null;
    }

    [ApiMethod(Name="api.account.getAccount")]
    public byte[] getPlayerAccount(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.account.logout")]
    public byte[] logoutPlayerAccount(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.account.searchAccounts")]
    public byte[] searchPlayerAccounts(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.account.setSettings")]
    public byte[] setPlayerAccountSettings(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.account.unlock")]
    public byte[] unlockPlayerAccount(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.account.setNewPlayerStats")]
    public byte[] setNewPlayerStats(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.creature.getCreature")]
    public byte[] getCreature(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.creature.getTemplate")]
    public byte[] getTemplate(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.creature.resetCreature")]
    public byte[] resetCreature(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.creature.unlockCreature")]
    public byte[] unlockCreature(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.creature.updateCreature")]
    public byte[] updateCreature(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.deck.updateDecks")]
    public byte[] updateDecks(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.game.exitGame")]
    public byte[] exitGame(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.game.getGame")]
    public byte[] getGame(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.game.getRandomGame")]
    public byte[] getRandomGame(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.inventory.getPartList")]
    public byte[] getPartList(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.inventory.getPartOfferList")]
    public byte[] getPartOfferList(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.inventory.updatePartStatus")]
    public byte[] updatePartStatus(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.inventory.vendorParts")]
    public byte[] getVendorParts(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.leaderboard.getLeaderboard")]
    public byte[] getLeaderboard(NameValueCollection parameters)
    {
        return null;
    }

    [ApiMethod(Name="api.status.getBroadcastList")]
    public byte[] getBroadcastList(NameValueCollection parameters)
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
    public byte[] getStatus(NameValueCollection parameters)
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
