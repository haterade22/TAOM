using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SignatureStrikes.Domain;
using TAOM.Features.SignatureStrikes.Hooks;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// The one-frame deferral between <c>OnMeleeHit</c> (enqueue) and <c>OnMissionTick</c> (drain).
/// A blow registered while draining fires <c>OnAgentHit</c> and <c>OnRegisterBlow</c>, never
/// <c>OnMeleeHit</c>, so nothing enqueues mid-drain in practice; the swap still guarantees that
/// if something did, it would land in the NEXT drain rather than in the list being iterated.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class StrikeRequestBufferTests
{
    private static StrikeEffect Slam => new StrikeEffect(
        StrikeKind.Slam, 4f, 1.5f, 100, 0.6f, 80f, KnockDown: true, KnockBack: false, FearMorale: 15f);

    private static StrikeRequest Request(float x) =>
        new StrikeRequest(null!, null!, new Vec3(x, 0f, 0f), Slam, "sauron");

    [TestMethod]
    public void Swap_ReturnsEverythingEnqueuedAndEmptiesPending()
    {
        var sut = new StrikeRequestBuffer();
        sut.Enqueue(Request(1f));
        sut.Enqueue(Request(2f));

        var drained = sut.Swap();

        Assert.AreEqual(2, drained.Count);
        Assert.AreEqual(1f, drained[0].Center.x, 0.001f);
        Assert.AreEqual(2f, drained[1].Center.x, 0.001f);
        Assert.AreEqual(0, sut.PendingCount);
    }

    [TestMethod]
    public void Swap_WithNothingPending_ReturnsEmpty()
    {
        var sut = new StrikeRequestBuffer();

        Assert.AreEqual(0, sut.Swap().Count);
    }

    [TestMethod]
    public void Enqueue_DuringADrain_LandsInTheNextSwap()
    {
        var sut = new StrikeRequestBuffer();
        sut.Enqueue(Request(1f));

        var first = sut.Swap();
        sut.Enqueue(Request(2f));

        Assert.AreEqual(1, first.Count, "the list being iterated must not grow under the iterator");
        var second = sut.Swap();
        Assert.AreEqual(1, second.Count);
        Assert.AreEqual(2f, second[0].Center.x, 0.001f);
    }

    [TestMethod]
    public void Swap_Twice_DoesNotReplayTheFirstDrain()
    {
        var sut = new StrikeRequestBuffer();
        sut.Enqueue(Request(1f));
        sut.Swap();

        Assert.AreEqual(0, sut.Swap().Count);
    }

    [TestMethod]
    public void Clear_DropsPendingAndDrained()
    {
        var sut = new StrikeRequestBuffer();
        sut.Enqueue(Request(1f));
        var drained = sut.Swap();
        sut.Enqueue(Request(2f));

        sut.Clear();

        Assert.AreEqual(0, drained.Count);
        Assert.AreEqual(0, sut.PendingCount);
        Assert.AreEqual(0, sut.Swap().Count);
    }
}
