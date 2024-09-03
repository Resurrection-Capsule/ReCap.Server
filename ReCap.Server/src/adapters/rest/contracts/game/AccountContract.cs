using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace HttpServer;

[XmlRoot("account")]
public class AccountContract {

    [XmlElement(ElementName = "id")]
    public int? Id { get; set; }

    [XmlElement(ElementName = "tutorial_completed")]
    public string? tutorialCompleted { get; set; }

    [XmlElement(ElementName = "chain_progression")]
    public int? chainProgression { get; set; }

    [XmlElement(ElementName = "creature_rewards")]
    public int? creatureRewards { get; set; }

    [XmlElement(ElementName = "current_game_id")]
    public int? currentGameId { get; set; }

    [XmlElement(ElementName = "current_playgroup_id")]
    public int? currentPlaygroupId { get; set; }

    [XmlElement(ElementName = "default_deck_pve_id")]
    public int? defaultDeckPveId { get; set; }

    [XmlElement(ElementName = "default_deck_pvp_id")]
    public int? defaultDeckPvpId { get; set; }

    [XmlElement(ElementName = "dna")]
    public int? dna { get; set; }

    [XmlElement(ElementName = "level")]
    public int? level { get; set; }

    [XmlElement(ElementName = "avatar_id")]
    public int? avatarId { get; set; }

    [XmlElement(ElementName = "new_player_inventory")]
    public int? newPlayerInventory { get; set; }

    [XmlElement(ElementName = "new_player_progress")]
    public int? newPlayerProgress { get; set; }

    [XmlElement(ElementName = "cashout_bonus_time")]
    public int? cashoutBonusTime { get; set; }

    [XmlElement(ElementName = "star_level")]
    public int? starLevel { get; set; }

    [XmlElement(ElementName = "unlock_catalysts")]
    public int? unlockCatalysts { get; set; }

    [XmlElement(ElementName = "unlock_diagonal_catalysts")]
    public int? unlockDiagonalCatalysts { get; set; }

    [XmlElement(ElementName = "unlock_fuel_tanks")]
    public int? unlockFuelTanks { get; set; }

    [XmlElement(ElementName = "unlock_inventory")]
    public int? unlockInventory { get; set; }

    [XmlElement(ElementName = "unlock_pve_decks")]
    public int? unlockPveDecks { get; set; }

    [XmlElement(ElementName = "unlock_pvp_decks")]
    public int? unlockPvpDecks { get; set; }

    [XmlElement(ElementName = "unlock_stats")]
    public int? unlockStats { get; set; }

    [XmlElement(ElementName = "unlock_inventory_identify")]
    public int? unlockInventoryIdentify { get; set; }

    [XmlElement(ElementName = "unlock_editor_flair_slots")]
    public int? unlockEditorFlairSlots { get; set; }

    [XmlElement(ElementName = "upsell")]
    public int? upsell { get; set; }

    [XmlElement(ElementName = "xp")]
    public int? xp { get; set; }

    [XmlElement(ElementName = "grant_all_access")]
    public int? grantAllAccess { get; set; }

    [XmlElement(ElementName = "grant_online_access")]
    public int? grantOnlineAccess { get; set; }
    public bool ShouldSerializegrantOnlineAccess() => grantOnlineAccess.HasValue;

    [XmlElement(ElementName = "cap_level")]
    public int? capLevel { get; set; }

    [XmlElement(ElementName = "cap_progression")]
    public int? capProgression { get; set; }

}
