using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CombatMechanics;

// Mike, 2026-09-25: the cave and the hill troll have 200 health. A troll's health comes from two places the engine
// never reconciles: Custom Battle reads the race Monster's hit_points (BasicCharacterObject.MaxHitPoints), and the
// campaign starts every troop at 100 (DefaultCharacterStatsModel.MaxHitpoints), which TAOM lifts through the race's
// baseHitPoints row. The Monster lives in the UNVERSIONED LOTRLOME_Armory, so this pins the two to one number.
// Inconclusive where the Armory is not installed.

namespace TAOM.Tests.Features.CombatMechanics;

[TestClass]
[TestCategory("LiveInstall")]
public class TrollHitPointsLiveDataTests
{
    private static string MonstersXml()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        string path = Path.Combine(game, "Modules", "LOTRLOME_Armory", "ModuleData", "monsters.xml");
        if (!File.Exists(path))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live data cannot be checked here.");
        return path;
    }

    [DataTestMethod]
    [DataRow("cave_troll")]
    [DataRow("hill_troll")]
    public void TrollMonster_HitPoints_MatchTheCampaignBaseHitPoints(string race)
    {
        int campaign = new CombatMechanicsConfig().RaceModifiers[race].BaseHitPoints;
        var monster = XDocument.Load(MonstersXml()).Descendants("Monster")
            .SingleOrDefault(m => (string?)m.Attribute("id") == race);

        Assert.IsNotNull(monster, $"the {race} Monster is missing from the Armory's monsters.xml");
        Assert.AreEqual(campaign.ToString(), (string?)monster!.Attribute("hit_points"),
            $"{race}: Custom Battle (the Monster's hit_points) and the campaign (baseHitPoints {campaign}) disagree");
    }
}
