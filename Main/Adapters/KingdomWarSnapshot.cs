using System;

namespace TAOM.Adapters;

/// <summary>
/// One non-eliminated kingdom as the War Chronicle's ledger and rally see it, read once per day by
/// <see cref="IKingdomWarSnapshotAdapter"/>. Pure data: no engine type crosses into the services.
/// </summary>
public sealed class KingdomWarSnapshot
{
    public string Id { get; set; } = string.Empty;

    public string CultureId { get; set; } = string.Empty;

    /// <summary>The kingdom's ruling clan is the player's clan.</summary>
    public bool IsPlayerRuled { get; set; }

    /// <summary>At war with at least one other non-eliminated kingdom.</summary>
    public bool AtWar { get; set; }

    public int Towns { get; set; }

    public int Castles { get; set; }

    /// <summary>The kingdom's initial home settlement is still owned by a clan of the kingdom; null when unknown.</summary>
    public bool? HomeHeld { get; set; }

    /// <summary>The engine's total strength of the kingdom's clans; -1 when it is not a finite number.</summary>
    public float Strength { get; set; }

    /// <summary>Heroes of the kingdom's clans who are in captivity.</summary>
    public int PrisonerLords { get; set; }

    /// <summary>Fortification points, 2 per town and 1 per castle (a negative count reads as zero).</summary>
    public int FortificationPoints => 2 * Math.Max(0, Towns) + Math.Max(0, Castles);
}
