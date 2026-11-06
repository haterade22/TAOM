using System;
using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Features.Arena;

/// <summary>
/// #744: which tournament entrants belong to the other side (Mordor orcs at Minas Tirith). Pure over
/// <see cref="TournamentEntrant"/> and <see cref="TournamentHost"/>; <c>Patch69_TournamentRosterGuard</c>
/// swaps each barred index for the filler troop, in place, so the 16-slot roster never shrinks.
/// </summary>
/// <remarks>
/// The host's side is its owner's: the owner faction's id, falling back to that faction's own culture, the
/// pairing CaravanTrade uses. A captured town therefore follows its conqueror, and a town held by a Neutral
/// realm bars nobody. An entrant is sided the same way, on one pair (<c>FactionSides.OfCharacter</c>): a clan
/// hero by the clan's map faction and that faction's culture (a lord of a Neutral realm is Neutral whatever the
/// lord's own culture), a clanless hero by its own culture, a troop by its culture. An entrant is barred
/// only when both sides are Free/Evil and differ: Neutral enters anywhere, the reading WandererAllegiance
/// and MarriageAlignment give the table.
/// <see cref="IAlignmentService.AreEnemyAlignments(FactionSide, FactionSide)"/> is deliberately not used,
/// because it counts Neutral as everyone's enemy.
/// <para>
/// Never barred: the player and the player's clan (the player chose them); an entrant with no culture and
/// no kingdom (no side to judge, so vanilla stands); and a troop of the town's own culture or of the culture
/// of its basic or elite troop, because vanilla pads the roster (the basic troop's tree, then basic and elite
/// troops) and Patch69 fills it (elite, else basic) from exactly those troops. Several cultures borrow another
/// culture's troops (Khand from khuzait, the Harad realms and Umbar's basic troop
/// from aserai, the goblin realms from goblin), hence the troop ids. Without the exemption a captured town's
/// filler would be barred and replaced by itself.
/// </para>
/// </remarks>
public sealed class TournamentAlignmentFilterService
{
    private readonly IAlignmentService _alignment;
    private readonly ITournamentAlignmentSettingsProvider _settings;

    public TournamentAlignmentFilterService(IAlignmentService alignment, ITournamentAlignmentSettingsProvider settings)
    {
        _alignment = alignment;
        _settings = settings;
    }

    public IReadOnlyList<int> FindBarredIndices(IReadOnlyList<TournamentEntrant>? roster, TournamentHost host)
    {
        var barred = new List<int>();
        if (roster == null || !_settings.IsEnabled)
            return barred;

        var hostSide = HostSide(host);
        if (hostSide == FactionSide.Neutral)
            return barred;

        for (var i = 0; i < roster.Count; i++)
        {
            if (IsBarred(roster[i], hostSide, host))
                barred.Add(i);
        }
        return barred;
    }

    public string Describe(TournamentEntrant entrant, TournamentHost host)
    {
        var side = _alignment.ResolveSide(entrant.Side);
        return $"{entrant.CharacterId ?? "<null>"} '{entrant.Name ?? "<unnamed>"}' {(entrant.IsHero ? "hero" : "troop")} " +
               $"side={side} (faction={Show(entrant.Side.FactionId)} sideCulture={Show(entrant.Side.CultureId)} " +
               $"culture={entrant.CultureId ?? "<none>"}) " +
               $"barred by host side={HostSide(host)} (owner={Show(host.OwnerSide.FactionId)} " +
               $"ownerCulture={Show(host.OwnerSide.CultureId)} town={host.TownCultureId ?? "<none>"})";
    }

    private static string Show(string id) => id.Length == 0 ? "<none>" : id;

    private FactionSide HostSide(TournamentHost host) => _alignment.ResolveSide(host.OwnerSide);

    private bool IsBarred(TournamentEntrant entrant, FactionSide hostSide, TournamentHost host)
    {
        if (entrant.IsPlayerOrPlayerClan)
            return false;

        if (!entrant.IsHero && entrant.CultureId != null &&
            (SameId(entrant.CultureId, host.TownCultureId) || SameId(entrant.CultureId, host.BasicTroopCultureId)
             || SameId(entrant.CultureId, host.EliteTroopCultureId)))
            return false;

        var side = _alignment.ResolveSide(entrant.Side);
        return FactionSideRules.AreOpposed(side, hostSide);
    }

    private static bool SameId(string a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
