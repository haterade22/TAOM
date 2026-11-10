using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOM.Features.CareerSystem.UI;

// Issues #382 and #1095: the circular battle HUD, in place of the native health cluster, inside
// Native's AgentStatus prefab. Living inside that prefab is the point: it inherits the root's
// IsVisible="@IsCombatUIActive" and the prioritized/passive slide, so it appears, hides and moves
// exactly with the combat HUD. Its own IsVisible follows native ShowAgentHealthBar, the flag that
// showed the native health bar it replaces (BattleHudNativeSuppressionPrefabs hides that bar).
//
// Container safety (gui-ui.md checklist, verified 2026-08-05 against the installed v1.4.7 DLLs):
// MissionGauntletAgentStatus does no widget-child traversal at all, and the only positional/typed
// child access in the mission HUD widget assembly is the damage-feed ListPanel, which this patch does
// not touch. The insertion parent is the plain Widget[@VisualDefinition='AgentStatus'] container.
//
// The ammo capsule (bh_ammo_capsule) is the first child, so the ring widget's backing disk and shell
// rim draw over its top and it tucks under the circle at 6 o'clock (BattleHudGeometry.AmmoCapsuleTop).
// Its number is native's own AgentAmmoTextWidget bound to AmmoCount and IsAmmoCountAlertEnabled, so the
// low-ammo alert is unchanged; the native counter is hidden (BattleHudHideNativeAmmoCounter).
// TaomBattleHudRingWidget draws the rings, rims, centre disk and leaf; the career glyph and the
// configured activation key sit on top. Every widget ignores events, so clicks pass to the scene.
[PrefabExtension("AgentStatus",
    "descendant::Widget[@VisualDefinition='AgentStatus']/Children")]
internal class CareerEnergyBarPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Child;

    // First child, as shipped: it draws under every vanilla sibling.
    public override int Index => 0;

    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument()
    {
        var doc = new XmlDocument();
        doc.LoadXml(@"
<Widget Id=""TaomBattleHud"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
        SuggestedWidth=""160"" SuggestedHeight=""160""
        HorizontalAlignment=""Right"" VerticalAlignment=""Bottom"" MarginRight=""28"" MarginBottom=""28""
        IsVisible=""@ShowAgentHealthBar"" DoNotAcceptEvents=""true"" DoNotPassEventsToChildren=""true"">
  <Children>
    <Widget Id=""TaomBattleHudAmmo"" DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
            SuggestedWidth=""110"" SuggestedHeight=""26.6"" HorizontalAlignment=""Center"" VerticalAlignment=""Top""
            PositionYOffset=""@HudAmmoTop"" Sprite=""bh_ammo_capsule"" IsVisible=""@HudShowAmmo"">
      <Children>
        <Widget DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                SuggestedWidth=""24"" SuggestedHeight=""24"" HorizontalAlignment=""Left"" VerticalAlignment=""Center""
                MarginLeft=""8"" Sprite=""bh_ammo_quiver"" IsVisible=""@HudAmmoIsQuiver"" />
        <Widget DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                SuggestedWidth=""24"" SuggestedHeight=""24"" HorizontalAlignment=""Left"" VerticalAlignment=""Center""
                MarginLeft=""8"" Sprite=""bh_ammo_javelin"" IsVisible=""@HudAmmoIsJavelin"" />
        <AgentAmmoTextWidget DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                SuggestedWidth=""48"" SuggestedHeight=""26.6"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                PositionXOffset=""@HudAmmoNumberOffsetX"" IntText=""@AmmoCount"" IsAlertEnabled=""@IsAmmoCountAlertEnabled""
                Brush=""AgentAmmoCount.Text"" Brush.FontSize=""22""
                Brush.TextHorizontalAlignment=""Center"" Brush.TextVerticalAlignment=""Center"" />
      </Children>
    </Widget>

    <TaomBattleHudRingWidget Id=""TaomBattleHudRings"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
        DoNotAcceptEvents=""true""
        HealthFill=""@HudHealthFill"" HorseFill=""@HudHorseFill"" ShieldFill=""@HudShieldFill""
        HasHorse=""@HudHasHorse"" HasShield=""@HudHasShield""
        HasAbility=""@HasCareerAbility"" AbilityFill=""@AbilityFill"" IsAbilityReady=""@IsAbilityReady""
        IsAbilityActive=""@IsAbilityActive"" IsAbilityCooldown=""@IsAbilityCooldown"" />

    <Widget DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
            SuggestedWidth=""31"" SuggestedHeight=""31"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""
            PositionYOffset=""-9"" Sprite=""@CareerGlyphSprite"" Color=""#F0E2C0FF"" IsVisible=""@HasCareerGlyph"" />

    <TextWidget DoNotAcceptEvents=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
                SuggestedWidth=""40"" SuggestedHeight=""18"" HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                PositionYOffset=""19"" Text=""@ActivationKeyText"" IsVisible=""@HasActivationKey""
                Brush=""Mission.MainAgentHUD.TakenDamageFeed.Text"" Brush.FontSize=""16""
                Brush.TextHorizontalAlignment=""Center"" Brush.TextVerticalAlignment=""Center""
                Brush.FontColor=""#F4E6C4FF"" />
  </Children>
</Widget>");
        return doc;
    }
}
