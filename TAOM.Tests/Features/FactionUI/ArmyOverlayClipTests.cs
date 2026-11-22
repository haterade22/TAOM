using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.Campaign;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #1532. The vanilla army panel slid under the TAOM stats plate, which is taller than the vanilla map bar.
/// The TAOM army panel is cut at the top of the plate's gold frame, and that edge moves with the plate's shown
/// expansion (CampaignStatsPanelWidget.ShownExpansion).
/// </summary>
[TestClass]
public class ArmyOverlayClipTests
{
    private const float Tolerance = 0.0001f;

    /// <summary>The closed stats plate's art starts 108 px above the screen edge (4 margin, 168 panel, 64 slide),
    /// drawn at 104/234 of cb_stats_upper.png. Its frame's top is source row 15 along the plate and row 37 on the
    /// lower right shoulder that holds the muster banner.</summary>
    internal static float FrameTopAt(int sourceRow) =>
        4f + CampaignStatsPanelWidget.PanelHeight - CampaignStatsPanelWidget.SlideDistance - sourceRow * 104f / 234f;

    [TestMethod]
    public void ClipBottom_ReachesIntoTheShouldersBorder()
    {
        // Above the shoulder the plate is clear: a cut higher than its border leaves a notch of map showing.
        float shoulder = FrameTopAt(37);
        float bottom = ArmyOverlayClipWidget.ClipBottom(0f);

        Assert.IsTrue(bottom <= shoulder, $"cut at {bottom} is above the shoulder's border top {shoulder}: a gap shows");
        Assert.IsTrue(bottom >= shoulder - 4f, $"cut at {bottom} is past the shoulder's border: the panel shows through");
    }

    [TestMethod]
    public void ClipBottom_RisesWithThePlateAsItExtends()
    {
        float rise = ArmyOverlayClipWidget.ClipBottom(1f) - ArmyOverlayClipWidget.ClipBottom(0f);

        Assert.AreEqual(CampaignStatsPanelWidget.SlideDistance, rise, Tolerance);
        Assert.AreEqual(ArmyOverlayClipWidget.ClipBottom(0f) + rise / 2f, ArmyOverlayClipWidget.ClipBottom(0.5f), Tolerance);
    }
}
