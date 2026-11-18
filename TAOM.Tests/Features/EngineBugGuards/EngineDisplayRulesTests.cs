using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EngineBugGuards.Domain;

namespace TAOM.Tests.Features.EngineBugGuards;

/// <summary>
/// The decisions behind the #1444 display fixes from Silas He's v1.5.6 fix package. The patches that call them are
/// thin; <c>HarmonyPatchBindingTests</c> resolves their targets against the installed engine.
/// </summary>
[TestClass]
public class EngineDisplayRulesTests
{
    // v1.5.7 CampaignUIHelper.GetCharacterTierData (:1380) draws nothing above 7; CustomBattleTroopTypeVM caps the tier
    // at 7 first (:229), so it draws 7.
    [DataTestMethod]
    [DataRow(8, false, @"General\TroopTierIcons\icon_tier_7")]
    [DataRow(10, false, @"General\TroopTierIcons\icon_tier_7")]
    [DataRow(9, true, @"General\TroopTierIcons\icon_tier_7_big")]
    public void EliteTierIcon_AboveNativeTiers_UsesTheTierSevenGlyph(int tier, bool isBig, string expected)
        => Assert.AreEqual(expected, EngineDisplayRules.EliteTierIcon(tier, isBig));

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(7)]
    public void EliteTierIcon_NativeTiers_LeavesThemToTheEngine(int tier)
        => Assert.IsNull(EngineDisplayRules.EliteTierIcon(tier, isBig: false));

    // Custom battle's GetCharacterTier (:223-229) is the campaign formula clamped to 7; TAOM's MaxCharacterTier is 10.
    [DataTestMethod]
    [DataRow(31, false, 6)]
    [DataRow(40, false, 7)]
    [DataRow(41, false, 8)]
    [DataRow(55, false, 10)]
    [DataRow(90, false, 10)]
    [DataRow(3, false, 0)]
    [DataRow(60, true, 0)]
    public void TroopTier_IsTheCampaignFormulaUpToTen(int level, bool isHero, int expected)
        => Assert.AreEqual(expected, EngineDisplayRules.TroopTier(level, isHero));

    // v1.5.7 KingdomManager.CreateKingdom (:127-131) names a kingdom with no formal name "Kingdom of {founder clan}".
    [DataTestMethod]
    [DataRow(true, false, false, true)]
    [DataRow(true, true, false, false)]
    [DataRow(false, false, false, false)]
    [DataRow(true, false, true, false)]
    public void PlayerFoundedKingdom_TakesItsChosenNameAsTheFormalName(bool founderIsPlayer, bool formalNameGiven, bool rebel, bool expected)
        => Assert.AreEqual(expected, EngineDisplayRules.ShouldUseChosenFormalName(founderIsPlayer, formalNameGiven, rebel));

    // v1.5.7 Hero.OnDeath (:2027-2028) empties both kits; the getters then fall back to Campaign.DeadBattleEquipment.
    [TestMethod]
    public void DeadHeroKit_OnlyRefillsWhatDeathEmptied()
    {
        var own = new object();
        var other = new object();

        Assert.AreSame(own, EngineDisplayRules.KitAfterDeath(own, afterDeath: null));
        Assert.AreSame(other, EngineDisplayRules.KitAfterDeath(own, afterDeath: other), "a kit another patch set wins");
        Assert.IsNull(EngineDisplayRules.KitAfterDeath<object>(null, afterDeath: null), "no kit before death, none after");
    }

    // #1446: TAOM's custom battle muster screen (TcbVM.Tier) had its own numerals up to VII.
    [DataTestMethod]
    [DataRow(0, "I")]
    [DataRow(1, "I")]
    [DataRow(4, "IV")]
    [DataRow(7, "VII")]
    [DataRow(8, "VIII")]
    [DataRow(9, "IX")]
    [DataRow(10, "X")]
    [DataRow(11, "X")]
    public void TierNumeral_ReadsUpToTen(int tier, string expected)
        => Assert.AreEqual(expected, EngineDisplayRules.TierNumeral(tier));
}
