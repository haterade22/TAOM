using System.Collections.Generic;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.TroopProgression;

namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// Pure rule for where a wanderer may stand (docs/features/wanderer-allegiance.md, "Where wanderers stand"). A wanderer fits a town
/// unless the two are on opposite sides, the same Free/Evil reading as the hire rule (<see cref="FactionSideRules.AreOpposed"/>):
/// a Neutral wanderer fits anywhere and a Neutral town takes anyone.
/// </summary>
/// <remarks>
/// The wanderer's side is its culture (<see cref="IAlignmentService.GetCultureSide"/>), as in <see cref="WandererAllegianceService"/>.
/// The town's side is its OWNER's: the owner clan's map faction id with that faction's culture (<see cref="IAlignmentService.ResolveSide(string, string)"/>),
/// so a kingdom's towns follow its side whatever their own culture was at capture, and an owner with no kingdom falls back to the
/// clan's culture. The settlement's own culture lags a capture by a day (Culture Conversion), so it is not read.
/// Single player only: the picks are random and a co-op peer would draw its own.
/// </remarks>
public sealed class WandererTownSideService
{
    private readonly IAlignmentService _alignment;
    private readonly IWandererAllegianceSettingsProvider _settings;
    private readonly IRandomProvider _random;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;

    public WandererTownSideService(
        IAlignmentService alignment,
        IWandererAllegianceSettingsProvider settings,
        IRandomProvider random,
        ICoopSessionProvider coop,
        IDedicatedServerProvider dedicated)
    {
        _alignment = alignment;
        _settings = settings;
        _random = random;
        _coop = coop;
        _dedicated = dedicated;
    }

    /// <summary>The switch is on and this peer decides the world alone. Read live on every call.</summary>
    public bool Applies() =>
        _settings.PlacesWanderersBySide && CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated);

    /// <summary>True unless the wanderer's culture side and the town owner's side are Free against Evil. No culture fails open.</summary>
    public bool Fits(string? wandererCultureId, SideIds town)
    {
        if (string.IsNullOrEmpty(wandererCultureId))
            return true;
        var wanderer = _alignment.GetCultureSide(wandererCultureId!);
        return !FactionSideRules.AreOpposed(wanderer, _alignment.ResolveSide(town));
    }

    /// <summary>
    /// The index into <paramref name="options"/> of the town to use instead of <paramref name="destination"/>, or -1 to keep
    /// the destination. A destination that fits and holds no other unhired wanderer is kept. A destination of the opposite side is
    /// replaced by a fitting town, an empty one first. A fitting destination that already holds another wanderer
    /// (<paramref name="destinationHasWanderer"/>: an earlier redirect took its slot) is replaced only by an empty fitting town,
    /// so two wanderers do not share a tavern while another town has none.
    /// </summary>
    public int Choose(string? wandererCultureId, SideIds destination, IReadOnlyList<WandererTownOption> options, bool destinationHasWanderer = false)
    {
        var fits = Fits(wandererCultureId, destination);
        if (fits && !destinationHasWanderer)
            return -1;

        var empty = new List<int>();
        var occupied = new List<int>();
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            if (!option.Usable || !Fits(wandererCultureId, option.Side))
                continue;
            (option.HasWanderer ? occupied : empty).Add(i);
        }

        var pool = empty.Count > 0 || fits ? empty : occupied;
        return pool.Count == 0 ? -1 : pool[_random.Next(pool.Count)];
    }
}
