using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// The damage component <see cref="FordsTowerAdapter"/> attaches at runtime to a tower entity a Fords of War scene built itself, so the
/// prefabs stay free of scripts and shared with other maps. It adds nothing to <see cref="DestructableComponent"/> and must NOT
/// override <c>OnHit</c>, because the tower hit patch is on the base method. The constructor only gives the three string fields the
/// engine reads in <c>OnInit</c> a value: an empty state list skips the destruction-state setup, and the heavy-hit tag is never null
/// because <c>OnInit</c> passes it to <c>HasTag</c> for every child of the tower.
/// </summary>
// PUBLIC, not internal: the engine's script factory resolves a component by public type name (TaomMumakilStandingPoint explains why).
public class FordsTowerComponent : DestructableComponent
{
    /// <summary>A tag no entity carries, so a tower has no heavy-hit particle children.</summary>
    internal const string NoHeavyHitTag = "fow_no_heavy_hit";

    public FordsTowerComponent()
    {
        DestructionStates = "";
        ReferenceEntityTag = "";
        HeavyHitParticlesTag = NoHeavyHitTag;
    }
}
