using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Features.DreadAura.Hooks;
using TAOM.Features.FordsOfWar;
using TAOM.Tests.Features.FordsOfWar;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.DreadAura;

/// <summary>
/// The dread aura also runs in a Fords of War match (build-spec section 4), which has no campaign: the Fords branch must come
/// before the campaign requirement, and every other mission keeps the old gate.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class DreadMissionGateFordsTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private static Mission NewMission() => (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));

    [TestMethod]
    public void IsEligible_FordsMissionWithNoCampaign_IsTrue()
    {
        Assert.IsNull(Campaign.Current, "the test host has no campaign, which is the point");

        Assert.IsTrue(DreadMissionGate.IsEligible(NewMission(), _ => true));
    }

    [TestMethod]
    public void IsEligible_NotAFordsMissionAndNoCampaign_IsStillFalse()
    {
        Assert.IsFalse(DreadMissionGate.IsEligible(NewMission(), _ => false));
    }

    [TestMethod]
    public void IsEligible_NullMission_IsFalseEvenWhenTheFordsCheckSaysYes()
    {
        Assert.IsFalse(DreadMissionGate.IsEligible(null!, _ => true));
    }

    [TestMethod]
    public void IsEligible_TheFordsCheckReceivesTheMissionAsked()
    {
        var mission = NewMission();
        Mission? seen = null;

        DreadMissionGate.IsEligible(mission, m =>
        {
            seen = m;
            return true;
        });

        Assert.AreSame(mission, seen);
    }

    [TestMethod]
    public void IsEligible_PublicOverloadUsesTheRealFordsCheck()
    {
        var mission = NewMission();

        Assert.IsFalse(DreadMissionGate.IsEligible(mission), "no Fords mission state, no campaign");
        using (new FordsOfWarMissionTests.FakeMissionState(mission, FordsOfWarMission.MissionName))
            Assert.IsTrue(DreadMissionGate.IsEligible(mission));
    }
}
