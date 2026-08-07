using System.Collections.Generic;

namespace TAOM.Features.Arena;

/// <inheritdoc cref="ITournamentRosterGuardService"/>
public sealed class TournamentRosterGuardService : ITournamentRosterGuardService
{
    public TournamentEntrantVerdict Classify(TournamentEntrant entrant)
    {
        // The hook maps a null CharacterObject to a default-constructed entrant, whose
        // CharacterId is null. Vanilla would already have NREd building the bracket, but the
        // guard reports it rather than silently passing it through.
        if (entrant.CharacterId == null)
            return TournamentEntrantVerdict.UnsafeNullCharacter;

        // Mirror the winner-panel branch exactly: IsHero picks MapFaction, otherwise Culture.
        // Deliberately NOT "unsafe if either is null" — a hero with a null Culture never reaches
        // the culture branch, and substituting it would be a change with no crash behind it.
        if (entrant.IsHero)
            return entrant.HasMapFaction ? TournamentEntrantVerdict.Safe : TournamentEntrantVerdict.UnsafeNullMapFaction;

        return entrant.HasCulture ? TournamentEntrantVerdict.Safe : TournamentEntrantVerdict.UnsafeNullCulture;
    }

    public IReadOnlyList<int> FindUnsafeIndices(IReadOnlyList<TournamentEntrant> roster)
    {
        var unsafeIndices = new List<int>();
        if (roster == null) return unsafeIndices;

        for (var i = 0; i < roster.Count; i++)
        {
            if (Classify(roster[i]) != TournamentEntrantVerdict.Safe)
                unsafeIndices.Add(i);
        }
        return unsafeIndices;
    }

    public string Describe(TournamentEntrant entrant, TournamentEntrantVerdict verdict)
    {
        var id = entrant.CharacterId ?? "<null>";
        var name = entrant.Name ?? "<unnamed>";
        var race = entrant.RaceName ?? "<unknown>";
        var clan = entrant.ClanId ?? "<none>";
        var kind = entrant.IsHero ? "hero" : "troop";
        var sex = entrant.IsFemale ? "female" : "male";

        return $"{id} '{name}' {kind} {sex} race={race} clan={clan} " +
               $"mapFaction={(entrant.HasMapFaction ? "ok" : "NULL")} " +
               $"culture={(entrant.HasCulture ? "ok" : "NULL")} -> {verdict}";
    }
}
