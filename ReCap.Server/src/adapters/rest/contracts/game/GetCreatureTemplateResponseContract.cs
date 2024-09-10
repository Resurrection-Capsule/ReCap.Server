using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HttpServer;

[XmlRoot("response")]
public class GetCreatureTemplateResponseContract : ResponseContract {

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

    // TODO: creature_parts, stats_template

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