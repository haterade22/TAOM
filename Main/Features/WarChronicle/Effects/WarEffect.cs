namespace TAOM.Features.WarChronicle.Effects;

/// <summary>
/// One timed percentage on one kingdom: +0.15 is +15%. Immutable; <see cref="IWarEffectService.Apply"/>
/// owns the validation, so a row built from a save or the console can be carried as is and rejected there.
/// </summary>
public sealed class WarEffect
{
    public WarEffect(string sourceId, string kingdomId, WarEffectKind kind, float magnitude, double endTimeHours)
    {
        SourceId = sourceId;
        KingdomId = kingdomId;
        Kind = kind;
        Magnitude = magnitude;
        EndTimeHours = endTimeHours;
    }

    /// <summary>Who wrote it: "rally:gondor", an event id, "console". With the kingdom and kind it is the key.</summary>
    public string SourceId { get; }

    public string KingdomId { get; }

    public WarEffectKind Kind { get; }

    /// <summary>A fraction on top of 1: +0.15 raises the rate 15%, -0.05 lowers it 5%.</summary>
    public float Magnitude { get; }

    /// <summary>Campaign hours (<c>CampaignTime.ToHours</c>) at which the effect ends; ended once now reaches it.</summary>
    public double EndTimeHours { get; }
}
