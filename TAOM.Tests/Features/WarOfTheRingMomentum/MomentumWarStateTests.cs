using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

[TestClass]
public class MomentumWarStateTests
{
    private MomentumWarState _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new MomentumWarState();
    }

    private static MomentumEvent Event(int value)
    {
        return new MomentumEvent(value, "test", MomentumActionType.BattleWon, 1000.0);
    }

    // ---- InternalMomentum (×100 scale, signed) ----

    [TestMethod]
    public void InternalMomentum_NoEvents_IsZero()
    {
        Assert.AreEqual(0f, _sut.InternalMomentum);
    }

    [TestMethod]
    public void InternalMomentum_FreeEvents_PositiveAndDescaled()
    {
        _sut.Free.AddEvent(Event(500));
        Assert.AreEqual(5f, _sut.InternalMomentum);
    }

    [TestMethod]
    public void InternalMomentum_EvilGreater_Negative()
    {
        _sut.Free.AddEvent(Event(200));
        _sut.Evil.AddEvent(Event(500));
        Assert.AreEqual(-3f, _sut.InternalMomentum);
    }

    // ---- Lifecycle flags ----

    [TestMethod]
    public void MarkWarStarted_SetsFlag()
    {
        _sut.MarkWarStarted();
        Assert.IsTrue(_sut.HasWarStarted);
    }

    [TestMethod]
    public void MarkWarEnded_SetsEndedAndVictor()
    {
        _sut.MarkWarEnded(WarOutcome.FreeVictory);
        Assert.IsTrue(_sut.HasWarEnded);
        Assert.AreEqual(WarOutcome.FreeVictory, _sut.Victor);
    }

    [TestMethod]
    public void MarkWarEnded_Twice_KeepsFirstVictor()
    {
        _sut.MarkWarEnded(WarOutcome.FreeVictory);
        _sut.MarkWarEnded(WarOutcome.EvilVictory);
        Assert.AreEqual(WarOutcome.FreeVictory, _sut.Victor);
    }

    [TestMethod]
    public void RestoreFlags_RehydratesAll()
    {
        _sut.RestoreFlags(warStarted: true, warEnded: true, victor: WarOutcome.EvilVictory);
        Assert.IsTrue(_sut.HasWarStarted);
        Assert.IsTrue(_sut.HasWarEnded);
        Assert.AreEqual(WarOutcome.EvilVictory, _sut.Victor);
    }

    // ---- Side lookup / participation ----

    [TestMethod]
    public void GetSide_Free_ReturnsFreeSide()
    {
        Assert.AreSame(_sut.Free, _sut.GetSide(MomentumSide.Free));
    }

    [TestMethod]
    public void GetSide_Evil_ReturnsEvilSide()
    {
        Assert.AreSame(_sut.Evil, _sut.GetSide(MomentumSide.Evil));
    }

    [TestMethod]
    public void DoesKingdomTakePart_EnrolledOnEitherSide_True()
    {
        _sut.Free.AddKingdom("empire_w");
        _sut.Evil.AddKingdom("empire_s");
        Assert.IsTrue(_sut.DoesKingdomTakePart("empire_w"));
        Assert.IsTrue(_sut.DoesKingdomTakePart("empire_s"));
    }

    [TestMethod]
    public void DoesKingdomTakePart_UnknownOrNull_False()
    {
        Assert.IsFalse(_sut.DoesKingdomTakePart("khuzait"));
        Assert.IsFalse(_sut.DoesKingdomTakePart(null));
        Assert.IsFalse(_sut.DoesKingdomTakePart(""));
    }
}
