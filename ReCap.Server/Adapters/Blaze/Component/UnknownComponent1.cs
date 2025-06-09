using ReCap.Server.Utils;

namespace ReCap.Server.Adapters.Blaze.Component;

public class UnknownComponent1 : IComponent
{
    public ushort Id => 0x2678;
    public BlazeServer? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x200:
                return HandleUnknownPacket1(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private bool HandleUnknownPacket1(Client client, Packet packet)
    {
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return "<unknown>";
    }

    public string GetNotificationName(ushort id)
    {
        return "<unknown>";
    }

    private static void Log(string message) => Logger.debug($"[Unknown component 1]: {message}");
}

