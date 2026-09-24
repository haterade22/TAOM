using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Siege.Hooks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.Siege;

/// <summary>
/// Patch8_SiegeCampGuard when the camp has no besieged settlement. The bare BesiegerCamp is
/// uninitialized, so SiegeEvent is null and the prefix sees no settlement. Debug.DebugManager is a
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

    private static BesiegerCamp BareCamp() =>
        (BesiegerCamp)FormatterServices.GetUninitializedObject(typeof(BesiegerCamp));

    [TestMethod]
    public void Prefix_NoSettlementAndNoFrames_DefersToVanillaWithoutThrowing()
    {
        var camp1 = new MatrixFrame[0];
        var camp2 = new MatrixFrame[0];
        CampaignVec2 result = default;

        bool runVanilla = BesiegerCamp_GetSiegeCampPartyPosition_Patch.Prefix(
            BareCamp(), null!, ref camp1, ref camp2, ref result);

        Assert.IsTrue(runVanilla);
        _debug.Received(1).Print(Arg.Is<string>(m => m.Contains("No besieged settlement")),
            Arg.Any<int>(), Arg.Any<Debug.DebugColor>(), Arg.Any<ulong>());
        _debug.DidNotReceive().Print(Arg.Is<string>(m => m.Contains("patch exception")),
            Arg.Any<int>(), Arg.Any<Debug.DebugColor>(), Arg.Any<ulong>());
    }

    [TestMethod]
    public void Prefix_NoSettlementButCamp2Frames_SwapsCamp2IntoCamp1()
    {
        var camp1 = new MatrixFrame[0];
        var camp2 = new[] { MatrixFrame.Identity };
        CampaignVec2 result = default;

        bool runVanilla = BesiegerCamp_GetSiegeCampPartyPosition_Patch.Prefix(
            BareCamp(), null!, ref camp1, ref camp2, ref result);

        Assert.IsTrue(runVanilla);
        Assert.AreEqual(1, camp1.Length);
        Assert.AreEqual(0, camp2.Length);
    }
}
