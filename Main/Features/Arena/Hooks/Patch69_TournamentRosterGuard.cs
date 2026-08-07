using System.Collections.Generic;
using HarmonyLib;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Library;

namespace TAOM.Features.Arena.Hooks;

/// <summary>
/// Patch69 — Postfix on <c>FightTournamentGame.GetParticipantCharacters(Settlement, bool)</c>.
///
/// Vanilla <c>TournamentVM.OnTournamentEnd</c> (v1.4.7) reads the winner's armour colours through
/// two unguarded dereferences: <c>hero.MapFaction.Color</c> for a hero winner, and
/// <c>character.Culture.Color</c> for a troop winner. <c>Hero.MapFaction</c> genuinely returns
/// null for a clanless, non-special hero with neither a home settlement nor a party — so a
/// wanderer who wins a tournament NREs the winner panel. The player sees a
/// <c>TargetInvocationException</c> out of <c>ViewModel.ExecuteCommand</c>; TAOM's CrashReport
/// finalizer swallows it, leaving the tournament screen half-drawn (crash bundle
/// <c>d7d9f7d3</c>, Erebor, 2026-08-06).
///
/// This postfix SUBSTITUTES offenders in place. It never changes the roster size, and that is
/// load-bearing: vanilla pads the list to exactly <c>MaximumParticipantCount</c> (16), then
/// <c>TournamentBehavior.CreateParticipants</c> copies it into a FIXED 16-slot array and
/// <c>FillParticipants</c> hands every slot — including trailing nulls from a short roster — to
/// <c>TournamentMatch.AddParticipant</c>. That method reads <c>participant.Team</c>; the null check
/// there is on <c>.Team</c>, NOT on <c>participant</c> itself, so a null slot NREs during bracket
/// construction, before the tournament even opens. Trading one crash for another is not a fix. The
/// replacement is the culture's elite/basic troop — the same filler vanilla itself uses in the tail
/// of <c>GetParticipantCharacters</c>.
///
/// **This runs on every call, not once per tournament.** `GetParticipantCharacters` has four call
/// sites — `CreateParticipants` (the one that matters), `GetAllPossibleParticipants`, and
/// `GetMenuText` + `GetTournamentPrize`, both reached from the arena join menu's on_init. So the
/// clean-roster line is DEBUG (it would otherwise emit a durable, synchronously-flushed INFO on
/// every menu open); an actual substitution stays WARNING, because it is rare and is the thing a
/// future crash bundle needs to name.
///
/// Two accepted consequences of patching a method the menu also calls: `GetMenuText`'s
/// "{NOBLE_COUNT} lords are competing" and `GetTournamentPrize`'s reward-tier gate both count
/// `p.IsHero`, so substituting a hero shifts each by one. Both are arguably *more* correct — a
/// substituted hero genuinely does not compete — but they are behaviour changes, recorded here
/// rather than discovered later.
///
/// Decision logic lives in <see cref="ITournamentRosterGuardService"/>; this patch is a thin
/// boundary (ADR-002/007). Lazy service resolve mirrors Patch46.
/// </summary>
[HarmonyPatch(typeof(FightTournamentGame), "GetParticipantCharacters")]
[HarmonyPatchCategory("Patch69_TournamentRosterGuard")]
public static class Patch69_TournamentRosterGuard
{
    private const string Tag = "[TournamentDiag]";

    private static ITournamentRosterGuardService _guard;
    private static IModLogger _logger;
    private static IRaceManager _raceManager;

    private static ITournamentRosterGuardService GetGuard() =>
        _guard ??= TAOM.IoC.Resolve<ITournamentRosterGuardService>();

    private static IModLogger GetLogger() =>
        _logger ??= TAOM.IoC.Resolve<IModLogger>();

    private static IRaceManager GetRaceManager() =>
        _raceManager ??= TAOM.IoC.Resolve<IRaceManager>();

    /// <summary>Module-unload lifecycle hook — drop cached IoC references.</summary>
    public static void ResetForUnload()
    {
        _guard = null;
        _logger = null;
        _raceManager = null;
    }

    [HarmonyPostfix]
    public static void Postfix(MBList<CharacterObject> __result, Settlement settlement)
    {
        if (__result == null || __result.Count == 0) return;

        var guard = GetGuard();
        var logger = GetLogger();
        if (guard == null || logger == null) return;

        try
        {
            var roster = new List<TournamentEntrant>(__result.Count);
            for (var i = 0; i < __result.Count; i++)
                roster.Add(TournamentEntrantMapper.Describe(__result[i], GetRaceManager()));

            var unsafeIndices = guard.FindUnsafeIndices(roster);

            if (unsafeIndices.Count == 0)
            {
                // DEBUG, not INFO: the arena join menu's on_init calls GetMenuText and
                // GetTournamentPrize, which both re-enter this method, so an INFO line here would
                // emit a durable synchronously-flushed write on every menu open. DEBUG is async.
                logger.LogDebug($"{Tag} roster for {settlement?.StringId ?? "<no settlement>"}: " +
                                $"{__result.Count} entrant(s), all safe.");
                return;
            }

            // A substitution is rare and is exactly what a future crash bundle needs named, so the
            // summary and each offender below stay at a durable level.
            logger.LogWarning($"{Tag} roster for {settlement?.StringId ?? "<no settlement>"}: " +
                              $"{__result.Count} entrant(s), {unsafeIndices.Count} unsafe.");

            var replacement = TournamentEntrantMapper.ResolveFiller(settlement);
            foreach (var index in unsafeIndices)
            {
                var verdict = guard.Classify(roster[index]);
                var description = guard.Describe(roster[index], verdict);

                if (replacement == null)
                {
                    // Fail-safe: no filler to substitute, so leave vanilla's roster alone. A
                    // possible crash beats a certain one from a hole in the participant array.
                    logger.LogWarning($"{Tag} UNSAFE (left in place — no filler troop): {description}");
                    continue;
                }

                logger.LogWarning($"{Tag} UNSAFE (substituted with {replacement.StringId}): {description}");
                __result[index] = replacement;
            }
        }
        catch (System.Exception e)
        {
            // Diagnostics must never be the thing that breaks a tournament.
            logger.LogWarning($"{Tag} roster guard failed, deferring to vanilla: {e.GetType().Name}: {e.Message}");
        }
    }
}
