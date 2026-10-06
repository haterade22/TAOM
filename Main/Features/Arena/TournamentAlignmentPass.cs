using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace TAOM.Features.Arena;

/// <summary>
/// #744 boundary for <c>Patch69_TournamentRosterGuard</c>: swaps the other side's entrants (Mordor orcs at
/// Minas Tirith) for the host culture's filler troop, in place, before the crash guard runs, so the guard
/// still checks every slot. <see cref="TournamentAlignmentFilterService"/> decides; this applies, the
/// role <see cref="TournamentEntrantMapper"/> plays for the guard (ADR-002/007).
/// </summary>
public static class TournamentAlignmentPass
{
    private const string Tag = "[TournamentDiag]";

    /// <summary>
    /// Replaces each barred entrant in both <paramref name="result"/> and <paramref name="roster"/>. Has its
    /// own catch, because a fault here must never stop the crash guard that runs after it; the service is
    /// resolved inside that catch for the same reason. With no filler troop nothing is barred: the roster
    /// size is load-bearing, and a barred entrant beats a hole.
    /// </summary>
    public static void Apply(MBList<CharacterObject> result, List<TournamentEntrant> roster, Settlement settlement,
        System.Func<TournamentAlignmentFilterService> resolveFilter, IRaceManager raceManager, IModLogger logger)
    {
        try
        {
            var filter = resolveFilter();
            var filler = TournamentEntrantMapper.ResolveFiller(settlement);
            if (filler == null) return;

            var host = TournamentEntrantMapper.ResolveHost(settlement);
            var barred = filter.FindBarredIndices(roster, host);
            if (barred.Count == 0) return;

            var fillerEntrant = TournamentEntrantMapper.Describe(filler, raceManager);
            var names = new List<string>(barred.Count);
            foreach (var index in barred)
            {
                names.Add(filter.Describe(roster[index], host));
                result[index] = filler;
                roster[index] = fillerEntrant;
            }

            // One line per substituting read, only where the other side is present: two per visit to the
            // join menu, three more when the player joins or watches (the known-heroes mark, the preload and
            // the bracket), plus the off-screen reads (the tournament's creation, its resolution, a hero
            // winner's prize and game load), so two or three per tournament on the map.
            logger.LogInfo($"{Tag} {settlement?.StringId ?? "<no settlement>"}: {barred.Count} of the other side " +
                           $"replaced with {filler.StringId}: {string.Join("; ", names)}");
        }
        catch (System.Exception e)
        {
            logger.LogWarning($"{Tag} alignment pass failed, remaining entrants left in place: {e.GetType().Name}: {e.Message}");
        }
    }
}
