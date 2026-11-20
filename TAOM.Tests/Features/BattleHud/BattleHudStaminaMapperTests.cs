using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BattleHud.UI;
using TAOM.Features.Stamina.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BattleHud;

[TestClass]
public class BattleHudStaminaMapperTests
{
    [TestMethod]
    public void Map_BarSwitchedOff_IsHidden()
    {
        Assert.IsFalse(BattleHudStaminaMapper.Map(showBar: false, hasStamina: true, 0.5f, StaminaBand.Winded).IsVisible);
    }

    [TestMethod]
    public void Map_NoStaminaInThisMission_IsHidden()
    {
        // An arena, a mission with the feature off, or no main agent: no empty track is drawn.
        Assert.IsFalse(BattleHudStaminaMapper.Map(showBar: true, hasStamina: false, 1f, StaminaBand.Fresh).IsVisible);
    }

    [DataTestMethod]
    [DataRow(1f, 120)]
    [DataRow(0.5f, 60)]
    [DataRow(0.3f, 36)]
    [DataRow(0f, 0)]
    [DataRow(1.7f, 120, DisplayName = "a share above 1 draws a full bar")]
    [DataRow(-0.2f, 0)]
    [DataRow(float.NaN, 0, DisplayName = "a share that is not a number draws an empty bar")]
    public void Map_Share_IsTheFillHeight(float fraction, int expected)
    {
        var bar = BattleHudStaminaMapper.Map(showBar: true, hasStamina: true, fraction, StaminaBand.Fresh);

        Assert.IsTrue(bar.IsVisible);
        Assert.AreEqual(expected, bar.Height);
    }

    [TestMethod]
    public void Map_EachBand_HasItsOwnColour()
    {
        Assert.AreEqual(BattleHudStaminaMapper.FreshColour, BattleHudStaminaMapper.Map(true, true, 1f, StaminaBand.Fresh).Colour);
        Assert.AreEqual(BattleHudStaminaMapper.WindedColour, BattleHudStaminaMapper.Map(true, true, 0.5f, StaminaBand.Winded).Colour);
        Assert.AreEqual(BattleHudStaminaMapper.TiredColour, BattleHudStaminaMapper.Map(true, true, 0.2f, StaminaBand.Tired).Colour);
        Assert.AreEqual(BattleHudStaminaMapper.ExhaustedColour, BattleHudStaminaMapper.Map(true, true, 0.1f, StaminaBand.Exhausted).Colour);
        var colours = new[] { BattleHudStaminaMapper.FreshColour, BattleHudStaminaMapper.WindedColour, BattleHudStaminaMapper.TiredColour, BattleHudStaminaMapper.ExhaustedColour };
        CollectionAssert.AllItemsAreUnique(colours);
        foreach (var colour in colours)
            Assert.IsTrue(Regex.IsMatch(colour, "^#[0-9A-F]{8}$"), colour);
    }

    [TestMethod]
    public void Map_Exhausted_ShowsThePoolComingBack()
    {
        // The lock ends at about a third of the pool: the grey bar must rise so the player sees how long is left.
        var bar = BattleHudStaminaMapper.Map(true, true, 0.25f, StaminaBand.Exhausted);

        Assert.AreEqual(30, bar.Height);
        Assert.AreEqual(BattleHudStaminaMapper.ExhaustedColour, bar.Colour);
    }

    [TestMethod]
    public void ThePrefab_DrawsTheBarFromTheMixinsThreeValues_AndTheTrackFitsTheFill()
    {
        var prefab = Regex.Replace(RepoPaths.ReadSource("Client/Features/CareerSystem/UI/CareerEnergyBarPrefab.cs", stripComments: true), @"\s+", " ");
        StringAssert.Contains(prefab, @"Id=""""TaomBattleHudStamina""""");
        StringAssert.Contains(prefab, @"IsVisible=""""@HudStaminaVisible""""");
        StringAssert.Contains(prefab, @"SuggestedHeight=""""@HudStaminaHeight""""");
        StringAssert.Contains(prefab, @"Color=""""@HudStaminaColour""""");
        // 2 px of track above and below a full fill.
        StringAssert.Contains(prefab, $@"SuggestedHeight=""""{BattleHudStaminaMapper.TrackHeight + 4}""""");

        var mixin = Regex.Replace(RepoPaths.ReadSource("Client/Features/BattleHud/UI/MissionAgentStatusBattleHudMixin.cs", stripComments: true), @"\s+", " ");
        // Read fresh each refresh from the player's current soldier: nothing about an agent is cached in the mixin.
        StringAssert.Contains(mixin, "StaminaHooks.TryRead(Mission.Current?.MainAgent, out var staminaShare, out var staminaBand)");
        StringAssert.Contains(mixin, "BattleHudStaminaMapper.Map(_showStaminaBar,");
    }

    [TestMethod]
    public void ShowStaminaBar_NoSettings_IsOnAsTheCompiledDefault()
    {
        Assert.IsTrue(new TaomSettings().ShowStaminaBar);
    }
}
