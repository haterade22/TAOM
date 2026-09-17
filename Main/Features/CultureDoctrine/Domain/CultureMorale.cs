using TAOM.Core.Validation;

namespace TAOM.Features.CultureDoctrine.Domain;

/// <summary>
/// How a culture's soldiers hold under fear. <see cref="NeverRout"/> answers
/// <c>BattleMoraleModel.CanPanicDueToMorale</c> with false, so a soldier whose morale reaches
/// zero stands and fights (<c>CommonAIComponent.OnTickParallel</c> only raises the panic flag
/// when the model allows it); the team-level retreat is a separate knob, the doctrine's
/// <c>CoordinatedRetreat</c> row, which such a culture simply does not carry.
/// <see cref="Bravery"/> is added to every soldier's initial morale
/// (<c>CommonAIComponent.InitializeMorale</c>: 35 plus a random 0 to 29, then the model, then a
/// clamp to 15..100), so a positive value delays the break and a negative one hastens it.
/// Immutable; read on the engine's worker threads.
/// </summary>
public sealed class CultureMorale
{
    public const float MinBravery = -30f;
    public const float MaxBravery = 30f;

    /// <summary>Vanilla: everyone can rout, nobody is braver than the dice.</summary>
    public static readonly CultureMorale Vanilla = new CultureMorale(neverRout: false, bravery: 0f);

    public CultureMorale(bool neverRout, float bravery)
    {
        NeverRout = neverRout;
        Bravery = FiniteFloatValidator.IsFiniteInRange(bravery, MinBravery, MaxBravery) ? bravery : 0f;
    }

    public bool NeverRout { get; }
    public float Bravery { get; }

    public bool CanPanic => !NeverRout;

    /// <summary>The engine's base initial morale plus this culture's bravery; the caller clamps
    /// to its own 15..100 afterwards, so no clamp here beyond finiteness.</summary>
    public float InitialMorale(float baseMorale) =>
        FiniteFloatValidator.IsFinite(baseMorale) ? baseMorale + Bravery : baseMorale;
}
