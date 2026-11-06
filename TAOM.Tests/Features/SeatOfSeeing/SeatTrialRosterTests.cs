using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The field trial's two armies (build-spec section 12). The ids are checked against the repo's own ModuleData, so a rename that strands one
/// fails here, and the launcher refuses to start on an id the game does not have.
/// </summary>
[TestClass]
public class SeatTrialRosterTests
{
    private static XDocument Data(params string[] path) => XDocument.Load(RepoPaths.RepoPath(new[] { "Main", "_Module", "ModuleData" }.Concat(path).ToArray()));

    [TestMethod]
    public void TheArmies_MatchTheContractTable()
    {
        Assert.AreEqual("gondor", SeatTrialRoster.PlayerCulture);
        Assert.AreEqual("mordor", SeatTrialRoster.EnemyCulture);
        Assert.AreEqual("lord_1_9", SeatTrialRoster.Hero);
        CollectionAssert.AreEqual(
            new[] { ("fow_gondor_militia_spearman", 40), ("fow_gondor_ano_archer_militia", 40), (SeatTrialRoster.MountedTroop, 20) },
            SeatTrialRoster.Player.Select(g => (g.Id, g.Count)).ToArray());
        CollectionAssert.AreEqual(
            new[] { ("fow_mordor_orc_fighter", 120), ("fow_mordor_orc_hunter", 30) },
            SeatTrialRoster.Enemy.Select(g => (g.Id, g.Count)).ToArray());
    }

    [TestMethod]
    public void PlayerFor_NoStepList_OrAnyOtherStep_IsTheStandardRoster()
    {
        foreach (var steps in new string?[] { null, "", "queue_two_moves", "hold_line_proof, hold_line_slot", "advance_throwers_never" })
            CollectionAssert.AreEqual(SeatTrialRoster.Player.ToArray(), SeatTrialRoster.PlayerFor(steps).ToArray(), steps);
    }

    [TestMethod]
    public void PlayerFor_EitherThrowerStep_FieldsFortyJavelineersInPlaceOfTheSpearmen_AndKeepsTheRest()
    {
        foreach (var steps in new[] { "advance_throwers", "advance_throwers_vanilla", " queue_two_moves , advance_throwers ", "advance_throwers,advance_throwers_vanilla", "ADVANCE_THROWERS", " Advance_Throwers_Vanilla " })
        {
            var roster = SeatTrialRoster.PlayerFor(steps);

            CollectionAssert.AreEqual(
                new[] { (SeatTrialRoster.ThrowerTroop, 40), ("fow_gondor_ano_archer_militia", 40), (SeatTrialRoster.MountedTroop, 20) },
                roster.Select(g => (g.Id, g.Count)).ToArray(), steps);
        }

        Assert.AreEqual("gondor_har_javelineer", SeatTrialRoster.ThrowerTroop);
    }

    [TestMethod]
    public void TheThrowerSteps_AreTheMarshalTrialRowsOfTheSameNames()
    {
        foreach (var step in SeatTrialRoster.ThrowerSteps)
            Assert.IsTrue(TAOM.Features.MarshalsWord.Domain.MarshalTrialRows.Rows.Any(r => r.Name == step), step);
        Assert.AreEqual(2, SeatTrialRoster.ThrowerSteps.Count);
    }

    [TestMethod]
    public void Missing_ChecksTheJavelineer_OnlyWhenAThrowerStepIsChosen()
    {
        var asked = new System.Collections.Generic.List<string>();

        SeatTrialRoster.Missing(_ => true, id => { asked.Add(id); return true; }, "advance_throwers");
        Assert.IsTrue(asked.Contains("gondor_har_javelineer"));
        Assert.IsFalse(asked.Contains("fow_gondor_militia_spearman"), "the spearmen are not fielded in that battle");

        asked.Clear();
        SeatTrialRoster.Missing(_ => true, id => { asked.Add(id); return true; }, null);
        Assert.IsFalse(asked.Contains("gondor_har_javelineer"));
        Assert.IsTrue(asked.Contains("fow_gondor_militia_spearman"));
    }

    [TestMethod]
    public void TheJavelineer_IsAFootSoldierWithJavelinsASwordAndAShield_AndNoHorse()
    {
        var troop = Data("troops", "troops_gondor.xml").Descendants("NPCCharacter").Single(n => (string?)n.Attribute("id") == SeatTrialRoster.ThrowerTroop);

        Assert.AreEqual("Infantry", (string?)troop.Attribute("default_group"));
        var rosters = troop.Descendants("EquipmentRoster").ToList();
        Assert.AreEqual(4, rosters.Count);
        foreach (var roster in rosters)
        {
            var slots = roster.Elements("equipment").ToDictionary(e => (string)e.Attribute("slot")!, e => (string)e.Attribute("id")!);
            Assert.AreEqual("Item.western_javelin_3_t4", slots["Item0"]);
            Assert.AreEqual("Item.western_javelin_3_t4", slots["Item1"]);
            Assert.IsFalse(slots.ContainsKey("Horse") || slots.ContainsKey("HorseHarness"));
        }
    }

    [TestMethod]
    public void EveryTroopId_IsAnNpcCharacterInTheFordsRoster()
    {
        var ids = Data("fords_of_war", "fords_troops.xml").Descendants("NPCCharacter").Select(n => (string?)n.Attribute("id")).ToHashSet();

        Assert.IsTrue(ids.Count > 100, "the scan found the roster (a floor, so an empty read cannot pass)");
        foreach (var id in SeatTrialRoster.PlayerFor(null).Concat(SeatTrialRoster.Enemy).Select(g => g.Id))
            Assert.IsTrue(ids.Contains(id), id + " is not in fords_troops.xml");
    }

    [TestMethod]
    public void TheMountedTroop_CarriesAHorseAndAHarness_AndTheSideIsOfThePlayersCulture()
    {
        var troop = Data("fords_of_war", "fords_troops.xml").Descendants("NPCCharacter")
            .Single(n => (string?)n.Attribute("id") == SeatTrialRoster.MountedTroop);

        var slots = troop.Descendants("equipment").Select(e => (string?)e.Attribute("slot")).ToList();
        CollectionAssert.Contains(slots, "Horse");
        CollectionAssert.Contains(slots, "HorseHarness");
        Assert.AreEqual("Culture." + SeatTrialRoster.PlayerCulture, (string?)troop.Attribute("culture"));
    }

    [TestMethod]
    public void TheHero_IsACustomBattleLordStub()
    {
        var ids = Data("characters", "custom_battle_lords.xml").Descendants("NPCCharacter").Select(n => (string?)n.Attribute("id")).ToList();

        CollectionAssert.Contains(ids, SeatTrialRoster.Hero);
    }

    [TestMethod]
    public void BothCultures_AreDefinedByTheModulesCultureFile()
    {
        var cultures = Data("taom_spcultures.xml").Descendants("Culture").Select(c => (string?)c.Attribute("id")).ToList();

        CollectionAssert.Contains(cultures, SeatTrialRoster.PlayerCulture);
        CollectionAssert.Contains(cultures, SeatTrialRoster.EnemyCulture);
    }

    [TestMethod]
    public void Missing_WhenEverythingExists_IsEmpty()
    {
        Assert.AreEqual(0, SeatTrialRoster.Missing(_ => true, _ => true).Count);
    }

    [TestMethod]
    public void Missing_NamesEachUnknownIdOnce_WithItsKind()
    {
        var missing = SeatTrialRoster.Missing(culture => culture != "mordor", id => id != "fow_mordor_orc_hunter" && id != SeatTrialRoster.Hero);

        CollectionAssert.AreEquivalent(new[] { "culture:mordor", "character:fow_mordor_orc_hunter", "character:lord_1_9" }, missing.ToArray());
    }

    [TestMethod]
    public void Missing_WhenNothingExists_ListsEveryIdOnce()
    {
        var missing = SeatTrialRoster.Missing(_ => false, _ => false);

        Assert.AreEqual(missing.Count, missing.Distinct().Count());
        Assert.AreEqual(2 + 1 + SeatTrialRoster.Player.Concat(SeatTrialRoster.Enemy).Select(g => g.Id).Distinct().Count(), missing.Count);
    }

    [TestMethod]
    public void TheCounts_AreOneHundredToOneHundredFifty_BesideTheHero()
    {
        Assert.AreEqual(100, SeatTrialRoster.Player.Sum(g => g.Count));
        Assert.AreEqual(150, SeatTrialRoster.Enemy.Sum(g => g.Count));
    }
}
