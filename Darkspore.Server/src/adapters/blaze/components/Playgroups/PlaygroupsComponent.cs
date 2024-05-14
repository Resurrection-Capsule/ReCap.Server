namespace Darkspore.Server.Adapters.Blaze.Component.Playgroups;

using Darkspore.Server.Adapters.Blaze.Component.GameManager;
using BlazeServer;
using Darkspore.Server.Adapters.Blaze.Component.Util;

public class PlaygroupsComponent : IComponent
{
    public ushort Id => 6;
    public Server? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 1:
                return HandleCreatePlaygroupPacket(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private bool HandleCreatePlaygroupPacket(Client client, Packet packet)
    {
        var request = packet.ReadContent<CreatePlaygroupRequest>();

        client.RespondTo(packet, new JoinPlaygroupResponse() { Info = request.Info });
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x1 => "createPlaygroup",
            0x2 => "destroyPlaygroup",
            0x3 => "joinPlaygroup",
            0x4 => "leavePlaygroup",
            0x5 => "setPlaygroupAttributes",
            0x6 => "setMemberAttributes",
            0x7 => "kickPlaygroupMember",
            0x8 => "setPlaygroupJoinControls",
            0x9 => "finalizePlaygroupCreation",
            0xA => "lookupPlaygroupInfo",
            0xB => "resetPlaygroupSession",
            _ => "<unknown>"
        };
    }

    public string GetNotificationName(ushort id)
    {
        return id switch
        {
            0x32 => "NotifyDestroyPlaygroup",
            0x33 => "NotifyJoinPlaygroup",
            0x34 => "NotifyMemberJoinedPlaygroup",
            0x35 => "NotifyMemberRemovedFromPlaygroup",
            0x36 => "NotifyPlaygroupAttributesSet",
            0x4B => "NotifyMemberAttributesSet",
            0x4F => "NotifyLeaderChange",
            0x50 => "NotifyMemberPermissionsChange",
            0x55 => "NotifyJoinControlsChange",
            0x56 => "NotifyXboxSessionInfo",
            0x57 => "NotifyXboxSessionChange",
            _ => "<unknown>"
        };
    }

    private static void Log(string message) => Console.WriteLine($"[Playgroups component]: {message}");
}

public class CreatePlaygroupRequest : Tdf
{
    [TdfField("JOIN", false)]
    public bool Join { get; set; }

    [TdfField("PGRP")]
    public PlaygroupInfo Info { get; } = new();
}

public enum PlaygroupJoinability
{
    Open = 0,
    Closed = 1
}

public class PlaygroupInfo : Tdf
{
    [TdfField("ATTR")]
    public TdfPrimitiveMap<string, string> PlaygroupAttributes { get; } = [];

    [TdfField("ENBV", false)]
    public bool EnableVoIP { get; set; }

    [TdfField("HNET")]
    public NetworkAddress HostNetworkAddress { get; } = new();

    [TdfField("HSID", 0)]
    public byte HostSlotId { get; set; }

    [TdfField("JOIN", PlaygroupJoinability.Open)]
    public PlaygroupJoinability PlaygroupJoinability { get; set; } = PlaygroupJoinability.Open;

    [TdfField("MLIM", 0)]
    public ushort MaxMembers { get; set; }

    [TdfField("NAME", "")]
    public string Name { get; set; } = string.Empty;

    [TdfField("NTOP", GameNetworkTopology.ClientServerPeerHosted)]
    public GameNetworkTopology NetworkTopology { get; set; } = GameNetworkTopology.ClientServerPeerHosted;

    [TdfField("OWNR", 0)]
    public ulong OwnerBlazeId { get; set; }

    [TdfField("PGID", 0)]
    public ulong PlaygroupId { get; set; }

    [TdfField("PRES", PresenceMode.None)]
    public PresenceMode PresenceMode { get; set; } = PresenceMode.None;

    [TdfField("UKEY", "")]
    public string UniqueKey { get; set; } = string.Empty;

    [TdfField("UPRS", false)]
    public bool HasPresence { get; set; }

    [TdfField("UUID", "")]
    public string UUID { get; set; } = string.Empty;

    [TdfField("VOIP", VoipTopology.Disabled)]
    public VoipTopology VoipNetwork { get; set; } = VoipTopology.Disabled;

    [TdfField("XNNC")]
    public TdfBlob XnetNonce { get; } = new();

    [TdfField("XSES")]
    public TdfBlob XnetSession { get; } = new();
}

public class JoinPlaygroupResponse : Tdf
{
    [TdfField("INFO")]
    public PlaygroupInfo Info { get; set; } = new();
}
