using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.SignatureStrikes;
using TAOM.Features.SignatureStrikes.Domain;
using TAOM.Features.SignatureStrikes.Hooks;
using TAOM.Tests.Infrastructure;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// The campaign damage model's knockdown and knock-back verdicts for a signature hero (#605, moved out of the model by
/// #737). A null service or roster is the feature absent; a horse charge is never a signature verdict, so the roster is
/// not even probed; an attacker off the roster costs one lookup and has no opinion. A rostered attacker reaches the
/// service with a bare agent: the strike context reads the attacker only past its weapon-slot check, so slot -1 keeps
/// it off the uninitialized object, and it reads the victim only behind a null check.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SignatureStrikeVerdictsTests
{
    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    private static AttackCollisionData MeleeHit() => CollisionDataFixture.With(weaponSlot: -1);

    private static ISignatureAgentRoster RosterWith(Agent attacker)
    {
        var roster = Substitute.For<ISignatureAgentRoster>();
        roster.TryGet(attacker, out _).Returns(call =>
        {
            call[1] = new SignatureAgentEntry(0);
            return true;
        });
        return roster;
    }

    [TestMethod]
    public void Decide_NoService_HasNoOpinionAndNeverProbesTheRoster()
    {
        var roster = Substitute.For<ISignatureAgentRoster>();

        Assert.IsNull(SignatureStrikeVerdicts.Decide(null, roster, null!, null!, default, default, knockdown: true));
        roster.DidNotReceiveWithAnyArgs().TryGet(default, out _);
    }

    [TestMethod]
    public void Decide_NoRoster_HasNoOpinionAndNeverAsks()
    {
        var service = Substitute.For<ISignatureStrikeService>();

        Assert.IsNull(SignatureStrikeVerdicts.Decide(service, null, null!, null!, default, default, knockdown: true));
        service.DidNotReceiveWithAnyArgs().DecideKnockdown(default);
    }

    [TestMethod]
    public void Decide_AttackerOffTheRoster_HasNoOpinionAndNeverAsks()
    {
        var service = Substitute.For<ISignatureStrikeService>();
        var roster = Substitute.For<ISignatureAgentRoster>();

        Assert.IsNull(SignatureStrikeVerdicts.Decide(service, roster, null!, null!, default, default, knockdown: true));
        Assert.IsNull(SignatureStrikeVerdicts.Decide(service, roster, null!, null!, default, default, knockdown: false));
        service.DidNotReceiveWithAnyArgs().DecideKnockdown(default);
        service.DidNotReceiveWithAnyArgs().DecideKnockback(default);
    }

    [TestMethod]
    public void Decide_HorseCharge_HasNoOpinionAndNeverProbesTheRoster()
    {
        var service = Substitute.For<ISignatureStrikeService>();
        var roster = Substitute.For<ISignatureAgentRoster>();
        var charge = CollisionDataFixture.With(chargeVelocity: 6f);

        Assert.IsNull(SignatureStrikeVerdicts.Decide(service, roster, null!, null!, charge, default, knockdown: true));
        Assert.IsNull(SignatureStrikeVerdicts.Decide(service, roster, null!, null!, charge, default, knockdown: false));
        roster.DidNotReceiveWithAnyArgs().TryGet(default, out _);
    }

    // IsHorseCharge is ChargeVelocity > 0f, so a NaN velocity is a melee hit, as the model routed it before #737.
    [TestMethod]
    public void Decide_NaNChargeVelocity_IsAMeleeHitAndProbesTheRoster()
    {
        var service = Substitute.For<ISignatureStrikeService>();
        var roster = Substitute.For<ISignatureAgentRoster>();

        SignatureStrikeVerdicts.Decide(service, roster, null!, null!, CollisionDataFixture.With(chargeVelocity: float.NaN), default, knockdown: true);

        roster.ReceivedWithAnyArgs(1).TryGet(default, out _);
    }

    [TestMethod]
    public void Decide_ProbesTheRosterWithTheAttacker()
    {
        var attacker = BareAgent();
        var victim = BareAgent();
        var roster = Substitute.For<ISignatureAgentRoster>();

        SignatureStrikeVerdicts.Decide(Substitute.For<ISignatureStrikeService>(), roster, attacker, victim, MeleeHit(), default, knockdown: true);

        roster.Received(1).TryGet(attacker, out _);
        roster.DidNotReceive().TryGet(victim, out _);
    }

    [TestMethod]
    public void Decide_RosteredAttackerKnockdown_AsksTheKnockdownVerdict()
    {
        var attacker = BareAgent();
        var service = Substitute.For<ISignatureStrikeService>();
        service.DecideKnockdown(Arg.Any<StrikeContext>()).Returns(true);

        var verdict = SignatureStrikeVerdicts.Decide(service, RosterWith(attacker), attacker, null!, MeleeHit(), default, knockdown: true);

        Assert.AreEqual(true, verdict);
        service.DidNotReceiveWithAnyArgs().DecideKnockback(default);
    }

    [TestMethod]
    public void Decide_RosteredAttackerKnockback_AsksTheKnockbackVerdict()
    {
        var attacker = BareAgent();
        var service = Substitute.For<ISignatureStrikeService>();
        service.DecideKnockback(Arg.Any<StrikeContext>()).Returns(true);

        var verdict = SignatureStrikeVerdicts.Decide(service, RosterWith(attacker), attacker, null!, MeleeHit(), default, knockdown: false);

        Assert.AreEqual(true, verdict);
        service.DidNotReceiveWithAnyArgs().DecideKnockdown(default);
    }
}
