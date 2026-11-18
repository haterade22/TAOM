using System;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;

namespace TAOM.Features.HostilePeace;

/// <summary>
/// The one rule behind the four blocks (docs/features/hostile-peace.md): two realms whose
/// <c>diplomacy.json</c> tier is <see cref="AllianceTier.Hostile"/> (every Free realm against every Dark Power, plus a
/// pledged realm through <c>DiplomacyService</c>) are enemies in all but war. While they are at peace their caravans do not
/// trade in each other's towns, they sign no trade agreement, and the player meets their towns as at war. No war is declared
/// and no phase is read: the War of the Ring decides when they fight, this decides only what peace allows.
///
/// <para>The blocks also hold after the War of the Ring ends (<c>WarPhase.WarEnded</c>): a victory lifts the peace locks, so
/// two Hostile realms may make peace again, and they are still on opposite sides. Pure: kingdom StringIds in, no TaleWorlds
/// types. Main thread or a worker; it only reads.</para>
/// </summary>
public sealed class HostilePeaceService
{
    private readonly IDiplomacyService _diplomacy;
    private readonly IAlignmentService _alignment;
    private readonly IHostilePeaceSettingsProvider _settingsProvider;

    public HostilePeaceService(IDiplomacyService diplomacy, IAlignmentService alignment, IHostilePeaceSettingsProvider settings)
    {
        _diplomacy = diplomacy;
        _alignment = alignment;
        _settingsProvider = settings;
    }

    /// <summary>
    /// Block D: true when a kingdom the player rules may not ally with the other kingdom, because the two stand on opposite
    /// sides of the Free/Evil line. Each kingdom is sided by <see cref="IAlignmentService.ResolveSide"/> on its own id and its own
    /// culture: a listed realm (the player ruling Mirkwood) by its id, a kingdom the player founded (an id <c>alignment.json</c>
    /// does not list) by its culture. A Neutral side on either end never blocks, so a kingdom with no side may ally with anyone.
    /// Unlike blocks A to C this reads the alignment table, not the Hostile tier: a player-founded kingdom is in no
    /// <c>diplomacy.json</c> row, so the tier would never block it. The switch "Allow Player Alliances With Anyone" turns it off.
    /// </summary>
    public bool BlocksPlayerAlliance(string? playerKingdomId, string? playerCultureId, string? otherKingdomId, string? otherCultureId)
    {
        if (_settingsProvider.AllowPlayerAlliancesWithAnyone)
            return false;
        if (string.IsNullOrEmpty(playerKingdomId) || string.IsNullOrEmpty(otherKingdomId))
            return false;
        if (string.Equals(playerKingdomId, otherKingdomId, StringComparison.Ordinal))
            return false;

        return FactionSideRules.AreOpposed(
            _alignment.ResolveSide(playerKingdomId!, playerCultureId ?? string.Empty),
            _alignment.ResolveSide(otherKingdomId!, otherCultureId ?? string.Empty));
    }

    /// <summary>True when a caravan of <paramref name="caravanFactionId"/> may not trade in a town of
    /// <paramref name="townFactionId"/>. A pair at war is vanilla's to refuse.</summary>
    public bool BlocksCaravanTrade(string? caravanFactionId, string? townFactionId, bool atWar) =>
        !atWar && _settingsProvider.BlockCaravanTrade && IsHostilePair(caravanFactionId, townFactionId);

    /// <summary>True when the two realms may not sign a trade agreement. Vanilla already refuses one at war.</summary>
    public bool BlocksTradeAgreement(string? kingdomAId, string? kingdomBId) =>
        _settingsProvider.BlockTradeAgreements && IsHostilePair(kingdomAId, kingdomBId);

    /// <summary>True when the player's realm meets a settlement of <paramref name="settlementFactionId"/> as at war. At war
    /// the base game already does.</summary>
    public bool LimitsTownAccess(string? playerFactionId, string? settlementFactionId, bool atWar) =>
        !atWar && _settingsProvider.LimitTownAccess && IsHostilePair(playerFactionId, settlementFactionId);

    /// <summary>True when the player meets this settlement as at war: a Hostile pair at peace, unless the player's clan owns
    /// it.</summary>
    public bool LimitsPlayerAccess(string? playerFactionId, string? settlementFactionId, bool atWar, bool playerOwnsSettlement) =>
        !playerOwnsSettlement && LimitsTownAccess(playerFactionId, settlementFactionId, atWar);

    /// <summary>True when the player's crime rating with <paramref name="crimeFactionId"/> must stay below the war threshold:
    /// the player leads the realm and meets the Hostile pair as at war (<see cref="LimitsTownAccess"/>). A player who does not
    /// lead a realm is never declared against by crime, so there is nothing to cap.</summary>
    public bool CapsCrimeBelowWar(string? playerFactionId, string? crimeFactionId, bool atWar, bool playerLeadsRealm) =>
        playerLeadsRealm && LimitsTownAccess(playerFactionId, crimeFactionId, atWar);

    /// <summary>Two different, named realms of the Hostile tier. A clan without a kingdom, or a realm missing from
    /// <c>diplomacy.json</c>, reads Neutral there and is never blocked.</summary>
    public bool IsHostilePair(string? kingdomAId, string? kingdomBId)
    {
        if (string.IsNullOrEmpty(kingdomAId) || string.IsNullOrEmpty(kingdomBId))
            return false;
        if (string.Equals(kingdomAId, kingdomBId, StringComparison.Ordinal))
            return false;

        return _diplomacy.GetRelationshipTier(kingdomAId!, kingdomBId!) == AllianceTier.Hostile;
    }
}
