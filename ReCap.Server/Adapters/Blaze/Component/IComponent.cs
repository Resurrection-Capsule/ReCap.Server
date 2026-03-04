namespace ReCap.Server.Adapters.Blaze.Component;

public interface IComponent
{
    ushort Id { get; }

    BlazeServer? Server { get; set; }

    bool HandlePacket(Client client, Packet packet);

    string GetCommandName(ushort id);
    string GetNotificationName(ushort id);
}
