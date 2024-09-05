namespace ReCap.Server.Adapters.Blaze.Component.Messaging;

using BlazeServer;

public class MessagingComponent : IComponent
{
    public ushort Id { get; } = 0xF;
    public Server? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x2:
                return HandleFetchMessages(client, packet);

            case 0x3:
                return HandlePurgeMessages(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    public void NotifyMessage(Client client)
    {
        var data = new ServerMessage();

        client.Notify(data, Id, 1);
    }

    private static bool HandleFetchMessages(Client client, Packet packet)
    {
        var request = packet.ReadContent<FetchMessageRequest>();
        if (request is null)
        {
            Log("Unable to read content of fetchMessages request!");

            client.RespondTo(packet, error: 0x9000F);
            return true;
        }

        client.RespondTo(packet, new FetchMessageResponse());
        return true;
    }

    private static bool HandlePurgeMessages(Client client, Packet packet)
    {
        var request = packet.ReadContent<PurgeMessageRequest>();
        if (request is null)
        {
            Log("Unable to read content of purgeMessages request!");

            client.RespondTo(packet, error: 0x9000F);
            return true;
        }

        client.RespondTo(packet, new PurgeMessageResponse());
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x1 => "sendMessage",
            0x2 => "fetchMessages",
            0x3 => "purgeMessages",
            0x4 => "touchMessages",
            0x5 => "getMessages",
            0x7 => "sendGlobalMessage",
            _ => "<unknown>"
        };
    }

    public string GetNotificationName(ushort id)
    {
        return id switch
        {
            1 => "NotifyMessage",
            _ => "<unknown>"
        };
    }

    private static void Log(string message) => Console.WriteLine($"[Messaging component]: {message}");
}

public enum MessageOrder
{
    Default = 0,
    TimeAsc = 1,
    TimeDesc = 2
}

public class FetchMessageRequest : Tdf
{
    [TdfField("FLAG", 0)]
    public uint Flags { get; set; }

    [TdfField("MGID", 0)]
    public ulong MessageId { get; set; }

    [TdfField("PIDX", 0)]
    public uint PageIndex { get; set; }

    [TdfField("PSIZ", 0)]
    public uint PageSize { get; set; }

    [TdfField("SMSK", 0)]
    public uint StatusMask { get; set; }

    [TdfField("SORT", MessageOrder.Default)]
    public MessageOrder OrderBy { get; set; } = MessageOrder.Default;

    [TdfField("SRCE")]
    public BlazeObjectId Source { get; } = new();

    [TdfField("STAT", 0)]
    public uint Status { get; set; }

    [TdfField("TARG")]
    public BlazeObjectId Target { get; } = new();

    [TdfField("TYPE", 0)]
    public uint Type { get; set; }

    [TdfField("TYPL")]
    public TdfPrimitiveVector<uint> TypeList { get; } = [];
}

public class FetchMessageResponse : Tdf
{
    [TdfField("MCNT", 0)]
    public uint MessageCount { get; set; }
}

public class PurgeMessageRequest : Tdf
{
    [TdfField("FLAG", 0)]
    public uint Flags { get; set; }

    [TdfField("MGID", 0)]
    public ulong MessageId { get; set; }

    [TdfField("SMSK", 0)]
    public uint StatusMask { get; set; }

    [TdfField("SRCE")]
    public BlazeObjectId Source { get; } = new();

    [TdfField("STAT", 0)]
    public uint Status { get; set; }

    [TdfField("TYPE", 0)]
    public uint Type { get; set; }
}

public class PurgeMessageResponse : Tdf
{
    [TdfField("MCNT", 0)]
    public uint MessageCount { get; set; }
}

public class ServerMessage : Tdf
{
    public override void Decode(TdfDecoder decoder)
    {
        throw new NotImplementedException();
    }

    public override void Encode(TdfEncoder encoder)
    {
        throw new NotImplementedException();
    }
}