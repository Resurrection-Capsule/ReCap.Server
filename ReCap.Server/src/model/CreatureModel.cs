namespace ReCap.Server.Model.Creature;

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

    // Example: STR,14,3;DEX,23,5;MIND,13,0;HLTH,200,107;MANA,100,13;PDEF,150,168;EDEF,50,78;CRTR,100,112
    public List<CreatureModelStat> Stats = new List<CreatureModelStat>();
    
    // Example: 868969257!minDamage,4;868969257!maxDamage,12;4022963036!percent,50;1137096183!minDamage,24;1137096183!maxDamage,36;1137096183!stunDuration,3;3492557026!minSecondaryDamage,8;3492557026!maxSecondaryDamage,20;3492557026!minDamage,24;3492557026!maxDamage,40;3492557026!radius,4;2779439490!numOrbs,6;2779439490!minDamage,16;2779439490!maxDamage,40;2779439490!deflectionIncrease,100
    public List<CreatureModelAbilityStat> AbilityStats = new List<CreatureModelAbilityStat>();

    public string? LargePngUrl { get; set; }
    public string? LargePngBase64 { get; set; }
    public string? LargeCrc { get; set; }

    public string? ThumbPngUrl { get; set; }
    public string? ThumbPngBase64 { get; set; }
    public string? ThumbCrc { get; set; }

    public void setPartsWithString(string parts) {
        Parts = parts.Split(",").ToList().Select(partId => (ulong)Convert.ToInt64(partId)).ToList();
    }

    public string getPartsAsString() {
        return string.Join(",", Parts.Select(part => $"{part}").ToList());
    }

    public void setStatsWithString(string stats) {
        Stats = stats.Split(";").ToList().Select(stat => {
            var statVals = stat.Split(",");
            return new CreatureModelStat{
                statName = statVals[0],
                maxValue = Convert.ToInt32(statVals[1]),
                currentValue = Convert.ToInt32(statVals[2])
            };
        }).ToList();
    }

    public string getStatsAsString() {
        return string.Join(";", Stats.Select(stat => $"{stat.statName},{stat.maxValue},{stat.currentValue}").ToList());
    }

    public void setAbilityStatsWithString(string abilityStats) {
        AbilityStats = abilityStats.Split(";").ToList().Select(stat => {
            var statVals1 = stat.Split("!");
            var statVals2 = statVals1[1].Split(",");
            return new CreatureModelAbilityStat{
                key = statVals1[0],
                token = statVals2[0],
                value = statVals2[1]
            };
        }).ToList();
    }

    public string getAbilityStatsAsString() {
        return string.Join(";", AbilityStats.Select(stat => $"{stat.key}!{stat.token},{stat.value}").ToList());
    }
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