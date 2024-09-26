using HttpServer;

namespace HttpServer;

public class CreatureModel
{
    public ulong ID { get; set; }
    public int Version { get; set; }

    public ulong TemplateID { get; set; }
    public string? TemplateName { get; set; }

    public ulong AccountID { get; set; }

    public ulong Cost = 0;
    public double GearScore = 0; // Shows up as "level" ingame
    public double ItemPoints = 0;
    public ulong PartsCount = 0;

    public string? LargePngUrl { get; set; }
    public string? LargePngBase64 { get; set; }
    public ulong? LargeCrc { get; set; }

    public string? ThumbPngUrl { get; set; }
    public string? ThumbPngBase64 { get; set; }
    public ulong? ThumbCrc { get; set; }
}
