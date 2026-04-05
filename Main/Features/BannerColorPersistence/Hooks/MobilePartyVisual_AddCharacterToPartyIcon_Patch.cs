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
        return AccessTools.Method(typeof(MobilePartyVisual), "AddCharacterToPartyIcon",
            new[]
            {
                typeof(PartyBase),
                typeof(CharacterObject),
                typeof(uint),
                typeof(string),
                typeof(int),
                typeof(uint),
                typeof(uint),
                typeof(ActionIndexCache).MakeByRefType(),
                typeof(ActionIndexCache).MakeByRefType(),
                typeof(float),
                typeof(bool).MakeByRefType()
            });
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
