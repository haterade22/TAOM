using System;
using HarmonyLib;
using TAOM.Core.Diagnostics;
using SandBox.ViewModelCollection.Nameplate;

namespace TAOM.Features.HiddenHosts.Hooks;

/// <summary>
/// Patch105 (docs/reference/harmony-patch-registry.md "Patch105_HiddenHosts"). <c>PartyNameplateVM.RefreshBinding</c>
/// copies the plate's bind fields to its bound properties once per map frame, on the main thread; it is the only place
/// that reads <c>_countBind</c>, <c>_extraInfoTextBind</c> and <c>_fullNameBind</c> (v1.5.5). The prefix lays the mask
/// over those fields for that one call and keeps vanilla's values as Harmony's <c>__state</c>; the finalizer puts them
/// back, so no mask outlives the call. Vanilla decides its own writes from int caches, never from the field text, and
/// every property setter returns early on an equal value, so nothing else needs tracking. The toggle off, a party the
/// player's own side owns, a plate that is not on the map (the visibility bind is read, not the property that lags a
/// frame) and any fault leave the fields as vanilla wrote them. After <see cref="HiddenHostsCalls.FaultBudget"/> faults
/// the hook stops for the rest of the process.
/// </summary>
[HarmonyPatch(typeof(PartyNameplateVM), nameof(PartyNameplateVM.RefreshBinding))]
[HarmonyPatchCategory(HiddenHostsModule.PatchCategory)]
[ClientOnly]
public static class PartyNameplateVM_RefreshBinding_HiddenHosts_Patch
{
    // Bound once, fail-soft: a missing field on a future engine leaves a null here and the hook reports it once and
    // stands down, instead of throwing a TypeInitializationException out of the first call.
    private static readonly AccessTools.FieldRef<PartyNameplateVM, string>? CountBind = Bind<string>("_countBind");
    private static readonly AccessTools.FieldRef<PartyNameplateVM, string>? ExtraInfoBind = Bind<string>("_extraInfoTextBind");
    private static readonly AccessTools.FieldRef<PartyNameplateVM, string>? NameBind = Bind<string>("_fullNameBind");
    private static readonly AccessTools.FieldRef<PartyNameplateVM, bool>? VisibleOnMapBind = Bind<bool>("_isVisibleOnMapBind");

    private static bool _bindReported;

    private static AccessTools.FieldRef<PartyNameplateVM, T>? Bind<T>(string field)
    {
        try { return AccessTools.FieldRefAccess<PartyNameplateVM, T>(field); }
        catch { return null; }
    }

    [HarmonyPrefix]
    public static void Prefix(PartyNameplateVM __instance, out VanillaBinds __state)
    {
        __state = default;
        var service = HiddenHostsCalls.Service;
        var adapter = HiddenHostsCalls.Adapter;
        if (HiddenHostsCalls.Stopped || __instance == null || service == null || adapter == null || !service.Enabled) return;
        try
        {
            if (CountBind == null || ExtraInfoBind == null || NameBind == null || VisibleOnMapBind == null)
            {
                ReportMissingField();
                return;
            }

            var party = __instance.Party;
            if (party == null || !VisibleOnMapBind(__instance)) return;

            var intel = service.Evaluate(adapter.Read(party));
            // Everything that can throw runs before the first write.
            string? horizonName = intel.HidesName ? HiddenHostsTexts.UnknownParty : null;
            __state = VanillaBinds.LayMask(in intel, horizonName, ref CountBind(__instance),
                ref ExtraInfoBind(__instance), ref NameBind(__instance));
        }
        catch (Exception ex)
        {
            HiddenHostsCalls.ReportFault(nameof(PartyNameplateVM_RefreshBinding_HiddenHosts_Patch), ex);
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(PartyNameplateVM __instance, VanillaBinds __state)
    {
        if (!__state.IsSet || __instance == null || CountBind == null || ExtraInfoBind == null || NameBind == null) return;
        try
        {
            __state.Restore(ref CountBind(__instance), ref ExtraInfoBind(__instance), ref NameBind(__instance));
        }
        catch (Exception ex)
        {
            HiddenHostsCalls.ReportFault(nameof(PartyNameplateVM_RefreshBinding_HiddenHosts_Patch), ex);
        }
    }

    private static void ReportMissingField()
    {
        if (_bindReported) return;
        _bindReported = true;
        // #1185: the hook does nothing from here on, and one fault never spends the budget, so report it here.
        SelfDisables.Report(HiddenHostsModule.PatchCategory, "PartyNameplateVM lacks one of _countBind, _extraInfoTextBind, _fullNameBind, _isVisibleOnMapBind");
        HiddenHostsCalls.ReportFault(nameof(PartyNameplateVM_RefreshBinding_HiddenHosts_Patch),
            new MissingFieldException("PartyNameplateVM lacks one of _countBind, _extraInfoTextBind, _fullNameBind, _isVisibleOnMapBind"));
    }
}
