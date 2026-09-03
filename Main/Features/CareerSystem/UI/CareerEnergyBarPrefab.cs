using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.CareerSystem.UI;

// Issue #382 — the career ability energy bar, docked above the bottom-right health
// cluster inside Native's AgentStatus prefab. Living inside that prefab is the point:
// it inherits the root's IsVisible="@IsCombatUIActive" and the prioritized/passive
// shrink animation, so it appears and disappears exactly with the combat HUD.
//
// Container safety (gui-ui.md checklist, verified 2026-08-05 against the installed
// v1.4.7 DLLs): MissionGauntletAgentStatus does no widget-child traversal at all, and
// the only positional/typed child access in the mission HUD widget assembly is the
// damage-feed ListPanel (MissionAgentDamageFeedWidget.GetChild(i) typed cast) — which
// this patch does not touch. The insertion parent is the plain
// Widget[@VisualDefinition='AgentStatus'] container.
//
// Geometry + brush choices come from the externally-measured reference implementation
// (TAOM-Career-UX-Upstream-2026-08-05): native ShieldHealthBar Canvas/Frame/Fill brushes
// (a flat rectangle reads as misaligned at every offset — the native fill brush carries
// the bevel and rounded cap), fill insets L12/R10/Y4 with 169 usable units, MarginBottom
// 208 clearing the weapon-icon row, MarginRight 95 per the shipped reference XML.
[PrefabExtension("AgentStatus",
    "descendant::Widget[@VisualDefinition='AgentStatus']/Children")]
internal class CareerEnergyBarPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(@"
<Widget Id=""TaomCareerEnergyBar"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
        SuggestedWidth=""!Mission.MainAgentHUD.ShieldHealthBar.Frame.Width""
        SuggestedHeight=""!Mission.MainAgentHUD.ShieldHealthBar.Frame.Height""
        HorizontalAlignment=""Right"" VerticalAlignment=""Bottom""
        MarginRight=""95"" MarginBottom=""208""
        IsVisible=""@IsCareerBarVisible"" DoNotAcceptEvents=""true"">
  <Children>
    <BrushWidget Id=""TaomCareerBarCanvas"" DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                 SuggestedWidth=""!Mission.MainAgentHUD.ShieldHealthBar.Canvas.Width""
                 SuggestedHeight=""!Mission.MainAgentHUD.ShieldHealthBar.Canvas.Height""
                 HorizontalAlignment=""Left"" VerticalAlignment=""Center""
                 Brush=""Mission.MainAgentHUD.ShieldHealthBar.Canvas"">
      <Children>
        <Widget DoNotAcceptEvents=""true"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""Fixed""
                SuggestedHeight=""!Mission.MainAgentHUD.ShieldHealthBar.Fill.Height""
                HorizontalAlignment=""Left"" VerticalAlignment=""Center"" PositionYOffset=""4""
                MarginLeft=""12"" MarginRight=""10"" Sprite=""BlankWhiteSquare_9"" Color=""#0B0A08C8"" />
        <BrushWidget DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                     SuggestedWidth=""@BarFillWidth"" SuggestedHeight=""!Mission.MainAgentHUD.ShieldHealthBar.Fill.Height""
                     HorizontalAlignment=""Left"" VerticalAlignment=""Center"" PositionYOffset=""4"" MarginLeft=""12""
                     Brush=""Mission.MainAgentHUD.ShieldHealthBar.Fill"" Color=""#B8912FFF"" IsVisible=""@IsBarReady"" />
        <BrushWidget DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                     SuggestedWidth=""@BarFillWidth"" SuggestedHeight=""!Mission.MainAgentHUD.ShieldHealthBar.Fill.Height""
                     HorizontalAlignment=""Left"" VerticalAlignment=""Center"" PositionYOffset=""4"" MarginLeft=""12""
                     Brush=""Mission.MainAgentHUD.ShieldHealthBar.Fill"" Color=""#FACC61FF"" IsVisible=""@IsBarActive"" />
        <BrushWidget DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                     SuggestedWidth=""@BarFillWidth"" SuggestedHeight=""!Mission.MainAgentHUD.ShieldHealthBar.Fill.Height""
                     HorizontalAlignment=""Left"" VerticalAlignment=""Center"" PositionYOffset=""4"" MarginLeft=""12""
                     Brush=""Mission.MainAgentHUD.ShieldHealthBar.Fill"" Color=""#574726FF"" IsVisible=""@IsBarCooldown"" />
      </Children>
    </BrushWidget>

    <BrushWidget DoNotAcceptEvents=""true"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                 Brush=""Mission.MainAgentHUD.ShieldHealthBar.Frame"" />

    <Widget DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
            SuggestedWidth=""58"" SuggestedHeight=""58"" VerticalAlignment=""Center"" HorizontalAlignment=""Left""
            PositionXOffset=""-38"" IsVisible=""@HasCareerGlyph"">
      <Children>
        <Widget DoNotAcceptEvents=""true"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                Sprite=""BlankWhiteCircle"" Color=""#8A7038FF"" />
        <Widget DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                SuggestedWidth=""50"" SuggestedHeight=""50"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                Sprite=""BlankWhiteCircle"" Color=""#14120EFF"" />
        <Widget DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                SuggestedWidth=""36"" SuggestedHeight=""36"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                Sprite=""@CareerGlyphSprite"" Color=""#B9A87CFF"" />
      </Children>
    </Widget>

    <Widget DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
            SuggestedWidth=""30"" SuggestedHeight=""22"" HorizontalAlignment=""Right"" VerticalAlignment=""Center""
            PositionXOffset=""34"" Sprite=""BlankWhiteSquare_9"" Color=""#2B2620FF""
            IsVisible=""@HasActivationKey"">
      <Children>
        <TextWidget DoNotAcceptEvents=""true"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                    Text=""@ActivationKeyText"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                    Brush=""Mission.MainAgentHUD.TakenDamageFeed.Text"" Brush.FontSize=""18""
                    Brush.TextHorizontalAlignment=""Center"" Brush.TextVerticalAlignment=""Center""
                    Brush.FontColor=""#E8C877FF"" />
      </Children>
    </Widget>
  </Children>
</Widget>");
        return doc;
    }
}
