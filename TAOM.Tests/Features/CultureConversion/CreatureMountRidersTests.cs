using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureConversion.GarrisonSwap.Domain;

namespace TAOM.Tests.Features.CultureConversion;

/// <summary>
/// Which mounts keep a troop from ever replacing a converted garrison stack (Mike 2026-09-29): the mount-locked
/// creatures, the same three <c>TaomAgentStatCalculateModel.CanAgentRideMount</c> refuses. War rams and elk are
/// horse-skeleton cavalry and stay eligible; their rows use TAOM's real Monster ids (lotr_monster_war_ram.xml,
/// LOTRAOM_horses.xml) so a rule like "every taom_ Monster" fails here.
/// </summary>
[TestClass]
public class CreatureMountRidersTests
{
    [DataTestMethod]
    [DataRow("spider")]
    [DataRow("taom_war_elephant")]
    [DataRow("taom_mumakil")]
    public void IsLockedCreatureMonster_MountLockedCreature_IsTrue(string monsterId)
    {
        Assert.IsTrue(CreatureMountRiders.IsLockedCreatureMonster(monsterId));
    }

    [DataTestMethod]
    [DataRow("horse")]
    [DataRow("camel")]
    [DataRow("warg")]
    [DataRow("taom_war_ram")]
    [DataRow("taom_elk")]
    [DataRow("taom_animalia_elk")]
    [DataRow("taom_animalia_moose")]
    [DataRow("spider_statue")]
    [DataRow("")]
    [DataRow(null)]
    public void IsLockedCreatureMonster_OtherMountOrNone_IsFalse(string? monsterId)
    {
        Assert.IsFalse(CreatureMountRiders.IsLockedCreatureMonster(monsterId));
    }
}
