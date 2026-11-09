using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TAOM.Adapters;
using static TAOM.Tests.Features.MapEventGuard.SallySiegeTestObjects;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>#1099: what the adapter reads from the engine objects the sally-out guard sees.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SallySiegeAdapterTests
{
    [TestMethod]
    public void Read_LeaderSharesThePlayersBattle_ReportsLeaderInPlayerBattle()
    {
        var battle = Battle();
        var scene = Scene(battle, battle, Array.Empty<TroopRosterElement>());

        Assert.IsTrue(new SallySiegeAdapter(scene.Settlement, scene.Main).Read()!.LeaderInPlayerBattle);
    }

    [TestMethod]
    public void Read_LeaderInAnotherBattle_ReportsLeaderOutsidePlayerBattle()
    {
        var scene = Scene(Battle(), Battle(), Array.Empty<TroopRosterElement>());

        Assert.IsFalse(new SallySiegeAdapter(scene.Settlement, scene.Main).Read()!.LeaderInPlayerBattle);
    }

    [TestMethod]
    public void Read_LeaderInNoBattle_ReportsLeaderOutsidePlayerBattle()
    {
        var scene = Scene(null, Battle(), Array.Empty<TroopRosterElement>());

        Assert.IsFalse(new SallySiegeAdapter(scene.Settlement, scene.Main).Read()!.LeaderInPlayerBattle);
    }

    [TestMethod]
    public void Read_FieldBattle_ReportsNoLiveBattleToJoin()
    {
        var battle = Battle();
        var scene = Scene(battle, battle, Array.Empty<TroopRosterElement>());

        Assert.IsFalse(new SallySiegeAdapter(scene.Settlement, scene.Main).Read()!.JoinsLiveBattle);
    }

    [TestMethod]
    [DataRow(MapEvent.BattleTypes.SiegeOutside)]
    [DataRow(MapEvent.BattleTypes.BlockadeBattle)]
    public void Read_SiegeOutsideOrBlockade_ReportsLiveBattleToJoin(MapEvent.BattleTypes type)
    {
        var battle = Battle(type);
        var scene = Scene(battle, battle, Array.Empty<TroopRosterElement>());

        Assert.IsTrue(new SallySiegeAdapter(scene.Settlement, scene.Main).Read()!.JoinsLiveBattle);
    }

    [TestMethod]
    public void Read_HeroMarkedDiedInBattle_MarksHisEntry()
    {
        var battle = Battle();
        var scene = Scene(battle, battle, new[] { HeroEntry(KillCharacterAction.KillCharacterActionDetail.DiedInBattle) });

        CollectionAssert.AreEqual(
            new[] { (1, true) },
            new SallySiegeAdapter(scene.Settlement, scene.Main).Read()!.Roster.ToArray());
    }

    [TestMethod]
    public void Read_HeroWithAnotherMark_DoesNotMarkHisEntry()
    {
        var battle = Battle();
        var scene = Scene(battle, battle, new[] { HeroEntry(KillCharacterAction.KillCharacterActionDetail.Executed) });

        CollectionAssert.AreEqual(
            new[] { (1, false) },
            new SallySiegeAdapter(scene.Settlement, scene.Main).Read()!.Roster.ToArray());
    }

    [TestMethod]
    public void Read_Troop_CarriesItsNumberUnmarked()
    {
        var battle = Battle();
        var scene = Scene(battle, battle, new[] { TroopEntry(12) });

        CollectionAssert.AreEqual(
            new[] { (12, false) },
            new SallySiegeAdapter(scene.Settlement, scene.Main).Read()!.Roster.ToArray());
    }

    [TestMethod]
    public void Read_NoSettlement_ReturnsNull()
    {
        var battle = Battle();
        var scene = Scene(battle, battle, Array.Empty<TroopRosterElement>());

        Assert.IsNull(new SallySiegeAdapter(null!, scene.Main).Read());
    }

    [TestMethod]
    public void Read_NoMainParty_ReturnsNull()
    {
        var battle = Battle();
        var scene = Scene(battle, battle, Array.Empty<TroopRosterElement>());

        Assert.IsNull(new SallySiegeAdapter(scene.Settlement, null!).Read());
    }
}
