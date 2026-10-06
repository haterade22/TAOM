using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Hooks;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// The refuge defender reduction as the campaign damage model applies it (#507, moved out of the model by #737): the
/// victim's party id from its agent origin, the service's reduction for that party, then the shared (1 - r) contract.
/// A party origin needs a campaign, so these drive the null and non-party origins; the contract's own edge cases are
/// <c>RefugeDamageReductionTests</c>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class RefugeDamageHooksTests
{
    [TestMethod]
    public void Reduce_NoService_LeavesTheDamage()
        => Assert.AreEqual(40f, RefugeDamageHooks.Reduce(null, null!, 40f));

    [TestMethod]
    public void Reduce_ServiceGrantsAQuarter_ScalesTheFinalDamage()
    {
        var service = Substitute.For<IRefugeDefenseService>();
        service.DefenderDamageReduction(Arg.Any<string>()).Returns(0.25f);

        Assert.AreEqual(30f, RefugeDamageHooks.Reduce(service, null!, 40f), 0.0001f);
    }

    [TestMethod]
    public void Reduce_NaNReduction_LeavesTheDamage()
    {
        var service = Substitute.For<IRefugeDefenseService>();
        service.DefenderDamageReduction(Arg.Any<string>()).Returns(float.NaN);

        Assert.AreEqual(40f, RefugeDamageHooks.Reduce(service, null!, 40f));
    }

    [TestMethod]
    public void Reduce_OriginThatIsNoParty_AsksWithNoPartyId()
    {
        var service = Substitute.For<IRefugeDefenseService>();

        RefugeDamageHooks.Reduce(service, Substitute.For<IAgentOriginBase>(), 40f);

        service.Received(1).DefenderDamageReduction(null!);
    }
}
