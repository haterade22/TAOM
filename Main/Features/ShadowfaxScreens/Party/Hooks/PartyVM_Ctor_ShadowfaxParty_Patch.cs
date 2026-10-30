using System;
using HarmonyLib;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.ShadowfaxScreens.Party.Hooks;

/// <summary>
/// Party Fast Open (docs/features/shadowfax-screens.md "Party fast open", registry "Patch129_ShadowfaxParty"): the
/// PartyVM constructor opens one caching session and one "Party open" timer window. The finalizer writes the open line
/// with the service's counters and ends the session only when the constructor threw; a normal session lives until that
/// PartyVM's OnFinalize or the next PartyVM constructor, whose BeginScreen clears it (Talk deactivates and reactivates
/// the screen and abandons the old view model without OnFinalize). The prefix and finalizer link only through __state, so a stripped prefix leaves the finalizer a no-op.
/// Any error here falls through to vanilla. UI main thread.
/// </summary>
[HarmonyPatch(typeof(PartyVM), MethodType.Constructor, new[] { typeof(PartyScreenLogic) })]
[HarmonyPatchCategory(PartyFastOpenCalls.PatchCategory)]
[ClientOnly]
public static class PartyVM_Ctor_ShadowfaxParty_Patch
{
    [HarmonyPrefix]
    public static void Prefix(out ScreenOpenWindow? __state)
    {
        __state = null;
        try
        {
            PartyFastOpenCalls.Service?.BeginScreen();
            __state = ScreenOpenTiming.Begin("Party", "open");
        }
        catch
        {
            // Vanilla runs without the session.
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(ScreenOpenWindow? __state, Exception? __exception)
    {
        try
        {
            var service = PartyFastOpenCalls.Service;
            var detail = __state == null ? null : service?.BuildOpenDetail();
            if (__exception != null) service?.EndScreen();
            ScreenOpenTiming.End(__state, __exception, detail);
        }
        catch
        {
            // A diagnostic must not break the screen.
        }
    }
}
