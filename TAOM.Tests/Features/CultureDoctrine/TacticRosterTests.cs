using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The list a team actually gets: the doctrine's entries filtered by side and by the side's
/// Tactics skill, deduplicated, with <c>Charge</c> always present. <c>Charge</c> is load-bearing
/// in the engine, not a preference: <c>TeamAIComponent.MakeDecision</c> searches the list for a
/// <c>TacticCharge</c> when no enemy formation remains and falls back to <c>FirstOrDefault</c>
/// (`TeamAIComponent.cs:276-298`), and an empty list would hand <c>MaxBy</c> nothing.
/// </summary>
[TestClass]
public class TacticRosterTests
{
    private static Doctrine Doctrine(params TacticEntry[] entries) =>
        new Doctrine("test", entries, isDefault: false);

    [TestMethod]
    public void Build_KeepsAuthoredOrderAndMultipliers()
    {
        var doctrine = Doctrine(
            new TacticEntry(DoctrineTactic.Charge, 0.5f, 0, DoctrineSide.Any),
            new TacticEntry(DoctrineTactic.FrontalCavalryCharge, 2f, 0, DoctrineSide.Any));

        var roster = TacticRoster.Build(doctrine, tacticsSkill: 0, side: DoctrineSide.Attacker);

        CollectionAssert.AreEqual(
            new[] { DoctrineTactic.Charge, DoctrineTactic.FrontalCavalryCharge },
            roster.Select(r => r.Tactic).ToArray());
        Assert.AreEqual(0.5f, roster[0].Multiplier);
        Assert.AreEqual(2f, roster[1].Multiplier);
    }

    [TestMethod]
    public void Build_WithoutCharge_PrependsChargeAtNeutralWeight()
    {
        var doctrine = Doctrine(new TacticEntry(DoctrineTactic.FullScaleAttack, 1.5f, 0, DoctrineSide.Any));

        var roster = TacticRoster.Build(doctrine, 0, DoctrineSide.Attacker);

        Assert.AreEqual(DoctrineTactic.Charge, roster[0].Tactic, "vanilla registers Charge first; the fallback branch takes the first entry");
        Assert.AreEqual(1f, roster[0].Multiplier);
        Assert.AreEqual(DoctrineTactic.FullScaleAttack, roster[1].Tactic);
    }

    [TestMethod]
    public void Build_EmptyDoctrine_IsJustCharge()
    {
        var roster = TacticRoster.Build(Doctrine(), 0, DoctrineSide.Defender);

        Assert.AreEqual(1, roster.Count);
        Assert.AreEqual(DoctrineTactic.Charge, roster[0].Tactic);
    }

    [TestMethod]
    public void Build_SkillBelowFloor_DropsTheEntry()
    {
        var doctrine = Doctrine(
            new TacticEntry(DoctrineTactic.Charge, 1f, 0, DoctrineSide.Any),
            new TacticEntry(DoctrineTactic.FullScaleAttack, 1f, 20, DoctrineSide.Any),
            new TacticEntry(DoctrineTactic.FrontalCavalryCharge, 1f, 50, DoctrineSide.Any));

        var roster = TacticRoster.Build(doctrine, tacticsSkill: 20, DoctrineSide.Attacker);

        CollectionAssert.AreEqual(
            new[] { DoctrineTactic.Charge, DoctrineTactic.FullScaleAttack },
            roster.Select(r => r.Tactic).ToArray());
    }

    [TestMethod]
    public void Build_SideSpecificEntry_OnlyOnThatSide()
    {
        var doctrine = Doctrine(
            new TacticEntry(DoctrineTactic.DefensiveEngagement, 2f, 0, DoctrineSide.Defender),
            new TacticEntry(DoctrineTactic.RangedHarrassmentOffensive, 2f, 0, DoctrineSide.Attacker),
            new TacticEntry(DoctrineTactic.FullScaleAttack, 1f, 0, DoctrineSide.Any));

        var attacker = TacticRoster.Build(doctrine, 0, DoctrineSide.Attacker).Select(r => r.Tactic).ToArray();
        var defender = TacticRoster.Build(doctrine, 0, DoctrineSide.Defender).Select(r => r.Tactic).ToArray();

        CollectionAssert.AreEqual(new[] { DoctrineTactic.Charge, DoctrineTactic.RangedHarrassmentOffensive, DoctrineTactic.FullScaleAttack }, attacker);
        CollectionAssert.AreEqual(new[] { DoctrineTactic.Charge, DoctrineTactic.DefensiveEngagement, DoctrineTactic.FullScaleAttack }, defender);
    }

    [TestMethod]
    public void Build_DuplicateTactic_FirstEntryWins()
    {
        var doctrine = Doctrine(
            new TacticEntry(DoctrineTactic.Charge, 2f, 0, DoctrineSide.Any),
            new TacticEntry(DoctrineTactic.Charge, 0.1f, 0, DoctrineSide.Any));

        var roster = TacticRoster.Build(doctrine, 0, DoctrineSide.Attacker);

        Assert.AreEqual(1, roster.Count, "the engine's list is unlocked and read every 5 s; two instances of one tactic would race each other's formation fields");
        Assert.AreEqual(2f, roster[0].Multiplier);
    }

    [TestMethod]
    public void Build_ChargeFilteredOutBySkill_IsStillPrepended()
    {
        var doctrine = Doctrine(new TacticEntry(DoctrineTactic.Charge, 3f, 100, DoctrineSide.Any));

        var roster = TacticRoster.Build(doctrine, tacticsSkill: 0, DoctrineSide.Attacker);

        Assert.AreEqual(1, roster.Count);
        Assert.AreEqual(DoctrineTactic.Charge, roster[0].Tactic);
        Assert.AreEqual(1f, roster[0].Multiplier, "an authored floor that excludes Charge cannot leave the team with no tactic");
    }

    [TestMethod]
    public void Build_EnsuredTactic_IsAppendedAtNeutralWeightWhenTheDoctrineLacksIt()
    {
        var doctrine = Doctrine(new TacticEntry(DoctrineTactic.Charge, 0.3f, 0, DoctrineSide.Any));

        var roster = TacticRoster.Build(doctrine, 0, DoctrineSide.Attacker, new[] { DoctrineTactic.DefensiveLine });

        // The caravan handler's DefensiveLine ignores side and skill; so does the ensured row.
        Assert.AreEqual(2, roster.Count);
        Assert.AreEqual(DoctrineTactic.DefensiveLine, roster[1].Tactic);
        Assert.AreEqual(1f, roster[1].Multiplier);
    }

    [TestMethod]
    public void Build_EnsuredTactic_KeepsTheDoctrinesOwnRowWhenPresent()
    {
        var doctrine = Doctrine(new TacticEntry(DoctrineTactic.DefensiveLine, 1.5f, 0, DoctrineSide.Any));

        var roster = TacticRoster.Build(doctrine, 0, DoctrineSide.Defender, new[] { DoctrineTactic.DefensiveLine });

        Assert.AreEqual(1, roster.Count(r => r.Tactic == DoctrineTactic.DefensiveLine));
        Assert.AreEqual(1.5f, roster.Single(r => r.Tactic == DoctrineTactic.DefensiveLine).Multiplier);
    }

    [TestMethod]
    public void VanillaEquivalent_Attacker_MatchesMissionCombatantsLogicAtSkill50()
    {
        var doctrine = DoctrineCatalog.VanillaEquivalent().Default;

        var roster = TacticRoster.Build(doctrine, tacticsSkill: 50, DoctrineSide.Attacker).Select(r => r.Tactic).ToArray();

        // MissionCombatantsLogic.cs:169-196: Charge; >=20 FullScaleAttack + RangedHarrassmentOffensive;
        // >=50 FrontalCavalryCharge + CoordinatedRetreat. Every multiplier is 1.
        CollectionAssert.AreEquivalent(new[]
        {
            DoctrineTactic.Charge, DoctrineTactic.FullScaleAttack, DoctrineTactic.RangedHarrassmentOffensive,
            DoctrineTactic.FrontalCavalryCharge, DoctrineTactic.CoordinatedRetreat,
        }, roster);
        Assert.IsTrue(TacticRoster.Build(doctrine, 50, DoctrineSide.Attacker).All(r => r.Multiplier == 1f));
    }

    [TestMethod]
    public void VanillaEquivalent_Defender_MatchesMissionCombatantsLogicAtSkill50()
    {
        var doctrine = DoctrineCatalog.VanillaEquivalent().Default;

        var roster = TacticRoster.Build(doctrine, tacticsSkill: 50, DoctrineSide.Defender).Select(r => r.Tactic).ToArray();

        CollectionAssert.AreEquivalent(new[]
        {
            DoctrineTactic.Charge, DoctrineTactic.FullScaleAttack, DoctrineTactic.DefensiveEngagement,
            DoctrineTactic.DefensiveLine, DoctrineTactic.FrontalCavalryCharge, DoctrineTactic.DefensiveRing,
            DoctrineTactic.HoldChokePoint,
        }, roster);
    }

    [TestMethod]
    public void VanillaEquivalent_SkillBelow20_IsChargeOnly()
    {
        var doctrine = DoctrineCatalog.VanillaEquivalent().Default;

        var roster = TacticRoster.Build(doctrine, tacticsSkill: 19, DoctrineSide.Attacker);

        Assert.AreEqual(1, roster.Count);
        Assert.AreEqual(DoctrineTactic.Charge, roster[0].Tactic);
    }
}
