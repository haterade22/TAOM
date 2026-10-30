using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace TAOM.Features.ShadowfaxScreens.Inventory.Hooks;

// Verified on the installed v1.5.5: private void SPInventoryVM.InitializeInventory() sorts both sides with
// `OrderBy<ItemRosterElement,string>(...)` straight into `ToArray<ItemRosterElement>()` (exactly two sites). Its
// callers are the 3-argument constructor and AfterReset, and both build the sort controllers and sort the rows
// right after it, with ItemDescription as the tie-break, so the row order the player sees matches vanilla except
// that rows tying on every key the active comparer reads can land in a different relative order. The transpiler is NameSortRewrite (soft-fail: any other shape leaves
// vanilla). The prefix and void finalizer time the method inside a timed open only (a Reset re-runs it outside one),
// linked by a nullable `__state` so a stripped prefix leaves a finalizer that does nothing.

[HarmonyPatch(typeof(SPInventoryVM), "InitializeInventory")]
[HarmonyPatchCategory(ShadowfaxInventoryCalls.PatchCategory)]
[ClientOnly]
public static class SPInventoryVM_InitializeInventory_ShadowfaxPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = NameSortRewrite.Rewrite(instructions.ToList(), out var swapped, out var problem);
        ShadowfaxInventoryCalls.Service?.ReportSite("name sort", swapped, NameSortRewrite.ExpectedSites, problem);
        return result;
    }

    [HarmonyPrefix]
    public static void Prefix(out long? __state) => __state = ShadowfaxInventoryCalls.BeginRows();

    [HarmonyFinalizer]
    public static void Finalizer(long? __state) => ShadowfaxInventoryCalls.EndRows(__state);
}
