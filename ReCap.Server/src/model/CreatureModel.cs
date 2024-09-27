using HttpServer;

namespace HttpServer;

public class CreatureModel
{
    public ulong ID { get; set; }
    public int Version { get; set; }

    public ulong TemplateID { get; set; }
    public string? TemplateName { get; set; }

    public ulong AccountID { get; set; }

    public ulong Cost;
    public double GearScore; // Shows up as "level" ingame
    public double ItemPoints;

    public List<ulong> Parts = new List<ulong>();
    public List<CreatureModelStat> Stats = new List<CreatureModelStat>();
    public List<CreatureModelAbilityStat> AbilityStats = new List<CreatureModelAbilityStat>();

    public string? LargePngUrl { get; set; }
    public string? LargePngBase64 { get; set; }
    public string? LargeCrc { get; set; }

    public string? ThumbPngUrl { get; set; }
    public string? ThumbPngBase64 { get; set; }
    public string? ThumbCrc { get; set; }
}

public class CreatureModelStat {
    public string statName;
    public int maxValue;
    public int currentValue;
}

public class CreatureModelAbilityStat {
    public string key;
    public string token;
    public string value;
}