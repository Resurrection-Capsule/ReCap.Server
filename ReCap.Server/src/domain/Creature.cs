using HttpServer;

namespace HttpServer;

public class Creature
{
    public ulong ID { get; set; }
    public int Version { get; set; }

    public ulong TemplateID { get; set; }
    public string TemplateName { get; set; }

    public ulong AccountID { get; set; }

    public ulong Cost = 0;
    public double GearScore = 0; // Shows up as "level" ingame
    public double ItemPoints = 0;
    public List<ulong> Parts = new List<ulong>();

    public string? LargePngUrl { get; set; }
    public string? ThumbPngUrl { get; set; }
}
