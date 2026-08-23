using HarmonyLib;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate;

namespace TAOM.Features.FieldCamp.Hooks;

/// <summary>
/// Postfix on <see cref="PartyPlayerNameplateWidget"/>.<c>UpdateNameplatesVisibility(float)</c>
/// (protected override, called every frame for the player nameplate): injects a 54x54 icon child
/// widget above the nameplate showing the current camp type, and hides it when no camp stands.
///
/// This namespace is PatchShield-excluded (see Patch38 next to it in SubModule.cs), so the body
/// is written to be structurally unable to throw: one try/catch around everything, null guards,
/// and swallowed failures, because an escaped exception here would ride a per-frame UI callback
/// with no managed backstop. All widget construction, sprite selection and memoization live in
/// <see cref="CampNameplateIconPresenter"/> (ADR-002 thin entry point); this file is only the
/// guarded read-state-and-delegate body.
///
/// Service handle arrives once via <see cref="Initialize"/> at module load (the Patch38 pattern);
/// resolving from IoC per call would put a container lookup on a per-frame path.
/// </summary>
[HarmonyPatch(typeof(PartyPlayerNameplateWidget), "UpdateNameplatesVisibility")]
[HarmonyPatchCategory("Patch74_FieldCampNameplateIcon")]
public static class PartyNameplateCampIconPatch
{
    private static ICampService? _campService;

    /// <summary>Called once from IoC.InitializePatchStatics at container build time.</summary>
    public static void Initialize(ICampService campService)
    {
        _campService = campService;
    }

    /// <summary>Session-end hook (wired from the campaign behavior): drops the presenter's
    /// per-widget memo so nothing from a dead UI context survives into the next campaign.</summary>
    public static void Reset()
    {
        CampNameplateIconPresenter.Reset();
    }

    [HarmonyPostfix]
    public static void Postfix(PartyPlayerNameplateWidget __instance)
    {
        try
        {
            var service = _campService;
            if (service == null || __instance == null)
                return;

            // The head group is the widget cluster the nameplate hangs from; the speed text is
            // the fallback anchor when a template variant ships without a head group.
            Widget? anchor = __instance.HeadGroupWidget ?? (Widget?)__instance.SpeedTextWidget;
            if (anchor == null)
                return;

            var camp = service.PlayerCamp;
            if (camp == null)
                CampNameplateIconPresenter.HideIcon(anchor);
            else
                CampNameplateIconPresenter.ShowIcon(anchor, camp.TypeEnum);
        }
        catch
        {
            // PatchShield-excluded namespace: swallowing IS the crash guard. A broken icon is
            // cosmetic; an exception here is a CTD.
        }
    }
}
