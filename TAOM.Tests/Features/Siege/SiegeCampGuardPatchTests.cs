using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Siege.Hooks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.Siege;

/// <summary>
/// Patch8_SiegeCampGuard, called directly on each path through the prefix. The engine objects are
/// uninitialized: a bare BesiegerCamp has a null SiegeEvent, so the prefix sees no settlement;
/// CampWithSettlementAt sets only the members the prefix reads. Debug.DebugManager is a
/// substitute so the prefix's log lines are observable (Debug.Print is a no-op without one).
/// </summary>
[TestClass]
public class SiegeCampGuardPatchTests
{
    private IDebugManager? _previousDebugManager;
    private IDebugManager _debug = null!;

    [TestInitialize]
    public void Setup()
    {
        _previousDebugManager = Debug.DebugManager;
        _debug = Substitute.For<IDebugManager>();
        Debug.DebugManager = _debug;
    }

    [TestCleanup]
    public void Cleanup() => Debug.DebugManager = _previousDebugManager;

    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static BesiegerCamp BareCamp() => Bare<BesiegerCamp>();

    private static void SetPrivate(object target, string property, object value) =>
        target.GetType().GetProperty(property)!.GetSetMethod(nonPublic: true)!
            .Invoke(target, new[] { value });

    private static BesiegerCamp CampWithSettlementAt(CampaignVec2 gate)
    {
        var settlement = Bare<Settlement>();
        SetPrivate(settlement, nameof(Settlement.GatePosition), gate);
        SetPrivate(settlement, nameof(Settlement.Party), Bare<PartyBase>());

        var siegeEvent = Bare<SiegeEvent>();
        typeof(SiegeEvent).GetField(nameof(SiegeEvent.BesiegedSettlement))!
            .SetValue(siegeEvent, settlement);

        var camp = BareCamp();
        SetPrivate(camp, nameof(BesiegerCamp.SiegeEvent), siegeEvent);
        return camp;
    }

    private void AssertLogged(string fragment, int times) =>
        _debug.Received(times).Print(Arg.Is<string>(m => m.Contains(fragment)),
            Arg.Any<int>(), Arg.Any<Debug.DebugColor>(), Arg.Any<ulong>());

    [TestMethod]
    public void Prefix_Camp1FramesPresent_RunsVanillaUntouched()
    {
        // Arrange
        var camp1 = new[] { MatrixFrame.Identity };
        var originalCamp1 = camp1;
        var camp2 = new MatrixFrame[0];
        CampaignVec2 result = default;

        // Act
        bool runVanilla = BesiegerCamp_GetSiegeCampPartyPosition_Patch.Prefix(
            BareCamp(), null!, ref camp1, ref camp2, ref result);

        // Assert
        Assert.IsTrue(runVanilla);
        Assert.AreSame(originalCamp1, camp1);
        _debug.DidNotReceiveWithAnyArgs().Print(default!, default, default, default);
    }

    [TestMethod]
    public void Prefix_NoSettlementAndNoFrames_DefersToVanillaWithoutThrowing()
    {
        // Arrange
        var camp1 = new MatrixFrame[0];
        var camp2 = new MatrixFrame[0];
        CampaignVec2 result = default;

        // Act
        bool runVanilla = BesiegerCamp_GetSiegeCampPartyPosition_Patch.Prefix(
            BareCamp(), null!, ref camp1, ref camp2, ref result);

        // Assert
        Assert.IsTrue(runVanilla);
        AssertLogged("No besieged settlement", 1);
        AssertLogged("patch exception", 0);
    }

    [TestMethod]
    public void Prefix_NoSettlementButCamp2Frames_SwapsCamp2IntoCamp1()
    {
        // Arrange
        var camp1 = new MatrixFrame[0];
        var camp2 = new[] { MatrixFrame.Identity };
        var originalCamp2 = camp2;
        CampaignVec2 result = default;

        // Act
        bool runVanilla = BesiegerCamp_GetSiegeCampPartyPosition_Patch.Prefix(
            BareCamp(), null!, ref camp1, ref camp2, ref result);

        // Assert: vanilla gets the camp-2 array itself, so the frames' transforms survive.
        Assert.IsTrue(runVanilla);
        Assert.AreSame(originalCamp2, camp1);
        Assert.AreEqual(0, camp2.Length);
    }

    [TestMethod]
    public void Prefix_SettlementButNoFrames_PlacesPartyOnGateRingAndSkipsVanilla()
    {
        // Arrange
        var gate = new CampaignVec2(new Vec2(10f, 20f), isOnLand: true);
        var camp1 = new MatrixFrame[0];
        var camp2 = new MatrixFrame[0];
        CampaignVec2 result = default;

        // Act
        bool runVanilla = BesiegerCamp_GetSiegeCampPartyPosition_Patch.Prefix(
            CampWithSettlementAt(gate), null!, ref camp1, ref camp2, ref result);

        // Assert: party index 0 sits at angle 0 on the first ring (radius 0.5), east of the gate.
        Assert.IsFalse(runVanilla);
        Assert.AreEqual(10.5f, result.X, 1e-4f);
        Assert.AreEqual(20f, result.Y, 1e-4f);
        Assert.IsTrue(result.IsOnLand);
        AssertLogged("No besieged settlement", 0);
        AssertLogged("patch exception", 0);
    }

    [TestMethod]
    public void Prefix_ThrowsInside_LogsAndDefersToVanilla()
    {
        // Arrange: a null camp makes the settlement lookup throw inside the try.
        var camp1 = new MatrixFrame[0];
        var camp2 = new MatrixFrame[0];
        CampaignVec2 result = default;

        // Act
        bool runVanilla = BesiegerCamp_GetSiegeCampPartyPosition_Patch.Prefix(
            null!, null!, ref camp1, ref camp2, ref result);

        // Assert
        Assert.IsTrue(runVanilla);
        AssertLogged("patch exception", 1);
    }
}
