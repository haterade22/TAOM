namespace TAOM.Features.SettlementNameplateRelation;

/// <summary>
/// The four player knobs of #596, already validated and in engine units (fractions, not the MCM
/// percentages). Read on two hot paths: the Patch38 postfix (~3000 calls/sec) reads the two
/// opacities, and every settlement plate's late update reads the toggle and strength once per
/// frame. Implementations must be cheap and thread-safe (stateless reads through a cached
/// reference), and must never return NaN or a value outside the documented range.
/// </summary>
public interface INameplateRelationSettingsProvider
{
    /// <summary>Master toggle. False paints every plate as neutral.</summary>
    bool ColorsEnabled { get; }

    /// <summary>0 to 1: how far each palette entry is applied, 0 being identity (parchment, black text).</summary>
    float TintStrength { get; }

    /// <summary>Target alpha of an untracked neutral plate inside the window (vanilla 0.35).</summary>
    float NeutralPlateAlpha { get; }

    /// <summary>Target alpha of an untracked own-faction, enemy or allied plate (vanilla own faction 0.5).</summary>
    float RelationPlateAlpha { get; }
}
