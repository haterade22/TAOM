using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// Pins <see cref="CreatureMounts.IsCreatureMount"/>, the predicate both dismount guards key on
/// (Patch47 <c>Agent_Die_SpiderDismount_Patch</c>, Patch48 <c>Agent_HandleBlowAux_SpiderDismountGuard_Patch</c>). A player CTD on v2.0.29.5 (2026-11-07) died
/// inside native <c>Agent.Die</c> from a warg bite in a battle of wargs and war rams while the guard
/// covered spiders alone.
/// </summary>
[TestClass]
public class CreatureMountsTests
{
    [DataTestMethod]
    [DataRow("horse")]
    [DataRow("horse_2")]
    [DataRow("camel")]
    [DataRow("mule")]
    public void IsCreatureMount_VanillaMount_ReturnsFalse(string monsterId)
    {
        Assert.IsFalse(CreatureMounts.IsCreatureMount(monsterId));
    }

    [DataTestMethod]
    [DataRow("spider")]
    [DataRow("warg")]
    [DataRow("fell_warg")]
    [DataRow("taom_war_ram")]
    [DataRow("taom_elk")]
    [DataRow("taom_animalia_elk")]
    [DataRow("taom_animalia_moose")]
    [DataRow("taom_war_elephant")]
    [DataRow("taom_mumakil")]
    [DataRow("chariot")]
    [DataRow("dire_wolf")]
    [DataRow("taom_wildmesh_boar")]
    [DataRow("taom_wildmesh_deer")]
    [DataRow("taom_wildmesh_moose")]
    [DataRow("taom_wildmesh_mountaingoat")]
    [DataRow("taom_wildmesh_warthog")]
    public void IsCreatureMount_TaomCreature_ReturnsTrue(string monsterId)
    {
        Assert.IsTrue(CreatureMounts.IsCreatureMount(monsterId));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void IsCreatureMount_NoMonster_ReturnsFalse(string? monsterId)
    {
        Assert.IsFalse(CreatureMounts.IsCreatureMount(monsterId));
    }
}
