namespace TAOM.Features.WarChronicle.Rally;

/// <summary>One kingdom as <c>taom.rally_status</c> shows it. Pure data.</summary>
public sealed class RallyStatusRow
{
    public string KingdomId { get; set; } = string.Empty;

    public int Points { get; set; }

    /// <summary>The fortification points at the start of the war; null when none has been taken.</summary>
    public int? Baseline { get; set; }

    /// <summary>Share of the baseline lost today (never negative); null without a usable baseline.</summary>
    public float? Loss { get; set; }

    /// <summary>The tier the last daily tick left the kingdom at.</summary>
    public int Tier { get; set; }

    /// <summary>AI-ruled, at war, and not excluded as Neutral: the kingdom would receive its tier's effects.</summary>
    public bool Eligible { get; set; }
}
