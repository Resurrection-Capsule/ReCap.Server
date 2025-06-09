using System.ComponentModel.DataAnnotations;

namespace ReCap.Server.Models;

public class CreaturePartTemplateModel
{
    [Key]
    public required ulong rigblockAssetId { get; set; }

    public required ulong prefixAssetId { get; set; }
    public required ulong prefixSecondaryAssetId { get; set; }
    public required ulong suffixAssetId { get; set; }

    public required int cost { get; set; }
    public required int level { get; set; }

    public required int rarity { get; set; }
    public required int marketStatus { get; set; }
    public required int status { get; set; }
    public required int usage { get; set; }

    public required string stats { get; set; }
    public required string typeFull { get; set; }

    public required string classTypesFull { get; set; }
    public required string scienceTypesFull { get; set; }
    public required string rarityFull { get; set; }
    public required string pngKey { get; set; }

    public required string weaponDamageModifier { get; set; }
    public required string modifiers { get; set; }
    public required string randSeed { get; set; }
}
