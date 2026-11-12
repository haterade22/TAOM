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
    // assumption is false for three fields (act_raid_jump, and the two wreckage fields from v1.5.3 at
    // the latest), found by diffing every Create() call site in the engine's static constructor
    // against its target field. Without the override a field resolves to -1, is misreported as
    // "unknown to this engine build", and stays poisoned. ActionIndexCacheRepairEngineTests diffs the
    // installed engine against the map.

    [TestMethod]
    public void ResolveActionName_KnownDivergentField_ReturnsMappedActionName()
    {
        // Engine cctor: act_raid_jump = Create("act_raid_jump_1"). There is no action named
        // "act_raid_jump" in Native/ModuleData/action_types.xml.
        Assert.AreEqual("act_raid_jump_1", ActionIndexCacheRepair.ResolveActionName("act_raid_jump"));
    }

    [TestMethod]
    [DataRow("act_wreckage_death_01", "act_cutscene_main_hero_battle_death_01")]
    [DataRow("act_wreckage_death_02", "act_cutscene_main_hero_battle_death_02")]
    public void ResolveActionName_WreckageDeathFields_ReturnMappedActionName(string field, string expected)
    {
        // cctor (v1.5.3 to v1.5.7): act_wreckage_death_01 = Create("act_cutscene_main_hero_battle_death_01"),
        // and the same for _02. Neither field name is an action in action_types.xml, so without the
        // override both stayed at -1 and were logged as "unknown to this engine build".
        Assert.AreEqual(expected, ActionIndexCacheRepair.ResolveActionName(field));
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
