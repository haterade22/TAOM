using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #834: merit belongs to the enlisted hero. v1.4.8 lets the player take over a non-hero soldier after
/// falling (Mission.CanTakeControlOfAgent needs !Character.IsHero, Mission.cs:6332-6336; enabled by
/// SandBoxMissions.OpenBattleMission and OpenSiegeMissionWithDeployment), so the sampler keys on
/// CharacterObject.PlayerCharacter, never on whichever agent is the main agent. Source pins from the
/// 1.5.x PersonalSpoilsWiringTests (9304a4cb9).
/// </summary>
[TestClass]
public class MeritHeroAttributionWiringTests
{
    private const string Behavior = "Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs";

    [TestMethod]
    public void TheSamplerTick_TreatsAControlledSoldierAsHeroDown()
    {
        var src = RepoPaths.ReadSource(Behavior, stripComments: true);
        var tick = src.Substring(src.IndexOf("OnMissionTick"), src.IndexOf("OnAgentRemoved") - src.IndexOf("OnMissionTick"));
        StringAssert.Contains(tick, "CharacterObject.PlayerCharacter");
    }

    [TestMethod]
    public void TheKillTest_IsTheHeroByCharacter_NotTheMainAgent()
    {
        // A soldier the player takes over after falling is never a hero, so his kills are not the player's.
        var src = RepoPaths.ReadSource(Behavior, stripComments: true);
        var body = src.Substring(src.IndexOf("OnAgentRemoved"));
        StringAssert.Contains(body, "CharacterObject.PlayerCharacter");
        Assert.IsFalse(body.Contains("IsMainAgent"), "OnAgentRemoved must not key on the controlled agent");
    }
}
