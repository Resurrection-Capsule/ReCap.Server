using ReCap.Server.Utils;

namespace ReCap.Server.Adapters.Blaze.Component.GameManager;

public class GameReportingComponent : IComponent
{
    public ushort Id { get; } = 0x1C;
    public BlazeServer? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x01 => "submitGameReport",
            0x02 => "submitOfflineGameReport",
            0x03 => "submitGameEvents",
            0x04 => "getGameReportQuery",
            0x05 => "getGameReportQueriesList",
            0x06 => "getGameReports",
            0x07 => "getGameReportView",
            0x08 => "getGameReportViewInfo",
            0x09 => "getGameReportViewInfoList",
            0x0A => "getGameReportTypes",
            0x0B => "updateMetric",
            0x0C => "getGameReportColumnInfo",
            0x0D => "getGameReportColumnValues",
            0x64 => "submitTrustedMidGameReport",
            0x65 => "submitTrustedEndGameReport",
            _ => "<unknown>"
        };
    }

    public string GetNotificationName(ushort id)
    {
        return id switch
        {
            0x72 => "ResultNotification",
            _ => "<unknown>"
        };
    }

    private static void Log(string message) => Logger.debug($"[Game Reporting component]: {message}");
}
