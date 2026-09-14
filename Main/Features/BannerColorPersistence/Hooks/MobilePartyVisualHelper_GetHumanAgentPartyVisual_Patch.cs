using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.BannerColorPersistence.Hooks;

// Keeps a clan's own colours on its campaign-map party icon instead of its kingdom's.
//
// v1.5.0 migration. The old target, MobilePartyVisual.AddCharacterToPartyIcon, lost the
// `ref uint teamColor1, ref uint teamColor2` parameters this feature wrote to (11 params -> 7),
// and the colour work moved into MobilePartyVisualHelper.GetHumanAgentPartyVisual, which reads
// party.MapFaction.Color / .Color2 into locals. A postfix can no longer reach them, so the seam
// is a transpiler on those two reads. See BannerColorTranspiler for the IL contract.
//
// Note the old postfix would have thrown at startup under v1.5.0, not merely misbehaved: it is
// applied through ManualPatchApplicator with Harmony.Patch, and it declared parameter names that
// no longer exist on the target. The binding gate stayed green throughout because TargetMethod()
// resolves by name only, which is the hole that motivated the IL-level test alongside this.
public static class MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;
    private static IModLogger? _logger;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter, IModLogger logger)
    {
        _service = service;
        _heroAdapter = heroAdapter;
        _logger = logger;
    }

    public static MethodBase? TargetMethod() =>
        AccessTools.Method(
            AccessTools.TypeByName("SandBox.View.SandBoxViewHelpers+MobilePartyVisualHelper"),
            "GetHumanAgentPartyVisual");

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
        BannerColorTranspiler.Rewrite(
            instructions,
            AccessTools.Method(typeof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch), nameof(ResolveColor1)),
            AccessTools.Method(typeof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch), nameof(ResolveColor2)),
            _logger);

    // Both resolvers pass the vanilla value straight through whenever the clan-colour rule does not
    // apply, so an unrecognised party keeps exactly the colour the engine chose.
    public static uint ResolveColor1(uint vanillaColor, PartyBase party) => Resolve(vanillaColor, party, primary: true);

    public static uint ResolveColor2(uint vanillaColor, PartyBase party) => Resolve(vanillaColor, party, primary: false);

    private static uint Resolve(uint vanillaColor, PartyBase party, bool primary)
    {
        var leader = PartyBaseHelper.GetVisualPartyLeader(party);
        if (leader == null) return vanillaColor;

        var info = _heroAdapter?.GetClanColorInfo(leader);
        if (info == null) return vanillaColor;
        if (!(_service?.ShouldUseClanColor(info.Value) ?? false)) return vanillaColor;

        return primary ? info.Value.Color1 : info.Value.Color2;
    }
}
