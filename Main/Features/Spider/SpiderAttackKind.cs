namespace TAOM.Features.Spider;

/// <summary>Which spider attack a cooldown decorator gates / the service resolves a clip for.</summary>
public enum SpiderAttackKind
{
    /// <summary>The priority lunge — front bite, or the charge variant at speed. Long cooldown.</summary>
    Pounce,

    /// <summary>Left/right swipe picked by the enemy's bearing — fills the gap while the pounce recharges. Short cooldown.</summary>
    SideAttack,
}
