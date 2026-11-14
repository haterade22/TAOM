using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.Campaign;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The perk timeline places the hero's real skill level between the native perk tiers: the marker, the
/// reached and locked tiers and the next tier come only from the native SkillVM.Level and PerkVM.Level.
/// </summary>
[TestClass]
public class CharacterSkillProgressTests
{
    // Vanilla skills hold a perk tier every 25 levels from 25 to 300.
    private static readonly int[] Tiers = Enumerable.Range(1, 12).Select(i => i * 25).ToArray();
    private static float Centre(int tier) => CharacterSkillProgress.TierCentre(tier);

    [TestMethod]
    public void TierCentre_IsTheMiddleOfThePerkColumn()
    {
        Assert.AreEqual(CharacterPerkItemVM.ColumnX(3) + CharacterPerkItemVM.ColumnWidth / 2f, Centre(3), 0.01f);
        Assert.AreEqual(126f + 3f * 252f, Centre(3), 0.01f, "D: 252 columns from the content's left edge");
    }

    // D: the gold fill starts at the rail's left end (RailInset) and ends at the exact skill interpolation,
    // so 177 ends just past the 175 node, never on it.
    [TestMethod]
    public void FillWidth_177_EndsAtTheExactMarker_JustPast175()
    {
        float content = CharacterScreenVM.PerkContentWidth(Tiers.Length);
        float marker = CharacterSkillProgress.MarkerX(Tiers, 177);
        float fill = CharacterSkillProgress.FillWidth(marker, content);
        Assert.AreEqual(marker, CharacterSkillProgress.RailInset + fill, 0.01f, "the fill ends at the marker");
        Assert.AreEqual(Centre(6) + 2f / 25f * 252f, CharacterSkillProgress.RailInset + fill, 0.01f, "20.16 past the 175 node");
    }

    [TestMethod]
    public void FillWidth_StaysOnTheRail()
    {
        float content = CharacterScreenVM.PerkContentWidth(5);
        Assert.AreEqual(0f, CharacterSkillProgress.FillWidth(0f, content), "nothing before the rail starts");
        Assert.AreEqual(0f, CharacterSkillProgress.FillWidth(10f, content), "a marker left of the rail start draws nothing");
        Assert.AreEqual(content - 2f * CharacterSkillProgress.RailInset, CharacterSkillProgress.FillWidth(content, content), 0.01f, "the fill stops at the rail's right end");
    }

    [TestMethod]
    public void FillWidth_NaNMarker_DrawsNothing()
    {
        Assert.AreEqual(0f, CharacterSkillProgress.FillWidth(float.NaN, 1260f));
    }

    [TestMethod]
    public void MarkerConstants_MatchTheApprovedDropMarker()
    {
        // d.js dRail: a 12 cs_tab_diamond centred on the fill's end, the number 10 right of that centre.
        Assert.AreEqual(12f, CharacterSkillProgress.MarkerDiamondSize);
        Assert.AreEqual(10f, CharacterSkillProgress.MarkerDiamondSize / 2f + CharacterSkillProgress.MarkerLabelGap, 0.01f);
    }

    [DataTestMethod]
    [DataRow(150, 5, DisplayName = "150 sits on the 150 tier")]
    [DataRow(175, 6, DisplayName = "175 sits on the 175 tier")]
    [DataRow(200, 7, DisplayName = "200 sits on the 200 tier")]
    public void MarkerX_LevelOnATier_SitsOnThatTier(int level, int tier)
    {
        Assert.AreEqual(Centre(tier), CharacterSkillProgress.MarkerX(Tiers, level), 0.01f);
    }

    [TestMethod]
    public void MarkerX_177_SitsBetween175And200_InProportion()
    {
        float x = CharacterSkillProgress.MarkerX(Tiers, 177);
        Assert.IsTrue(x > Centre(6) && x < Centre(7), "between the 175 and 200 columns");
        Assert.AreEqual(Centre(6) + 2f / 25f * CharacterPerkItemVM.ColumnWidth, x, 0.01f, "two levels of the 25 between the tiers");
    }

    [TestMethod]
    public void MarkerX_Zero_StartsTheTimeline()
    {
        Assert.AreEqual(0f, CharacterSkillProgress.MarkerX(Tiers, 0));
        Assert.AreEqual(Centre(0) * 10f / 25f, CharacterSkillProgress.MarkerX(Tiers, 10), 0.01f, "below the first tier, in proportion from 0");
    }

    [TestMethod]
    public void MarkerX_BeyondTheLastTier_StopsAtTheContentEnd()
    {
        float end = CharacterScreenVM.PerkContentWidth(Tiers.Length);
        Assert.AreEqual(Centre(11), CharacterSkillProgress.MarkerX(Tiers, 300), 0.01f);
        float beyond = CharacterSkillProgress.MarkerX(Tiers, 310);
        Assert.IsTrue(beyond > Centre(11) && beyond <= end, "past the last tier, still inside the content");
        Assert.AreEqual(end, CharacterSkillProgress.MarkerX(Tiers, 400), 0.01f, "clamped at the content end");
    }

    // The drawn marker (diamond and number) must lie inside the perk content, because the view clips at
    // the content edges: at 0 the diamond would hang off the left, past the last tier the number off the right.
    [DataTestMethod]
    [DataRow(0, DisplayName = "0: the start")]
    [DataRow(177, DisplayName = "177: between tiers")]
    [DataRow(300, DisplayName = "300: the last tier")]
    [DataRow(330, DisplayName = "330: past the last tier")]
    [DataRow(400, DisplayName = "400: far past the last tier")]
    public void Visual_DiamondAndNumber_StayInsideTheContent(int level)
    {
        float content = CharacterScreenVM.PerkContentWidth(Tiers.Length);
        var visual = CharacterSkillProgress.Visual(CharacterSkillProgress.MarkerX(Tiers, level), content);
        Assert.IsTrue(visual.DiamondLeft >= 0f && visual.DiamondLeft + CharacterSkillProgress.MarkerDiamondSize <= content,
            $"diamond {visual.DiamondLeft}..{visual.DiamondLeft + CharacterSkillProgress.MarkerDiamondSize} outside 0..{content}");
        Assert.IsTrue(visual.LabelLeft >= 0f && visual.LabelLeft + CharacterSkillProgress.MarkerLabelWidth <= content,
            $"number {visual.LabelLeft}..{visual.LabelLeft + CharacterSkillProgress.MarkerLabelWidth} outside 0..{content}");
        bool overlaps = visual.LabelLeft < visual.DiamondLeft + CharacterSkillProgress.MarkerDiamondSize
            && visual.DiamondLeft < visual.LabelLeft + CharacterSkillProgress.MarkerLabelWidth;
        Assert.IsFalse(overlaps, "the number never covers the diamond");
    }

    [TestMethod]
    public void Visual_177_CentresTheDiamondOnTheLevel_NumberToTheRight()
    {
        float content = CharacterScreenVM.PerkContentWidth(Tiers.Length);
        float marker = CharacterSkillProgress.MarkerX(Tiers, 177);
        var visual = CharacterSkillProgress.Visual(marker, content);
        Assert.AreEqual(marker, visual.DiamondLeft + CharacterSkillProgress.MarkerDiamondSize / 2f, 0.01f, "the diamond centre is the level");
        Assert.IsFalse(visual.LabelFlipped);
        Assert.IsTrue(visual.LabelLeft > visual.DiamondLeft, "the number follows the diamond");
    }

    [DataTestMethod]
    [DataRow(330)]
    [DataRow(400)]
    public void Visual_PastTheLastTier_PinsTheDiamondAtTheRailEnd_NumberFlipsLeft(int level)
    {
        float content = CharacterScreenVM.PerkContentWidth(Tiers.Length);
        float marker = CharacterSkillProgress.MarkerX(Tiers, level);
        var visual = CharacterSkillProgress.Visual(marker, content);
        Assert.AreEqual(content, marker, 0.01f, "the logical marker may run to the content end");
        Assert.AreEqual(content - CharacterSkillProgress.RailInset, DiamondCentre(visual), 0.01f, "the diamond sits on the rail's right end");
        Assert.IsTrue(visual.LabelFlipped && visual.LabelLeft < visual.DiamondLeft, "the number moves to the diamond's left");
    }

    [TestMethod]
    public void Visual_Zero_PinsTheDiamondAtTheRailStart()
    {
        var visual = CharacterSkillProgress.Visual(0f, CharacterScreenVM.PerkContentWidth(Tiers.Length));
        Assert.AreEqual(CharacterSkillProgress.RailInset, DiamondCentre(visual), 0.01f, "the diamond sits on the rail's left end");
        Assert.IsFalse(visual.LabelFlipped);
    }

    // Review fix: the drawn marker and the gold fill share one rail-bound endpoint, so at level 0 and at the
    // capped maximum the diamond sits on the fill's end, never 18 px off the rail; 177 keeps its exact place.
    [DataTestMethod]
    [DataRow(0, DisplayName = "0: the rail start")]
    [DataRow(10, DisplayName = "10: left of the rail start")]
    [DataRow(177, DisplayName = "177: between tiers")]
    [DataRow(300, DisplayName = "300: the last tier")]
    [DataRow(400, DisplayName = "400: the capped maximum")]
    public void Visual_DiamondCentre_IsTheFillEnd(int level)
    {
        float content = CharacterScreenVM.PerkContentWidth(Tiers.Length);
        float marker = CharacterSkillProgress.MarkerX(Tiers, level);
        var visual = CharacterSkillProgress.Visual(marker, content);
        float fillEnd = CharacterSkillProgress.RailInset + CharacterSkillProgress.FillWidth(marker, content);
        Assert.AreEqual(fillEnd, DiamondCentre(visual), 0.01f);
        Assert.IsTrue(DiamondCentre(visual) >= CharacterSkillProgress.RailInset
            && DiamondCentre(visual) <= content - CharacterSkillProgress.RailInset, "on the rail");
    }

    [TestMethod]
    public void Visual_177_KeepsTheExactInterpolation()
    {
        float content = CharacterScreenVM.PerkContentWidth(Tiers.Length);
        var visual = CharacterSkillProgress.Visual(CharacterSkillProgress.MarkerX(Tiers, 177), content);
        Assert.AreEqual(Centre(6) + 2f / 25f * 252f, DiamondCentre(visual), 0.01f, "20.16 past the 175 node");
    }

    [DataTestMethod]
    [DataRow(0f, DisplayName = "empty content")]
    [DataRow(-5f, DisplayName = "negative content")]
    [DataRow(30f, DisplayName = "content narrower than the two insets")]
    [DataRow(float.NaN, DisplayName = "NaN content")]
    public void RailEnd_EmptyOrNarrowContent_GivesFiniteCoordinatesAndNoFill(float content)
    {
        foreach (float marker in new[] { 0f, 177f, 5000f, float.NaN })
        {
            var visual = CharacterSkillProgress.Visual(marker, content);
            float fill = CharacterSkillProgress.FillWidth(marker, content);
            Assert.IsFalse(float.IsNaN(visual.DiamondLeft) || float.IsInfinity(visual.DiamondLeft), $"diamond {visual.DiamondLeft}");
            Assert.IsFalse(float.IsNaN(visual.LabelLeft) || float.IsInfinity(visual.LabelLeft), $"number {visual.LabelLeft}");
            Assert.AreEqual(0f, fill, $"no rail to fill at content {content}, marker {marker}");
        }
    }

    [TestMethod]
    public void RailEnd_NarrowContent_CentresTheDiamondInsideIt()
    {
        var visual = CharacterSkillProgress.Visual(500f, 30f);
        Assert.AreEqual(15f, DiamondCentre(visual), 0.01f);
        Assert.IsTrue(visual.DiamondLeft >= 0f && visual.DiamondLeft + CharacterSkillProgress.MarkerDiamondSize <= 30f);
    }

    [TestMethod]
    public void Visual_NaNMarker_SitsAtTheRailStart()
    {
        var visual = CharacterSkillProgress.Visual(float.NaN, 1260f);
        Assert.AreEqual(CharacterSkillProgress.RailInset, DiamondCentre(visual), 0.01f);
    }

    private static float DiamondCentre(CharacterSkillProgress.MarkerVisual visual) =>
        visual.DiamondLeft + CharacterSkillProgress.MarkerDiamondSize / 2f;

    [TestMethod]
    public void Visual_FiveTiers_PastTheLast_StaysInsideTheView()
    {
        var five = new[] { 25, 50, 75, 100, 125 };
        float content = CharacterScreenVM.PerkContentWidth(five.Length);
        var visual = CharacterSkillProgress.Visual(CharacterSkillProgress.MarkerX(five, 260), content);
        Assert.IsTrue(visual.DiamondLeft + CharacterSkillProgress.MarkerDiamondSize <= content);
        Assert.IsTrue(visual.LabelLeft + CharacterSkillProgress.MarkerLabelWidth <= content);
    }

    [TestMethod]
    public void MarkerX_NoTiers_IsZero()
    {
        Assert.AreEqual(0f, CharacterSkillProgress.MarkerX(new int[0], 177));
    }

    [DataTestMethod]
    [DataRow(150, 177, true, DisplayName = "150 reached at 177")]
    [DataRow(175, 177, true, DisplayName = "175 reached at 177")]
    [DataRow(200, 177, false, DisplayName = "200 locked at 177")]
    [DataRow(200, 200, true, DisplayName = "200 reached at exactly 200")]
    [DataRow(25, 0, false, DisplayName = "every tier locked at 0")]
    public void IsReached_TheSkillLevelMeetsTheTier(int tier, int level, bool reached)
    {
        Assert.AreEqual(reached, CharacterSkillProgress.IsReached(tier, level));
    }

    [TestMethod]
    public void ScrollFraction_ShowsTheMarkerInTheMiddleOfTheView()
    {
        float content = CharacterScreenVM.PerkContentWidth(Tiers.Length);
        float marker = CharacterSkillProgress.MarkerX(Tiers, 177);
        float view = CharacterScreenVM.PerkViewWidth;
        float fraction = CharacterSkillProgress.ScrollFraction(marker, content, view);
        float offset = fraction * (content - view);
        Assert.AreEqual(marker - view / 2f, offset, 0.5f, "the 177 marker at the view centre");
        Assert.AreEqual(0f, CharacterSkillProgress.ScrollFraction(CharacterSkillProgress.MarkerX(Tiers, 0), content, view), "0 stays at the start");
        Assert.AreEqual(1f, CharacterSkillProgress.ScrollFraction(CharacterSkillProgress.MarkerX(Tiers, 330), content, view), "beyond the last tier shows the end");
        Assert.AreEqual(0f, CharacterSkillProgress.ScrollFraction(600f, view, view), "five tiers never scroll");
    }

    [TestMethod]
    public void ScrollRequest_AppliesOnce_SoManualScrollingIsKept()
    {
        // The VM raises the request only when the inspected skill changes (a skill or member switch);
        // every other per-frame sync, and a reset, keeps the request, so a manual scroll is never undone.
        Assert.AreEqual(55f, CharacterPerkScrollbarWidget.RequestedValue(1, 0, 0f, 100f, 0.55f)!.Value, 0.001f, "a new request scrolls");
        Assert.IsNull(CharacterPerkScrollbarWidget.RequestedValue(1, 1, 0f, 100f, 0.55f), "an applied request never scrolls again");
        Assert.IsNull(CharacterPerkScrollbarWidget.RequestedValue(2, 1, 0f, 0f, 0.55f), "waits until the panel can scroll");
        Assert.AreEqual(30f, CharacterPerkScrollbarWidget.RequestedValue(2, 1, 10f, 30f, 1f)!.Value, 0.001f, "maps into the bar's own range");
        Assert.AreEqual(10f, CharacterPerkScrollbarWidget.RequestedValue(2, 1, 10f, 30f, float.NaN)!.Value, 0.001f, "NaN scrolls to the start");
    }
}
