using System.Collections.Generic;
using TaleWorlds.Engine.GauntletUI;
using TAOM.Features.FactionMap.Models;
using TAOM.Features.FactionMap.ViewModels;

namespace TAOM.Features.FactionMap.Hooks;

/// <summary>
/// Lets another screen take the culture stage in place of the faction map's own movie while still
/// driving TAOM's <see cref="FactionSelectionVM"/> (#704: Kysaro's faction and hero picker). Kysaro's
/// module did this by releasing TAOM's movie from a later Harmony postfix and reaching into TAOM's
/// view model by reflection.
/// </summary>
public interface ICultureStageMovieOverride
{
    /// <summary>Loads the replacement movie on <paramref name="layer"/>, or returns null to let the
    /// faction map load its own. <paramref name="regions"/> and <paramref name="factions"/> are the
    /// faction map's data, just loaded for this culture stage.</summary>
    GauntletMovieIdentifier? TryLoad(
        GauntletLayer layer,
        FactionSelectionVM factionVm,
        IReadOnlyDictionary<string, RegionData> regions,
        IReadOnlyDictionary<string, FactionData> factions);

    /// <summary>The culture stage is closing; the replacement releases what it holds.</summary>
    void OnCultureStageClosed();
}
