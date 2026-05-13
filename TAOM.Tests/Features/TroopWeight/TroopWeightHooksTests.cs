using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.TroopWeight;
using TAOM.Features.TroopWeight.Hooks;

namespace TAOM.Tests.Features.TroopWeight;

// ADR-008 minimum-coverage tests for the four IOn* hook implementations that the Phase 7 audit
// (issue #195) flagged as having zero tests. The hooks themselves accept sealed TaleWorlds types
// (PartyBase, MBBindingList<PartyCharacterVM>, RecruitmentVM) and use static engine APIs
// (MBTextManager.SetTextVariable) inside their bodies — full behavior tests would require an
// adapter refactor (ADR-007) that the audit explicitly placed out of scope for #195.
//
// What we CAN test without engine state:
//   - Construction with all deps (catches a future ctor-signature drift the production code
//     wouldn't notice until IoC resolution at startup)
//   - Interface implementation (catches a future rename that breaks the IoC.ResolveAll<T>() loop
//     used by each patch's static dispatcher)
//   - Null-receiver tolerance for the two PartyBase hooks (the production try/catch suggests this
//     is a real call path — assert the early-exit guard works without exception)
//
// What we deliberately don't test here (deferred to an ADR-007 refactor session):
//   - PartyVMPopulatePartyListLabelHook behavior with non-empty MBBindingList — would need engine init
//   - RecruitmentVMRefreshPartyPropertiesHook behavior — would need RecruitmentVM ctor + state
[TestClass]
public class TroopWeightHooksTests
{
    private ITroopWeightService _service = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<ITroopWeightService>();
        _logger = Substitute.For<IModLogger>();
    }

    // --- PartyBaseNumberOfAllMembersHook ---

    [TestMethod]
    public void PartyBaseNumberOfAllMembersHook_Constructs()
    {
        var hook = new PartyBaseNumberOfAllMembersHook(_service, _logger);
        Assert.IsNotNull(hook);
    }

    [TestMethod]
    public void PartyBaseNumberOfAllMembersHook_ImplementsInterface()
    {
        var hook = new PartyBaseNumberOfAllMembersHook(_service, _logger);
        Assert.IsInstanceOfType(hook, typeof(IOnPartyBaseNumberOfAllMembers));
    }

    [TestMethod]
    public void PartyBaseNumberOfAllMembersHook_NullPartyBase_DoesNotThrow()
    {
        var hook = new PartyBaseNumberOfAllMembersHook(_service, _logger);
        int result = 42;

        // Audit issue #195: the production hook catches all exceptions (try/catch wraps the whole
        // body), but null receiver should hit the explicit early-exit guard at the top — no
        // exception path. Assert the result is preserved unchanged (early exit, no mutation).
        hook.OnPartyBaseNumberOfAllMembers(null, ref result);

        Assert.AreEqual(42, result, "Null partyBase must early-exit without mutating __result");
    }

    // --- PartyBaseNumberOfRegularMembersHook ---

    [TestMethod]
    public void PartyBaseNumberOfRegularMembersHook_Constructs()
    {
        var hook = new PartyBaseNumberOfRegularMembersHook(_service, _logger);
        Assert.IsNotNull(hook);
    }

    [TestMethod]
    public void PartyBaseNumberOfRegularMembersHook_ImplementsInterface()
    {
        var hook = new PartyBaseNumberOfRegularMembersHook(_service, _logger);
        Assert.IsInstanceOfType(hook, typeof(IOnPartyBaseNumberOfRegularMembers));
    }

    [TestMethod]
    public void PartyBaseNumberOfRegularMembersHook_NullPartyBase_DoesNotThrow()
    {
        var hook = new PartyBaseNumberOfRegularMembersHook(_service, _logger);
        int result = 99;

        hook.OnPartyBaseNumberOfRegularMembers(null, ref result);

        Assert.AreEqual(99, result, "Null partyBase must early-exit without mutating __result");
    }

    // --- PartyVMPopulatePartyListLabelHook ---

    [TestMethod]
    public void PartyVMPopulatePartyListLabelHook_Constructs()
    {
        var hook = new PartyVMPopulatePartyListLabelHook(_service, _logger);
        Assert.IsNotNull(hook);
    }

    [TestMethod]
    public void PartyVMPopulatePartyListLabelHook_ImplementsInterface()
    {
        var hook = new PartyVMPopulatePartyListLabelHook(_service, _logger);
        Assert.IsInstanceOfType(hook, typeof(IOnPartyVMPopulatePartyListLabel));
    }

    // --- RecruitmentVMRefreshPartyPropertiesHook ---

    [TestMethod]
    public void RecruitmentVMRefreshPartyPropertiesHook_Constructs()
    {
        var hook = new RecruitmentVMRefreshPartyPropertiesHook(_service, _logger);
        Assert.IsNotNull(hook);
    }

    [TestMethod]
    public void RecruitmentVMRefreshPartyPropertiesHook_ImplementsInterface()
    {
        var hook = new RecruitmentVMRefreshPartyPropertiesHook(_service, _logger);
        Assert.IsInstanceOfType(hook, typeof(IOnRecruitmentVMRefreshPartyProperties));
    }
}
