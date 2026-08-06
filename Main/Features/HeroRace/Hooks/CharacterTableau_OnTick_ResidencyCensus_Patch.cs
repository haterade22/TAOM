using HarmonyLib;
using System;
using System.Reflection;
using TAOM.Features.HeroRace.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Tableaus;

namespace TAOM.Features.HeroRace.Hooks;

/// <summary>
/// Instrumentation for issue #389 — Isengard troops wearing an `sk_uruk_hai_helmet_*` render as
/// black silhouettes in the encyclopedia. Dumps ONE render census per character on the frame its
/// visual becomes ready: mesh names, material names, shaders, shader flags, per-mesh vertex colours
/// and bound diffuse textures. Diff an affected troop against a working one to name the cause.
///
/// Its own category, deliberately: the #389 experiment disables Patch2_RefreshTableau and
/// Patch3_SetRace to test whether TAOM's own patches cause the fault, and the instrument must keep
/// reporting while that runs.
///
/// PER-FRAME COST. This is a postfix on a method that runs once per rendered frame per live tableau,
/// so the steady-state path is deliberately allocation-free: a weak-table lookup, one ordinal string
/// compare (both inside <see cref="TableauCensusSession.KeyFor"/>), a lock, a dictionary lookup, and
/// an early return. Every lambda and every reflected field read sits AFTER that early return, so they
/// cost nothing on the frames that do not report.
/// </summary>
[HarmonyPatch(typeof(CharacterTableau), "OnTick")]
[HarmonyPatchCategory("Patch67_TableauResidencyDiag")]
public class CharacterTableau_OnTick_ResidencyCensus_Patch
{
    // Resolved once. Reflection on a per-frame path must never be re-resolved per call, and these are
    // only dereferenced on the single reporting frame anyway.
    private static readonly FieldInfo RaceField = AccessTools.Field(typeof(CharacterTableau), "_race");
    private static readonly FieldInfo InitialLoadingField = AccessTools.Field(typeof(CharacterTableau), "_initialLoadingCounter");
    private static readonly FieldInfo IsEnabledField = AccessTools.Field(typeof(CharacterTableau), "_isEnabled");
    private static readonly FieldInfo ClothColor1Field = AccessTools.Field(typeof(CharacterTableau), "_clothColor1");
    private static readonly FieldInfo ClothColor2Field = AccessTools.Field(typeof(CharacterTableau), "_clothColor2");
    private static readonly FieldInfo BodyPropertiesField = AccessTools.Field(typeof(CharacterTableau), "_bodyProperties");
    private static readonly FieldInfo AgentVisualsField = AccessTools.Field(typeof(CharacterTableau), "_agentVisuals");
    private static readonly FieldInfo OldAgentVisualsField = AccessTools.Field(typeof(CharacterTableau), "_oldAgentVisuals");

    // FOUR underscores, not three. Harmony strips exactly three and looks the remainder up as a field
    // name, so `___agentVisualLoadingCounter` asks for "agentVisualLoadingCounter" — which does not
    // exist — and the whole category throws ArgumentException("No such field") at apply time. The
    // engine's fields carry a leading underscore, so that underscore must survive the strip. This
    // shipped broken once (2026-08-06): it passed four binding tests and the full suite, because
    // AccessTools.Field resolves the field name happily and never exercises the parameter convention.
    // Now pinned repo-wide by HarmonyFieldInjectionNamingTests.
    [HarmonyPostfix]
    public static void Postfix(
        CharacterTableau __instance,
        int ____agentVisualLoadingCounter,
        int ____mountVisualLoadingCounter,
        string ____charStringId)
    {
        try
        {
            string key = TableauCensusSession.KeyFor(__instance, ____charStringId);
            var result = TableauCensusSession.Observe(key, ____agentVisualLoadingCounter, ____mountVisualLoadingCounter);
            if (result.Verdict == TableauResidencyVerdict.None) return;

            if (result.Verdict == TableauResidencyVerdict.CapacityReached)
            {
                TableauDiagnostics.LogRenderCensus(key, result.Verdict, result.Ticks, string.Empty, null);
                return;
            }

            var agentVisuals = AgentVisualsField?.GetValue(__instance) as AgentVisuals;
            var oldAgentVisuals = OldAgentVisualsField?.GetValue(__instance) as AgentVisuals;

            TableauDiagnostics.LogRenderCensus(
                key,
                result.Verdict,
                result.Ticks,
                BuildContext(__instance, ____charStringId, ____agentVisualLoadingCounter,
                             ____mountVisualLoadingCounter, agentVisuals, oldAgentVisuals),
                TableauRenderCensus.Describe(Entity(() => agentVisuals?.GetEntity())));
        }
        catch (Exception e)
        {
            TableauDiagnostics.LogError($"Patch67 render census THREW (rendering is unaffected): {e}");
        }
    }

    private static string BuildContext(
        CharacterTableau instance, string charStringId,
        int agentCounter, int mountCounter,
        AgentVisuals agentVisuals, AgentVisuals oldAgentVisuals)
    {
        string bodyProps = Describe(() =>
        {
            var bp = BodyPropertiesField?.GetValue(instance);
            return bp is BodyProperties p
                ? (p == BodyProperties.Default ? "DEFAULT(vanilla hides the visual)" : "set")
                : "<unreadable>";
        });

        return
            $"char='{charStringId ?? "<null>"}' race={Describe(() => RaceField?.GetValue(instance)?.ToString())} " +
            $"clothColor1={Describe(() => "0x" + ((uint)(ClothColor1Field?.GetValue(instance) ?? 0u)).ToString("X8"))} " +
            $"clothColor2={Describe(() => "0x" + ((uint)(ClothColor2Field?.GetValue(instance) ?? 0u)).ToString("X8"))} " +
            $"bodyProps={bodyProps} " +
            $"isEnabled={Describe(() => IsEnabledField?.GetValue(instance)?.ToString())} " +
            $"initialLoading={Describe(() => InitialLoadingField?.GetValue(instance)?.ToString())} " +
            $"agentLoading={agentCounter} mountLoading={mountCounter} " +
            $"agentVisible={DescribeVisibility(agentVisuals)} oldVisible={DescribeVisibility(oldAgentVisuals)}";
    }

    private static string DescribeVisibility(AgentVisuals visuals) => Describe(() =>
    {
        if (visuals == null) return "<null>";
        var entity = visuals.GetEntity();
        return entity == null ? "<no-entity>" : entity.GetVisibilityExcludeParents().ToString();
    });

    private static string Describe(Func<string> f)
    {
        try { return f() ?? "<null>"; }
        catch (Exception e) { return "<threw:" + e.GetType().Name + ">"; }
    }

    private static TaleWorlds.Engine.GameEntity Entity(Func<TaleWorlds.Engine.GameEntity> f)
    {
        try { return f(); }
        catch { return null; }
    }
}
