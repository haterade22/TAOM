using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The arming gates, checked in parameter order so the inert log names the first one that failed.</summary>
[TestClass]
public class StagedSiegeActivationTests
{
    // isSiege, hasSallyOut, isClient, isWallsAssault, enabled, hasPlayerSide, hasScene
    private static ActivationVerdict Decide(bool siege = true, bool sally = false, bool client = false, bool walls = true,
        bool enabled = true, bool side = true, bool scene = true) =>
        StagedSiegeActivation.Decide(siege, sally, client, walls, enabled, side, scene);

    [TestMethod]
    public void Decide_NotASiegeFails_IsNotASiege() => Assert.AreEqual(ActivationVerdict.NotASiege, Decide(siege: false));

    [TestMethod]
    public void Decide_SallyOutFails_IsSallyOut() => Assert.AreEqual(ActivationVerdict.SallyOut, Decide(sally: true));

    [TestMethod]
    public void Decide_ClientFails_IsClient() => Assert.AreEqual(ActivationVerdict.Client, Decide(client: true));

    [TestMethod]
    public void Decide_NotWallsAssaultFails_IsNotWallsAssault() => Assert.AreEqual(ActivationVerdict.NotWallsAssault, Decide(walls: false));

    [TestMethod]
    public void Decide_DisabledFails_IsDisabled() => Assert.AreEqual(ActivationVerdict.Disabled, Decide(enabled: false));

    [TestMethod]
    public void Decide_NoPlayerSideFails_IsNoPlayerSide() => Assert.AreEqual(ActivationVerdict.NoPlayerSide, Decide(side: false));

    [TestMethod]
    public void Decide_UnrecordedSceneFails_IsUnrecordedScene() => Assert.AreEqual(ActivationVerdict.UnrecordedScene, Decide(scene: false));

    [TestMethod]
    public void Decide_AllPass_Armed() => Assert.AreEqual(ActivationVerdict.Armed, Decide());

    [TestMethod]
    public void Decide_DisabledAndUnrecorded_ReportsDisabled() =>
        Assert.AreEqual(ActivationVerdict.Disabled, Decide(enabled: false, scene: false));

    [TestMethod]
    public void Decide_EverythingFails_ReportsTheFirstGuard() =>
        Assert.AreEqual(ActivationVerdict.NotASiege, Decide(false, true, true, false, false, false, false));
}
