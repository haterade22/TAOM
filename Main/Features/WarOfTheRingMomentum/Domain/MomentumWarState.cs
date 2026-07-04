using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.WarOfTheRingMomentum.Domain;

/// <summary>
/// Whole-war momentum state: both sides + lifecycle flags. Pure math — the tanh
/// soft-cap and the ×100 scale live here. LOTRAOM parity source: WarOfTheRingData
/// (minus its FactionManager/Inquiry side effects, which move to the victory service
/// and behavior).
/// </summary>
public class MomentumWarState
{
    /// <summary>Internal momentum ints are stored ×100 for battle-scaling precision.</summary>
    public const int MomentumScale = 100;

    public MomentumSideData Free { get; } = new();
    public MomentumSideData Evil { get; } = new();

    public bool HasWarStarted { get; private set; }
    public bool HasWarEnded { get; private set; }
    public WarOutcome Victor { get; private set; } = WarOutcome.None;

    /// <summary>Signed momentum: positive = Free ahead, negative = Evil ahead.</summary>
    public float InternalMomentum => (Free.SideMomentum - Evil.SideMomentum) / (float)MomentumScale;

    public MomentumSideData GetSide(MomentumSide side)
    {
        return side == MomentumSide.Free ? Free : Evil;
    }

    public bool DoesKingdomTakePart(string kingdomId)
    {
        if (string.IsNullOrEmpty(kingdomId))
            return false;
        return Free.ContainsKingdom(kingdomId) || Evil.ContainsKingdom(kingdomId);
    }

    public void MarkWarStarted()
    {
        HasWarStarted = true;
    }

    /// <summary>Terminal: first non-None victor wins; later calls no-op.</summary>
    public void MarkWarEnded(WarOutcome victor)
    {
        if (victor == WarOutcome.None) return;
        if (Victor != WarOutcome.None) return;

        Victor = victor;
        HasWarEnded = true;
    }

    /// <summary>Save-load rehydration only.</summary>
    public void RestoreFlags(bool warStarted, bool warEnded, WarOutcome victor)
    {
        HasWarStarted = warStarted;
        HasWarEnded = warEnded;
        Victor = victor;
    }
}
