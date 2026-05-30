using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Attr = Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch.Attribute;

namespace TAOM.Features.Mcm.UI;

// MCM v5.11.4's embedded options-screen prefabs (inside Bannerlord.MBOptionScreen.v1.4.x.dll)
// still use LayoutImp.LayoutMethod="VerticalBottomToTop". v1.4.0 fixed the long-standing engine
// bug where VerticalBottomToTop actually rendered top-to-bottom, so these now render the entire
// settings list inverted (group headers below their members, members in reverse Order, groups in
// reverse GroupOrder). Same regression class as commit ad836d1, but in the MCM library's embedded
// prefabs — which the ad836d1 mass-flip never saw because it only scanned TAOM's loose XML.
//
// These four PrefabExtensionSetAttributePatch entries rewrite the offending ListPanels back to
// VerticalTopToBottom at prefab-load time. Auto-registered via SubModule's _uiExtender.Register.
// If a future MCM ships the corrected layout the patch sets the same value (idempotent); if MCM
// restructures the prefab the XPath simply stops matching and the patch no-ops (fail-safe).

// 1. SettingsView — top-level group stack. Renders groups in GroupOrder order.
[PrefabExtension("SettingsView", "descendant::ListPanel[@Id='SettingPropertiesList']")]
internal class McmSettingsViewGroupList : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("LayoutImp.LayoutMethod", "VerticalTopToBottom")
    };
}

// 2. SettingsPropertyGroupView — outer group container. Puts the header above its members.
[PrefabExtension("SettingsPropertyGroupView", "descendant::ListPanel[@IsVisible='@IsGroupVisible']")]
internal class McmGroupOuter : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("LayoutImp.LayoutMethod", "VerticalTopToBottom")
    };
}

// 3. SettingsPropertyGroupView — members list. Renders members in declared Order.
[PrefabExtension("SettingsPropertyGroupView", "descendant::ListPanel[@DataSource='{SettingProperties}']")]
internal class McmGroupProperties : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("LayoutImp.LayoutMethod", "VerticalTopToBottom")
    };
}

// 4. SettingsPropertyGroupView — subgroups list. Renders nested groups in declared GroupOrder.
[PrefabExtension("SettingsPropertyGroupView", "descendant::ListPanel[@DataSource='{SettingPropertyGroups}']")]
internal class McmGroupSubgroups : PrefabExtensionSetAttributePatch
{
    public override List<Attr> Attributes => new List<Attr>
    {
        new Attr("LayoutImp.LayoutMethod", "VerticalTopToBottom")
    };
}
