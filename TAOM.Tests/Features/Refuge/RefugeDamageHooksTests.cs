using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Elephant;
using TAOM.Features.Mumakil;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Hooks;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// The refuge defender reduction as the campaign damage model applies it (#507, moved out of the model by #737): the
/// victim's party id from its agent origin, the service's reduction for that party, then the shared (1 - r) contract.
/// The party is read through the origin's <c>BattleCombatant</c>, so a bare party behind a substitute origin, or behind a
/// real howdah or mumak crew origin (#741), drives the same path a campaign battle does; the contract's own edge cases are
/// <c>RefugeDamageReductionTests</c>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class RefugeDamageHooksTests
{
    [TestMethod]
    public void Reduce_NoService_LeavesTheDamage()
        => Assert.AreEqual(40f, RefugeDamageHooks.Reduce(null, null, 40f));

    [TestMethod]
    public void Reduce_ServiceGrantsAQuarter_ScalesTheFinalDamage()
    {
        var service = Substitute.For<IRefugeDefenseService>();
        service.DefenderDamageReduction(Arg.Any<string>()).Returns(0.25f);

        Assert.AreEqual(30f, RefugeDamageHooks.Reduce(service, null, 40f), 0.0001f);
    }

    [TestMethod]
    public void Reduce_NaNReduction_LeavesTheDamage()
    {
        var service = Substitute.For<IRefugeDefenseService>();
        service.DefenderDamageReduction(Arg.Any<string>()).Returns(float.NaN);

        Assert.AreEqual(40f, RefugeDamageHooks.Reduce(service, null, 40f));
    }

    // A bare party: the hook reads only PartyBase.MobileParty (private setter, v1.5.4 PartyBase.cs:123, reached the way
    // RefugeCampaignBehaviorTests reaches it) and MobileParty.StringId.
    private static PartyBase PartyWithMobileId(string id)
    {
        var mobile = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        mobile.StringId = id;
        var party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
        typeof(PartyBase).GetProperty(nameof(PartyBase.MobileParty))!.SetValue(party, mobile);
        return party;
    }

    private static IAgentOriginBase OriginOf(PartyBase party)
    {
        var origin = Substitute.For<IAgentOriginBase>();
        origin.BattleCombatant.Returns(party);
        return origin;
    }

    private static IRefugeDefenseService RefugeFor(string partyId)
    {
        var service = Substitute.For<IRefugeDefenseService>();
        service.DefenderDamageReduction(partyId).Returns(0.25f);
        return service;
    }

    // #741: the crew's origin forwards BattleCombatant to the mahout's, so the crew shares its party's refuge.
    [TestMethod]
    public void Reduce_HowdahCrewOfARefugeParty_GetsTheReduction()
    {
        var crew = new HowdahCrewAgentOrigin(OriginOf(PartyWithMobileId("taom_refuge_0")), null, 7);

        Assert.AreEqual(30f, RefugeDamageHooks.Reduce(RefugeFor("taom_refuge_0"), crew, 40f), 0.0001f);
    }

    [TestMethod]
    public void Reduce_MumakCrewOfARefugeParty_GetsTheReduction()
    {
        var crew = new MumakilCrewAgentOrigin(OriginOf(PartyWithMobileId("taom_refuge_0")), null, 7);

        Assert.AreEqual(30f, RefugeDamageHooks.Reduce(RefugeFor("taom_refuge_0"), crew, 40f), 0.0001f);
    }

    // #741, mounts: a mount has no origin of its own, so its hit is credited to its rider's party, the branch the
    // career passives and vanilla's reductions take.
    [TestMethod]
    public void Reduce_MountOfARefugeRider_GetsTheReduction()
    {
        var hit = default(AttackInformation);
        hit.IsVictimAgentMount = true;
        hit.VictimRiderAgentOrigin = OriginOf(PartyWithMobileId("taom_refuge_0"));

        Assert.AreEqual(30f, RefugeDamageHooks.Reduce(RefugeFor("taom_refuge_0"), in hit, 40f), 0.0001f);
    }

    [TestMethod]
    public void Reduce_RiderlessMount_GetsNothing()
    {
        var service = RefugeFor("taom_refuge_0");
        var hit = default(AttackInformation);
        hit.IsVictimAgentMount = true;

        Assert.AreEqual(40f, RefugeDamageHooks.Reduce(service, in hit, 40f));
        service.Received(1).DefenderDamageReduction(null);
    }

    [TestMethod]
    public void Reduce_FootVictim_ReadsItsOwnOriginNotARiders()
    {
        var hit = default(AttackInformation);
        hit.VictimAgentOrigin = OriginOf(PartyWithMobileId("taom_refuge_0"));
        hit.VictimRiderAgentOrigin = OriginOf(PartyWithMobileId("lord_party"));

        Assert.AreEqual(30f, RefugeDamageHooks.Reduce(RefugeFor("taom_refuge_0"), in hit, 40f), 0.0001f);
    }

    [TestMethod]
    public void Reduce_OriginOfAnotherParty_AsksForThatParty()
    {
        var service = RefugeFor("taom_refuge_0");

        Assert.AreEqual(40f, RefugeDamageHooks.Reduce(service, OriginOf(PartyWithMobileId("lord_party")), 40f));
        service.Received(1).DefenderDamageReduction("lord_party");
    }

    [TestMethod]
    public void Reduce_OriginThatIsNoParty_AsksWithNoPartyId()
    {
        var service = Substitute.For<IRefugeDefenseService>();

        RefugeDamageHooks.Reduce(service, Substitute.For<IAgentOriginBase>(), 40f);

        service.Received(1).DefenderDamageReduction(null);
    }
}
