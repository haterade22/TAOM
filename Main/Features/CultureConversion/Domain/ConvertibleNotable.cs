namespace TAOM.Features.CultureConversion.Domain;

/// <summary>
/// Boundary snapshot of one settlement notable (ADR-007 — no TaleWorlds types). Produced by
/// <c>ICultureConversionAdapter.GetNotables</c>; the service uses it to decide WHICH notables to
/// replace at conversion (alive + culture mismatch), the adapter owns HOW a single replacement runs.
/// </summary>
public sealed class ConvertibleNotable
{
    public ConvertibleNotable(string heroId, string cultureId, bool isAlive)
    {
        HeroId = heroId;
        CultureId = cultureId;
        IsAlive = isAlive;
    }

    public string HeroId { get; }

    /// <summary>The notable hero's own culture id (not the settlement's), or null if unresolvable.</summary>
    public string CultureId { get; }

    public bool IsAlive { get; }
}
