using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// Pins <see cref="ActionIndexCacheRepair.ShouldRepair"/>, the one part of the repair that is pure
/// policy rather than engine interaction. The rules it encodes each prevent a specific way of
/// making things worse: writing over a healthy value, "repairing" the act_none sentinel (which is
/// legitimately -1), and writing a -1 back over a -1 for an action this engine build does not have.
/// </summary>
[TestClass]
public class ActionIndexCacheRepairTests
{
    [TestMethod]
    public void ShouldRepair_PoisonedFieldWithResolvableAction_ReturnsTrue()
    {
        Assert.IsTrue(ActionIndexCacheRepair.ShouldRepair("act_inventory_idle_start", currentIndex: -1, resolvedIndex: 4008));
    }

    [TestMethod]
    public void ShouldRepair_HealthyField_ReturnsFalse()
    {
        // Never overwrite a value that already resolved — that is the common case and must be a no-op.
        Assert.IsFalse(ActionIndexCacheRepair.ShouldRepair("act_inventory_idle_start", currentIndex: 4008, resolvedIndex: 4008));
    }

    [TestMethod]
    public void ShouldRepair_ActNoneSentinel_ReturnsFalse()
    {
        // act_none is -1 BY DESIGN; ActionIndexCache.GetName() relies on it. Repairing it would
        // corrupt the sentinel every "is this action unset?" comparison in the engine depends on.
        Assert.IsFalse(ActionIndexCacheRepair.ShouldRepair("act_none", currentIndex: -1, resolvedIndex: -1));
        Assert.IsFalse(ActionIndexCacheRepair.ShouldRepair("act_none", currentIndex: -1, resolvedIndex: 42));
    }

    [TestMethod]
    public void ShouldRepair_ActionUnknownToThisEngineBuild_ReturnsFalse()
    {
        // A live lookup that also returns -1 means the action does not exist here. Writing it back
        // would be a pointless reflection write on an initonly field.
        Assert.IsFalse(ActionIndexCacheRepair.ShouldRepair("act_removed_in_this_version", currentIndex: -1, resolvedIndex: -1));
    }

    [TestMethod]
    public void ShouldRepair_EmptyOrNullFieldName_ReturnsFalse()
    {
        Assert.IsFalse(ActionIndexCacheRepair.ShouldRepair(null!, currentIndex: -1, resolvedIndex: 1));
        Assert.IsFalse(ActionIndexCacheRepair.ShouldRepair(string.Empty, currentIndex: -1, resolvedIndex: 1));
        Assert.IsFalse(ActionIndexCacheRepair.ShouldRepair("   ", currentIndex: -1, resolvedIndex: 1));
    }

    // ── ResolveActionName ────────────────────────────────────────────────────────────────────
    // The repair looks an action up by FIELD NAME, which assumes field name == action name. That
    // assumption is false in v1.4.7 for exactly one field, found by diffing all 214 Create() call
    // sites in the engine's static constructor against their target fields. Without the override the
    // field resolves to -1, is misreported as "unknown to this engine build", and stays poisoned.

    [TestMethod]
    public void ResolveActionName_KnownDivergentField_ReturnsMappedActionName()
    {
        // Engine cctor: act_raid_jump = Create("act_raid_jump_1"). There is no action named
        // "act_raid_jump" in Native/ModuleData/action_types.xml.
        Assert.AreEqual("act_raid_jump_1", ActionIndexCacheRepair.ResolveActionName("act_raid_jump"));
    }

    [TestMethod]
    public void ResolveActionName_OrdinaryField_ReturnsFieldNameUnchanged()
    {
        Assert.AreEqual("act_inventory_idle_start", ActionIndexCacheRepair.ResolveActionName("act_inventory_idle_start"));
        Assert.AreEqual("act_none", ActionIndexCacheRepair.ResolveActionName("act_none"));
    }

    [TestMethod]
    public void ResolveActionName_NullOrEmpty_ReturnsInputUnchanged()
    {
        Assert.IsNull(ActionIndexCacheRepair.ResolveActionName(null!));
        Assert.AreEqual(string.Empty, ActionIndexCacheRepair.ResolveActionName(string.Empty));
    }
}
