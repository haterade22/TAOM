using System;
using System.Text;
using HarmonyLib;
using SandBox.ViewModelCollection.Tournament;
using TAOM.Core.Logging;

namespace TAOM.Features.Arena.Hooks;

/// <summary>
/// Patch69 — Finalizer on the private <c>TournamentVM.OnTournamentEnd()</c>.
///
/// Containment plus forensics for the crash <see cref="Patch69_TournamentRosterGuard"/> prevents.
/// The roster guard covers the two dereferences we can prove from the decompile
/// (<c>hero.MapFaction.Color</c>, <c>character.Culture.Color</c>). Two more remain reachable in
/// principle — <c>OnTournamentEnd</c> also reads <c>Participant.Character</c> off participant view
/// models selected by their <c>IsValid</c> flag, and <c>TournamentParticipantVM.Refresh(null, …)</c>
/// nulls <c>Participant</c> without ever resetting <c>IsValid</c> back to false. We could not
/// reproduce that state from a full 16-entrant bracket, so it is not guarded; it IS logged.
///
/// On any exception this dumps the entire bracket — every round, match, team and participant slot,
/// with <c>IsValid</c>, whether <c>Participant</c> is null, and the character behind it — then
/// swallows. The winner panel ends up incomplete, which is strictly better than the tournament
/// screen dying mid-input. If a bundle ever arrives with this dump in it, the offending slot is
/// named outright and the guess becomes a root cause.
///
/// Ordering note: TAOM's CrashReport finalizer on <c>ScreenManager.Update</c> would otherwise be
/// the first thing to see this exception, and it only records the managed stack. This finalizer
/// runs at the throw site, where the bracket is still reachable.
///
/// <para><b>Residual, deliberately uncovered.</b> <c>TournamentBehavior.EndCurrentMatch</c> sets
/// <c>Winner = LastMatch.Winners.FirstOrDefault()</c> and dereferences <c>Winner.Character</c> on
/// the very next line — BEFORE it raises the <c>TournamentEnd</c> event this finalizer wraps. An
/// empty <c>Winners</c> list therefore NREs upstream of both Patch69 hooks (the roster guard sits
/// upstream at roster build, this guard sits downstream at the VM). It needs `GetWinners()`'s score
/// slicing to return zero winners, which we have never observed and which is a different failure
/// from the reported bundle, so it is recorded rather than guarded — guarding it would mean
/// patching a third method on speculation.</para>
/// </summary>
[HarmonyPatch(typeof(TournamentVM), "OnTournamentEnd")]
[HarmonyPatchCategory("Patch69_TournamentEndGuard")]
public static class Patch69_TournamentEndGuard
{
    private const string Tag = "[TournamentDiag]";

    private static IModLogger _logger;

    private static IModLogger GetLogger() => _logger ??= TAOM.IoC.Resolve<IModLogger>();

    /// <summary>Module-unload lifecycle hook — drop the cached IoC reference.</summary>
    public static void ResetForUnload() => _logger = null;

    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception, TournamentVM __instance)
    {
        if (__exception == null) return null;

        var logger = GetLogger();
        if (logger == null) return __exception;   // no way to report it — let vanilla propagation stand

        try
        {
            logger.LogError($"{Tag} OnTournamentEnd threw {__exception.GetType().Name}: {__exception.Message}");
            logger.LogError($"{Tag} {TournamentBracketFormatter.Dump(__instance)}");
        }
        catch (Exception dumpFailure)
        {
            // The dump is best-effort; its failure must not replace the original exception.
            try { logger.LogWarning($"{Tag} bracket dump failed: {dumpFailure.GetType().Name}: {dumpFailure.Message}"); }
            catch { /* logging is already the fallback path */ }
        }

        // Swallow ONLY the null-dereference class this guard exists for. Anything else is rethrown
        // so it still reaches CrashReport and the player's bundle.
        //
        // This is not hypothetical tidiness: `OnTournamentEnd` opens with
        // `Round4.Matches.Last(m => m.IsValid)`, and `Last(predicate)` throws
        // InvalidOperationException — not NRE — when no match qualifies. Swallowing that would turn
        // a real bracket-construction bug into a silently half-drawn screen with no report, which is
        // strictly worse than the crash. Same reasoning as PatchShield's ShouldSwallow, which only
        // eats the engine-drift trinity and rethrows everything else.
        if (!(__exception is NullReferenceException)) return __exception;

        // The tournament screen survives with an incomplete winner panel.
        return null;
    }
}
