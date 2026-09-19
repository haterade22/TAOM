using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Elephant;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// A howdah crew archer's origin (#627, delta review F1). The crew are the elephant's crew, not party troops: when they
/// shared the mahout's origin, the first crew casualty went through PartyGroupAgentOrigin.SetKilled, which removes the
/// Harad elephant rider from the party roster and bumps the troop supplier's NumRemovedTroops, so a side could read as
/// beaten while its elephant still fought. Casualty and score calls must never reach the mahout's origin; what the
/// scoreboard, colours and command need must come from it.
/// </summary>
[TestClass]
public class HowdahCrewAgentOriginTests
{
    private IAgentOriginBase _mahout = null!;
    private HowdahCrewAgentOrigin _crew = null!;

    [TestInitialize]
    public void SetUp()
    {
        _mahout = Substitute.For<IAgentOriginBase>();
        _crew = new HowdahCrewAgentOrigin(_mahout, troop: null, seed: 4242);
    }

    [TestMethod]
    public void SetKilled_NeverReachesTheMahoutsOrigin()
    {
        _crew.SetKilled();
        _mahout.DidNotReceive().SetKilled();
    }

    [TestMethod]
    public void SetWounded_NeverReachesTheMahoutsOrigin()
    {
        _crew.SetWounded();
        _mahout.DidNotReceive().SetWounded();
    }

    [TestMethod]
    public void SetRouted_NeverReachesTheMahoutsOrigin()
    {
        _crew.SetRouted(isOrderRetreat: false);
        _crew.SetRouted(isOrderRetreat: true);
        _mahout.DidNotReceive().SetRouted(Arg.Any<bool>());
    }

    [TestMethod]
    public void OnAgentRemoved_NeverReachesTheMahoutsOrigin()
    {
        _crew.OnAgentRemoved(0f);
        _mahout.DidNotReceive().OnAgentRemoved(Arg.Any<float>());
    }

    [TestMethod]
    public void OnScoreHit_GivesTheMahoutsTroopNoExperience()
    {
        _crew.OnScoreHit(null!, null!, 30, isFatal: true, isTeamKill: false, attackerWeapon: null!);
        _mahout.DidNotReceiveWithAnyArgs().OnScoreHit(default!, default!, default, default, default, default!);
    }

    [TestMethod]
    public void BattleCombatant_IsTheMahouts_SoTheScoreboardFindsTheParty()
    {
        var combatant = Substitute.For<IBattleCombatant>();
        _mahout.BattleCombatant.Returns(combatant);
        Assert.AreSame(combatant, _crew.BattleCombatant);
    }

    [TestMethod]
    public void CommandAndColours_AreTheMahouts()
    {
        _mahout.IsUnderPlayersCommand.Returns(true);
        _mahout.IsInSameArmyAsPlayer.Returns(true);
        _mahout.FactionColor.Returns(0x11223344u);
        _mahout.FactionColor2.Returns(0x55667788u);
        Assert.IsTrue(_crew.IsUnderPlayersCommand);
        Assert.IsTrue(_crew.IsInSameArmyAsPlayer);
        Assert.AreEqual(0x11223344u, _crew.FactionColor);
        Assert.AreEqual(0x55667788u, _crew.FactionColor2);
    }

    [TestMethod]
    public void Seed_IsThePerSeatSeed_NotTheMahouts()
    {
        _mahout.Seed.Returns(7);
        _mahout.UniqueSeed.Returns(7);
        Assert.AreEqual(4242, _crew.Seed);
        Assert.AreEqual(4242, _crew.UniqueSeed);
    }

    [TestMethod]
    public void GetTraitsMask_NoTroop_ReportsNoTraitsInsteadOfThrowing()
    {
        Assert.AreEqual(TroopTraitsMask.None, _crew.GetTraitsMask());
    }

    [TestMethod]
    public void Banner_NoneSet_IsTheMahouts_AndSetBannerNeverReachesTheMahout()
    {
        _mahout.Banner.Returns((Banner?)null);
        Assert.IsNull(_crew.Banner);
        _crew.SetBanner(null!);
        _mahout.DidNotReceiveWithAnyArgs().SetBanner(default!);
    }

    [TestMethod]
    public void Troop_IsTheCrewCharacter_NotTheMahouts()
    {
        Assert.IsNull(_crew.Troop, "the crew origin reports the troop it was built with (null here), never the mahout's");
        _ = _mahout.DidNotReceive().Troop;
    }
}
