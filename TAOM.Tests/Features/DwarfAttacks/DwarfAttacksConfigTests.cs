using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Features.DwarfAttacks.DwarfAttacksConfig;

// Tuning invariants for the dwarf attacks (#1087): the impact fractions match the clips
// tools/gen_dwarf_attack_clips.ps1 wrote, the two trigger bands never overlap, each attack's hit reach covers its
// trigger band, and each cooldown outlasts its clip so a dwarf never chains the same attack.

namespace TAOM.Tests.Features.DwarfAttacks;

[TestClass]
public class DwarfAttacksConfigTests
{
    // The clips' masters, keyed in tools/blender/author_dwarf_attack_clips.py: 37 and 55 frames at 30 fps, played as
    // Source 1 .. frames-1, so progress at frame f is (f - 1) / (frames - 2).
    private const int HeadButtFrames = 37, HeadButtImpactFrame = 13;
    private const int DropKickFrames = 55, DropKickImpactFrame = 21;

    [TestMethod]
    public void ImpactFractions_MatchTheAuthoredImpactFrames()
    {
        Assert.AreEqual((HeadButtImpactFrame - 1f) / (HeadButtFrames - 2f), HeadButtImpactFraction, 0.001f);
        Assert.AreEqual((DropKickImpactFrame - 1f) / (DropKickFrames - 2f), DropKickImpactFraction, 0.001f);
    }

    [TestMethod]
    public void TriggerBands_AreOrderedAndDoNotOverlap()
    {
        Assert.IsTrue(HeadButtMaxRange > 0f);
        Assert.IsTrue(HeadButtMaxRange < DropKickMinRange);
        Assert.IsTrue(DropKickMinRange < DropKickMaxRange);
    }

    [TestMethod]
    public void HeadButtReach_CoversItsTriggerBand()
    {
        // The butt is planted: the victim is where it stood when the butt began, give or take a step.
        Assert.IsTrue(HeadButtReach >= HeadButtMaxRange);
    }

    [TestMethod]
    public void DropKick_TravelPlusReach_CoversItsTriggerBand()
    {
        // The clip carries the dwarf DropKickTravel metres by progress DropKickTravelEndFraction, linearly
        // (gen_dwarf_attack_clips.ps1); by the impact he has covered that share of it.
        float travelAtImpact = DropKickTravel * DropKickImpactFraction / DropKickTravelEndFraction;
        Assert.IsTrue(travelAtImpact + DropKickReach >= DropKickMaxRange,
            $"a victim at the far edge of the band ({DropKickMaxRange} m) is out of reach at impact ({travelAtImpact + DropKickReach:0.00} m)");
    }

    [TestMethod]
    public void Cooldowns_OutlastTheirClips()
    {
        Assert.IsTrue(HeadButtCooldownSeconds > HeadButtFrames / 30f);
        Assert.IsTrue(DropKickCooldownSeconds > DropKickFrames / 30f);
    }

    [TestMethod]
    public void FacingGates_AreCosinesInsideTheFrontHalf()
    {
        Assert.IsTrue(FacingDot > 0f && FacingDot < 1f);
        Assert.IsTrue(HitFacingDot > 0f && HitFacingDot < 1f);
    }

    [TestMethod]
    public void DamagePercent_DefaultSitsInsideItsRange()
    {
        Assert.IsTrue(DefaultDamagePercent > 0 && DefaultDamagePercent <= MaxDamagePercent);
    }
}
