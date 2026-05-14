using System.Reflection;
using HarmonyLib;
using SandBox.View.Map.Visuals;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BannerColorPersistence.Hooks;

public static class MobilePartyVisual_AddCharacterToPartyIcon_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    public static MethodBase? TargetMethod()
    {
        // Phase 9b #159 — drop the explicit param-type array. The method has exactly one overload
        // in v1.3.15 (verified via ilspycmd on SandBox.View.dll), so name-only resolution is
        // unambiguous. The previous array included `typeof(ActionIndexCache).MakeByRefType()` for
        // the two `in ActionIndexCache` params — `in` is modreq-qualified in IL and Harmony 2's
        // AccessTools is inconsistent about matching modreq. If resolution failed, the LogWarning
        // would fire and party-icon colors silently stop persisting. Name-only resolution is
        // robust because there's no ambiguity to resolve.
        return AccessTools.Method(typeof(MobilePartyVisual), "AddCharacterToPartyIcon");
    }

    public static void Postfix(CharacterObject characterObject, ref uint teamColor1, ref uint teamColor2)
    {
        var info = _heroAdapter?.GetClanColorInfo(characterObject);
        if (info == null) return;
        if (!(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        teamColor1 = info.Value.Color1;
        teamColor2 = info.Value.Color2;
    }
}
