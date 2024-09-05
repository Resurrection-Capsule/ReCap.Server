namespace BlazeServer;

public interface IComponent
{
    ushort Id { get; }

    Server? Server { get; set; }

    bool HandlePacket(Client client, Packet packet);

    string GetCommandName(ushort id);
    string GetNotificationName(ushort id);
}
