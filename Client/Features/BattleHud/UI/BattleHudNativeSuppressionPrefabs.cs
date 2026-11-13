using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Attr = Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch.Attribute;

namespace TAOM.Features.BattleHud.UI;

// Issue #1095: the circular HUD (CareerEnergyBarPrefab) replaces native AgentStatus.xml's three
// horizontal bars, so they are switched off here rather than drawn twice. Each AgentHealthWidget keeps
// its node and bindings for anything that reads it; only its display flag becomes constant false.
// The weapon and shield 3D previews (the PrimaryWeapon and OffhandWeapon ImageIdentifierWidgets) are
// shrunk to nothing. The primary keeps its node because the native ammo counter (AgentAmmoTextWidget,
// bound to the VM, not to the image) is its child; that counter is hidden, and the HUD's ammo capsule
// below the circle shows the count instead.
// Inventory, wielding, the lance, brace and troop-count row and the damage feed are untouched; the
// lance row only moves left so it does not overlap the HUD.

[PrefabExtension("AgentStatus", "descendant::AgentHealthWidget[@Id='HeroHealthWidget']")]
internal class BattleHudHideNativeHealthBar : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new() { new Attr("ShowHealthBar", "false"), new Attr("IsVisible", "false") };
}

[PrefabExtension("AgentStatus", "descendant::AgentHealthWidget[@Id='HorseHealthWidget']")]
internal class BattleHudHideNativeHorseBar : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new() { new Attr("ShowHealthBar", "false"), new Attr("IsVisible", "false") };
}

[PrefabExtension("AgentStatus", "descendant::AgentHealthWidget[@Id='ShieldHealthWidget']")]
internal class BattleHudHideNativeShieldBar : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new() { new Attr("ShowHealthBar", "false"), new Attr("IsVisible", "false") };
}

[PrefabExtension("AgentStatus", "descendant::ImageIdentifierWidget[@DataSource='{PrimaryWeapon}']")]
internal class BattleHudHidePrimaryWeaponPreview : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new() { new Attr("SuggestedWidth", "0"), new Attr("SuggestedHeight", "0") };
}

[PrefabExtension("AgentStatus", "descendant::ImageIdentifierWidget[@DataSource='{OffhandWeapon}']")]
internal class BattleHudHideOffhandWeaponPreview : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new() { new Attr("SuggestedWidth", "0"), new Attr("SuggestedHeight", "0") };
}

// The weapon column (native MarginRight 40, MarginBottom 118) now holds only the hidden previews and
// counter. Its margin is the HUD's 28 plus its 160 width plus a 10 unit gap.
[PrefabExtension("AgentStatus", "descendant::ListPanel[@MarginBottom='118']")]
internal class BattleHudMoveWeaponColumn : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new() { new Attr("MarginRight", "198"), new Attr("MarginBottom", "40") };
}

// The HUD's ammo capsule shows the same count and alert, so native's counter only stops drawing. The
// path goes through the primary preview so it never matches the capsule's own AgentAmmoTextWidget,
// whatever order UIExtenderEx applies the two patches in; the node and its bindings stay.
[PrefabExtension("AgentStatus",
    "descendant::ImageIdentifierWidget[@DataSource='{PrimaryWeapon}']/descendant::AgentAmmoTextWidget")]
internal class BattleHudHideNativeAmmoCounter : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new() { new Attr("IsVisible", "false") };
}

// Couch lance, spear brace, troop count and troop ammo row (native MarginRight 135, MarginBottom 190).
[PrefabExtension("AgentStatus", "descendant::ListPanel[@MarginBottom='190']")]
internal class BattleHudMoveLanceRow : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new() { new Attr("MarginRight", "206") };
}
