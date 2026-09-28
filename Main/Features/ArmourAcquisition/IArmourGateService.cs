using System.Collections.Generic;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// The class of every loaded armour piece and named weapon for the current game, and the gate that
/// keeps heavy, elite, lord and named pieces out of loot, workshops and ungated markets
/// (docs/features/armour-acquisition.md).
/// </summary>
public interface IArmourGateService
{
    /// <summary>
    /// Runs at every game init (items reload from XML with each game): in a campaign, classes the loaded
    /// items and, when the feature is on, marks the gated classes NotMerchandise, which the engine's
    /// workshops, battle loot, regular tournament prizes, plunder and hideout loot all honour. Any other
    /// game type only clears the state. Never throws.
    /// </summary>
    void ApplyGating(bool isCampaign);

    /// <summary>True when this game's gating is applied; false when the feature is off or could not apply.</summary>
    bool IsActive { get; }

    /// <summary>The class of a governed item (character armour or a named weapon), or null.</summary>
    ArmourClass? GetClass(string itemId);

    /// <summary>The piece the armoury upgrades <paramref name="itemId"/> into, or null.</summary>
    string? GetNext(string itemId);

    ArmourItemRecord? GetRecord(string itemId);

    /// <summary>The item's display name, looked up when shown.</summary>
    string GetName(string itemId);

    /// <summary>
    /// Loaded class-table pieces of one class, optionally of one culture, sorted by id. Never a piece
    /// classed only by its engine tier (a vanilla Calradian piece).
    /// </summary>
    IReadOnlyList<string> GetPieces(ArmourClass cls, string? cultureId);

    /// <summary>
    /// Whether a market of a town at <paramref name="townLevel"/> may stock the item. With gating off,
    /// always true (the markets behave as before). With it on: a heavy, elite or lord piece needs its
    /// class's armoury level, a named piece never qualifies, and an item the XML marked non-merchandise
    /// never does.
    /// </summary>
    bool IsEligibleForMarket(string itemId, int townLevel);
}
