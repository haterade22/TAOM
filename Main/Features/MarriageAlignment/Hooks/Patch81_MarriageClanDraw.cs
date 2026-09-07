using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Library;

namespace TAOM.Features.MarriageAlignment.Hooks;

/// <summary>
/// Narrows the AI's random partner-clan draw to alignment-compatible clans. Vanilla
/// <c>RomanceCampaignBehavior.CheckNpcMarriages</c> picks with
/// <c>Clan.All[MBRandom.RandomInt(Clan.All.Count)]</c>: one uniform draw over EVERY clan per lord
/// per day, then <c>continue</c> if it is unsuitable. With the Free/Evil block in
/// <c>TaomMarriageModel</c> and no steering, a Free lord's draw lands on a usable clan only ~41% of
/// the time (the pool is 53% Evil, 25% Free, 16% Neutral), so Gondor, Rohan and Dale would marry
/// far less often and run short of heirs.
/// </summary>
/// <remarks>
/// The transpiler swaps both <c>Clan.All</c> calls for <see cref="CandidateClansFor"/>. Vanilla's
/// <c>.Count</c> and indexer then operate on the filtered pool with no further IL change, and every
/// vanilla filter after the draw (war, clan relation, romance state, the model) still runs. The
/// block itself lives in the model, NOT here, because the player dialogue, barter and
/// AI-offers-to-player paths never reach this method.
/// </remarks>
[HarmonyPatch(typeof(RomanceCampaignBehavior), "CheckNpcMarriages")]
[HarmonyPatchCategory("Patch81_MarriageAlignment")]
public static class Patch81_MarriageClanDraw
{
    private static readonly MethodInfo? CandidatePoolMethod =
        AccessTools.Method(typeof(Patch81_MarriageClanDraw), nameof(CandidateClansFor));

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);

        if (CandidatePoolMethod == null)
        {
            LogDegradation("CandidateClansFor could not be resolved by reflection.");
            return code;
        }

        // Match on name + declaring type rather than MethodInfo equality, per the sibling
        // transpiler (DeliverOffSpring_RaceAssert_Patch) - operand identity is not reliable here.
        var hits = new List<int>();
        for (var i = 0; i < code.Count; i++)
        {
            if (code[i].opcode == OpCodes.Call &&
                code[i].operand is MethodInfo mi &&
                mi.Name == "get_All" &&
                mi.DeclaringType == typeof(Clan))
            {
                hits.Add(i);
            }
        }

        // Clan.All appears on exactly ONE source line in the whole class (the draw), which is two
        // IL calls: the indexer's receiver and the .Count argument. Any other count means the
        // engine IL moved, so degrade to vanilla rather than splice something we do not recognise.
        if (hits.Count != 2)
        {
            LogDegradation($"expected 2 Clan.All call sites in CheckNpcMarriages, found {hits.Count}.");
            return code;
        }

        // Back to front so the inserts do not shift the earlier index. Mutating the existing
        // instruction in place (rather than replacing it) preserves any labels attached to it.
        for (var k = hits.Count - 1; k >= 0; k--)
        {
            var i = hits[k];
            code[i].opcode = OpCodes.Ldarg_1;   // instance method: arg0 = this, arg1 = consideringClan
            code[i].operand = null;
            code.Insert(i + 1, new CodeInstruction(OpCodes.Call, CandidatePoolMethod));
        }

        return code;
    }

    /// <summary>
    /// The clans <paramref name="consideringClan"/> may marry into. Returns vanilla's
    /// <see cref="Clan.All"/> unchanged on every degenerate path, so the draw is never starved and
    /// the worst case is simply vanilla behaviour with the model still blocking the pairing.
    /// </summary>
    public static MBReadOnlyList<Clan> CandidateClansFor(Clan consideringClan)
    {
        // Read first, and deliberately with no Campaign.Current guard: Clan.All IS
        // Campaign.Current.Clans, and vanilla's CheckNpcMarriages reads it unconditionally at this
        // exact point, so this adds no failure vanilla did not already have. A guard here would be
        // unreachable theatre — the read would have thrown before reaching it — and putting the read
        // inside the try would leave the catch with nothing safe to return.
        var all = Clan.All;
        try
        {
            if (consideringClan == null)
                return all;

            var service = ResolveService();
            if (service == null || !service.ShouldSteerAiPartnerSearch)
                return all;

            var cultureId = consideringClan.Culture?.StringId;
            if (string.IsNullOrEmpty(cultureId))
                return all;

            return MarriageClanPoolCache.GetOrBuild(cultureId!, all, service) ?? all;
        }
        catch
        {
            // A steering helper must never take the campaign's daily clan tick down.
            return all;
        }
    }

    // ---- service resolution (cached: this runs twice per lord per day) ----

    private static IMarriageAlignmentService? _service;
    private static bool _serviceResolveFailed;

    private static IMarriageAlignmentService? ResolveService()
    {
        if (_service != null || _serviceResolveFailed) return _service;
        try
        {
            _service = IoC.Resolve<IMarriageAlignmentService>();
        }
        catch
        {
            _serviceResolveFailed = true;
        }
        return _service;
    }

    private static void LogDegradation(string detail)
    {
        try
        {
            IoC.Resolve<IModLogger>()?.LogWarning(
                $"[MarriageAlignment] Patch81 transpiler degrading to no-op - {detail} " +
                "Cross-alignment marriages are still blocked by TaomMarriageModel; only the " +
                "partner-search steering is lost, so Free clans will marry less often this session.");
        }
        catch { /* logger resolution failure must not surface to the transpiler caller */ }
    }
}
