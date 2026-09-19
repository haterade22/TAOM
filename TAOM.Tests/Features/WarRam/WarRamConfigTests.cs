using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.WarRam;

// War ram attack wiring, pinned. The ram's attack used to be the vanilla horse kick (act_horse_kick,
// bound in Native as_horse); since 2026-09-18 it is the bespoke head-butt act_war_ram_butt, bound only in
// LOTRLOME_Armory's as_war_ram (base_set as_horse) and typed actt_kick in the Armory's action_types.xml.
// These tests keep the C# side of that contract from drifting back to the vanilla action, and keep the
// cooldown longer than the clip (30 posed frames plus a 2.5 s hold with the head down, frames 1..105: 3.50 s at 30 fps),
// because the tree restarts the attack as soon as the cooldown clears and a restart mid-clip snaps the head up.

namespace TAOM.Tests.Features.WarRam;

[TestClass]
public class WarRamConfigTests
{
    /// <summary>Compiled master act_war_ram_butt: 106 frames including the rest frame, clip 1..105 at 30 fps.</summary>
    private const double HeadButtClipSeconds = 105.0 / 30.0;

    [TestMethod]
    public void AttackActionName_IsTheBespokeHeadButt_NotTheVanillaHorseKick()
    {
        Assert.AreEqual("act_war_ram_butt", WarRamConfig.AttackActionName);
    }

    [TestMethod]
    public void ActionSetId_IsTheArmorySetTheRamMonsterNames()
    {
        // The drift guard looks this set up by id at mission start (MBActionSet.GetActionSet), so it must match
        // action_set="as_war_ram" on the live lotr_monster_war_ram.xml.
        Assert.AreEqual("as_war_ram", WarRamConfig.ActionSetId);
    }

    [TestMethod]
    public void AllFourProfileSlots_HoldTheSameAttack_SoIsAttackMeansMidHeadButt()
    {
        Assert.AreEqual(WarRamConfig.AttackActionName, WarRamConfig.AttackAltActionName);
        Assert.AreEqual(WarRamConfig.AttackActionName, WarRamConfig.SideSlotLeftActionName);
        Assert.AreEqual(WarRamConfig.AttackActionName, WarRamConfig.SideSlotRightActionName);
    }

    [TestMethod]
    public void HeadButt_HitsOneEnemy_NotEveryoneInTheRadius()
    {
        // Mike, 2026-09-18: "the ram headbutt attack needs to only hit 1 person not AOE". The elephant and
        // mumakil tramples keep the radial sweep; only the ram's profile opts into a single target.
        Assert.IsTrue(WarRamConfig.AttackSingleTarget);
    }

    [TestMethod]
    public void AttackCooldown_OutlastsTheHeadButtClip_IncludingTheHold()
    {
        Assert.IsTrue(WarRamConfig.AttackCooldownSeconds > HeadButtClipSeconds,
            $"cooldown {WarRamConfig.AttackCooldownSeconds}s must exceed the {HeadButtClipSeconds:F2}s clip or the tree restarts the butt mid-hold");
    }
}
