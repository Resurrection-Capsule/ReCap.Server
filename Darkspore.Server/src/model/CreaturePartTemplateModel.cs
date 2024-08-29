using System.ComponentModel.DataAnnotations;

using HttpServer;

namespace HttpServer;

public class CreaturePartTemplateModel
{
    [Key]
    public ulong rigblockAssetId { get; set; }

    public ulong prefixAssetId { get; set; }
    public ulong prefixSecondaryAssetId { get; set; }
    public ulong suffixAssetId { get; set; }

    public int cost { get; set; }
    public int level { get; set; }

    public int rarity { get; set; }
    public int marketStatus { get; set; }
    public int status { get; set; }
    public int usage { get; set; }

    public string stats { get; set; }
    public string typeFull { get; set; }

    public string classTypesFull { get; set; }
    public string scienceTypesFull { get; set; }
    public string rarityFull { get; set; }
    public string pngKey { get; set; }

    public string weaponDamageModifier { get; set; }
    public string modifiers { get; set; }
    public string randSeed { get; set; }
}
