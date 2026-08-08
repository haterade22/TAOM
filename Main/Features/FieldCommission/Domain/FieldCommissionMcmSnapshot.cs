using System;

namespace TAOM.Features.FieldCommission.Domain;

/// <summary>
/// An immutable read of the Battlefield Promotions MCM knobs at one instant. Every field is nullable
/// because <c>TaomSettings.Instance</c> is null whenever MCM is absent or not yet initialized — a null
/// field means "the player expressed no preference", and the JSON value stands.
///
/// It exists to be a cheap cache key. <c>GetConfig()</c> runs on the campaign tick, so the merged
/// config is rebuilt only when this struct's value changes; equality therefore has to cover EVERY
/// knob, or that knob's slider would appear dead until the next restart.
/// </summary>
public readonly struct FieldCommissionMcmSnapshot
{
    public FieldCommissionMcmSnapshot(
        bool? enabled,
        float? ratioThreshold,
        int? meritPerKill,
        int? meritThreshold,
        int? retainerAllowance,
        int? maxOffersPerBattle,
        bool? diagnostics = null)
    {
        Enabled = enabled;
        RatioThreshold = ratioThreshold;
        MeritPerKill = meritPerKill;
        MeritThreshold = meritThreshold;
        RetainerAllowance = retainerAllowance;
        MaxOffersPerBattle = maxOffersPerBattle;
        Diagnostics = diagnostics;
    }

    public bool? Enabled { get; }
    public float? RatioThreshold { get; }
    public int? MeritPerKill { get; }
    public int? MeritThreshold { get; }
    public int? RetainerAllowance { get; }
    public int? MaxOffersPerBattle { get; }
    public bool? Diagnostics { get; }

    public bool Equals(FieldCommissionMcmSnapshot other) =>
        Enabled == other.Enabled
        // Nullable.Equals, not `==`, deliberately: this is a cache key, not a tolerance test, and it
        // routes through float.Equals, for which NaN DOES equal NaN. Plain `==` would report a NaN
        // slider as changed on every read and rebuild the merged config every tick, forever.
        && Nullable.Equals(RatioThreshold, other.RatioThreshold)
        && MeritPerKill == other.MeritPerKill
        && MeritThreshold == other.MeritThreshold
        && RetainerAllowance == other.RetainerAllowance
        && MaxOffersPerBattle == other.MaxOffersPerBattle
        && Diagnostics == other.Diagnostics;

    public override bool Equals(object obj) => obj is FieldCommissionMcmSnapshot other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Enabled.GetHashCode();
            hash = (hash * 31) + RatioThreshold.GetHashCode();
            hash = (hash * 31) + MeritPerKill.GetHashCode();
            hash = (hash * 31) + MeritThreshold.GetHashCode();
            hash = (hash * 31) + RetainerAllowance.GetHashCode();
            hash = (hash * 31) + MaxOffersPerBattle.GetHashCode();
            hash = (hash * 31) + Diagnostics.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(FieldCommissionMcmSnapshot left, FieldCommissionMcmSnapshot right) => left.Equals(right);

    public static bool operator !=(FieldCommissionMcmSnapshot left, FieldCommissionMcmSnapshot right) => !left.Equals(right);
}
