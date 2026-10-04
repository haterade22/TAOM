using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

// The live per-soldier state: active, then spent, then ready again. Keyed by object identity, so a new
// agent in a recycled engine slot is a different key (#592). Plain objects stand in for agents here.

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilityStoreTests
{
    private RaceAbilityStore<object> _sut = null!;
    private readonly List<RaceAbilityTransition<object>> _transitions = new List<RaceAbilityTransition<object>>();
    private readonly object _soldier = new object();
    private readonly RaceAbilityEffects _active = new RaceAbilityEffects { MeleeDamagePercent = 20f };
    private readonly RaceAbilityEffects _spent = new RaceAbilityEffects { MoveSpeedPercent = -20f };

    [TestInitialize]
    public void Setup() => _sut = new RaceAbilityStore<object>();

    private static RaceAbilityProfile Profile(float duration = 6f, float spent = 3f) =>
        new RaceAbilityProfile { DurationSeconds = duration, SpentSeconds = spent, MaxDurationSeconds = duration };

    [TestMethod]
    public void Get_NeverActivated_IsNull()
    {
        Assert.IsNull(_sut.Get(_soldier));
        Assert.IsNull(_sut.CurrentEffects(_soldier));
        Assert.IsNull(_sut.LastFiredAt(_soldier));
    }

    [TestMethod]
    public void Activate_MakesTheActiveEffectsCurrent()
    {
        _sut.Activate(_soldier, Profile(), 100f, _active, _spent);

        var state = _sut.Get(_soldier)!;
        Assert.AreEqual(RaceAbilityPhase.Active, state.Phase);
        Assert.AreEqual(106f, state.PhaseEndsAt, 0.0001f);
        Assert.AreSame(_active, _sut.CurrentEffects(_soldier));
        Assert.AreEqual(100f, _sut.LastFiredAt(_soldier));
    }

    [TestMethod]
    public void Advance_BeforeTheEnd_ChangesNothing()
    {
        _sut.Activate(_soldier, Profile(), 100f, _active, _spent);

        _sut.Advance(105.9f, _transitions);

        Assert.AreEqual(0, _transitions.Count);
        Assert.AreEqual(RaceAbilityPhase.Active, _sut.Get(_soldier)!.Phase);
    }

    [TestMethod]
    public void Advance_AtTheEnd_EntersTheSpentPhaseAndReportsIt()
    {
        _sut.Activate(_soldier, Profile(), 100f, _active, _spent);

        _sut.Advance(106f, _transitions);

        var transition = _transitions.Single();
        Assert.AreSame(_soldier, transition.Key);
        Assert.AreEqual(RaceAbilityPhase.Active, transition.Before.Phase);
        Assert.AreEqual(RaceAbilityPhase.Spent, transition.After.Phase);
        Assert.AreEqual(109f, _sut.Get(_soldier)!.PhaseEndsAt, 0.0001f);
        Assert.AreSame(_spent, _sut.CurrentEffects(_soldier));
    }

    [TestMethod]
    public void Advance_PastTheSpentPhase_IsReady()
    {
        _sut.Activate(_soldier, Profile(), 100f, _active, _spent);
        _sut.Advance(106f, _transitions);
        _transitions.Clear();

        _sut.Advance(109f, _transitions);

        var transition = _transitions.Single();
        Assert.AreEqual(RaceAbilityPhase.Spent, transition.Before.Phase);
        Assert.AreEqual(RaceAbilityPhase.Ready, transition.After.Phase);
        Assert.IsNull(_sut.CurrentEffects(_soldier));
        Assert.AreEqual(100f, _sut.LastFiredAt(_soldier));   // the cooldown still counts from activation
    }

    [TestMethod]
    public void Advance_NoSpentPhase_GoesStraightToReady()
    {
        _sut.Activate(_soldier, Profile(spent: 0f), 100f, _active, _spent);

        _sut.Advance(106f, _transitions);

        Assert.AreEqual(RaceAbilityPhase.Ready, _transitions.Single().After.Phase);
    }

    [TestMethod]
    public void Advance_LongStall_SkipsTheSpentPhaseItMissed()
    {
        // A hitch past both ends lands in Ready in one step, still reported as the end of an active window.
        _sut.Activate(_soldier, Profile(), 100f, _active, _spent);

        _sut.Advance(120f, _transitions);

        var transition = _transitions.Single();
        Assert.AreEqual(RaceAbilityPhase.Active, transition.Before.Phase);
        Assert.AreEqual(RaceAbilityPhase.Ready, transition.After.Phase);
    }

    [TestMethod]
    public void Advance_NaNTime_ChangesNothing()
    {
        _sut.Activate(_soldier, Profile(), 100f, _active, _spent);

        _sut.Advance(float.NaN, _transitions);

        Assert.AreEqual(0, _transitions.Count);
        Assert.AreEqual(RaceAbilityPhase.Active, _sut.Get(_soldier)!.Phase);
    }

    [TestMethod]
    public void Advance_ReadySoldier_IsNotReported()
    {
        _sut.Activate(_soldier, Profile(spent: 0f), 100f, _active, _spent);
        _sut.Advance(106f, _transitions);
        _transitions.Clear();

        _sut.Advance(200f, _transitions);

        Assert.AreEqual(0, _transitions.Count);
    }

    [TestMethod]
    public void ExtendActive_MovesTheEnd()
    {
        _sut.Activate(_soldier, Profile(), 100f, _active, _spent);

        _sut.ExtendActive(_soldier, 108f);

        Assert.AreEqual(108f, _sut.Get(_soldier)!.PhaseEndsAt, 0.0001f);
    }

    [TestMethod]
    public void ExtendActive_SpentSoldier_ChangesNothing()
    {
        _sut.Activate(_soldier, Profile(), 100f, _active, _spent);
        _sut.Advance(106f, _transitions);

        _sut.ExtendActive(_soldier, 200f);

        Assert.AreEqual(109f, _sut.Get(_soldier)!.PhaseEndsAt, 0.0001f);
    }

    [TestMethod]
    public void RecordKill_IsRemembered()
    {
        _sut.RecordKill(_soldier, 150f);

        Assert.AreEqual(150f, _sut.LastKillAt(_soldier));
    }

    [TestMethod]
    public void Remove_ForgetsTheSoldierAndHisKills()
    {
        _sut.Activate(_soldier, Profile(), 100f, _active, _spent);
        _sut.RecordKill(_soldier, 101f);

        _sut.Remove(_soldier);

        Assert.IsNull(_sut.Get(_soldier));
        Assert.IsNull(_sut.LastKillAt(_soldier));
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Clear_ForgetsEveryone()
    {
        _sut.Activate(_soldier, Profile(), 100f, _active, _spent);
        _sut.RecordKill(new object(), 101f);

        _sut.Clear();

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Keys_AreByIdentity()
    {
        // Two keys that compare equal still hold separate state: the store never trusts a key's Equals.
        var first = new EqualToEverything();
        var second = new EqualToEverything();
        _sut.Activate(first, Profile(), 100f, _active, _spent);

        Assert.IsNull(_sut.Get(second));
    }

    private sealed class EqualToEverything
    {
        public override bool Equals(object? obj) => true;

        public override int GetHashCode() => 1;
    }
}
