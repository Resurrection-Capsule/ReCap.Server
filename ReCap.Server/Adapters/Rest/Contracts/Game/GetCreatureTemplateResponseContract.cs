using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace ReCap.Server.Adapters.Rest.Contracts.Game;

[XmlRoot("response")]
public class GetCreatureTemplateResponseContract {

    [XmlElement(ElementName = "stat")]
    public string? Stat { get; set; }
    public bool ShouldSerializeStat() => Stat != null;

    [XmlElement(ElementName = "code")]
    public int? Code { get; set; }
    public bool ShouldSerializeCode() => Code.HasValue;

    [XmlElement(ElementName = "result")]
    public int? Result { get; set; }
    public bool ShouldSerializeResult() => Result.HasValue;

    [XmlElement(ElementName = "timestamp")]
    public int? Timestamp { get; set; }
    public bool ShouldSerializeTimestamp() => Timestamp.HasValue;

    [XmlElement(ElementName = "exectime")]
    public int? ExecTime { get; set; }
    public bool ShouldSerializeExecTime() => ExecTime.HasValue;

    [XmlElement(ElementName = "name_locale_id")]
    public string? NameLocaleId { get; set; }

    [XmlElement(ElementName = "text_locale_id")]
    public string? TextLocaleId { get; set; }

    [XmlElement(ElementName = "name")]
    public string? TemplateName { get; set; }

    [XmlElement(ElementName = "type_a")]
    public string? Type { get; set; }

    [XmlElement(ElementName = "weapon_min_damage")]
    public double? WeaponMinDamage { get; set; }

    [XmlElement(ElementName = "weapon_max_damage")]
    public double? WeaponMaxDamage { get; set; }

    [XmlElement(ElementName = "gear_score")]
    public double? GearScore { get; set; }

    [XmlElement(ElementName = "class")]
    public string? Class { get; set; }

    [XmlElement(ElementName = "creature_parts")]
    public string? PartsStr { get; set; }

    [XmlElement(ElementName = "stats_template")]
    public string? StatsTemplate { get; set; }
    public bool ShouldSerializeStatsTemplate() => StatsTemplate != null;

    // TODO: stats_template
    // TODO: stats_template_ability_keyvalues

    [XmlElement(ElementName = "ability_basic")]
    public ulong? AbilityBasic { get; set; }

    [XmlElement(ElementName = "ability_special_1")]
    public ulong? AbilitySpecial1 { get; set; }

    [XmlElement(ElementName = "ability_special_2")]
    public ulong? AbilitySpecial2 { get; set; }

    [XmlElement(ElementName = "ability_random")]
    public ulong? AbilityRandom { get; set; }

    [XmlElement(ElementName = "ability_passive")]
    public ulong? AbilityPassive { get; set; }

    [XmlArray("ability")]
    [XmlArrayItem("id")]
    public List<ulong>? Abilities { get; set; }
}