using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Spike;

/// <summary>The spike's two fortresses (#1079). Phase 1 moves this into validated JSON.</summary>
internal sealed class LongDefeatSpikeFortress
{
    private LongDefeatSpikeFortress(string scene, string defenderCulture, string attackerCulture, string player, string defenderInfantry,
        string defenderRanged, string[] attackerInfantry, string attackerRanged)
    {
        Scene = scene;
        DefenderCulture = defenderCulture;
        AttackerCulture = attackerCulture;
        Player = player;
        DefenderInfantry = defenderInfantry;
        DefenderRanged = defenderRanged;
        AttackerInfantry = attackerInfantry;
        AttackerRanged = attackerRanged;
    }

    public string Scene { get; }
    public string DefenderCulture { get; }
    public string AttackerCulture { get; }
    public string Player { get; }
    public string DefenderInfantry { get; }
    public string DefenderRanged { get; }
    public string[] AttackerInfantry { get; }
    public string AttackerRanged { get; }

    /// <summary>Every soldier id the garrison and the waves use. The launcher refuses a fortress with an unknown one, so a bad id never reaches a spawn.</summary>
    public IEnumerable<string> Troops => AttackerInfantry.Concat(new[] { AttackerRanged, DefenderInfantry, DefenderRanged });

    public static LongDefeatSpikeFortress? For(string name)
    {
        if (string.Equals(name, "helms", StringComparison.OrdinalIgnoreCase))
            return new LongDefeatSpikeFortress("taom_rohan_castle_helms_deep_forceatmo", "vlandia", "isengard", "lord_4_1",
                "fow_rohan_westfold_militiaman", "fow_rohan_eastfold_skirmisher",
                new[] { "fow_urukhai_fighter", "fow_urukhai_spearman", "fow_urukhai_berserker" }, "fow_urukhai_crossbowman");
        if (string.Equals(name, "minas", StringComparison.OrdinalIgnoreCase))
            return new LongDefeatSpikeFortress("taom_gondor_town_minas_tirith_forceatmo", "gondor", "mordor", "lord_1_9",
                "fow_gondor_militia_spearman", "fow_gondor_ano_archer_militia",
                new[] { "fow_mordor_orc_fighter", "fow_mordor_orc_impaler", "fow_mordor_uruk_warrior" }, "fow_mordor_orc_hunter");
        return null;
    }
}
