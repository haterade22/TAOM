namespace TAOM.Features.Stamina.Domain;

/// <summary>The multipliers one band puts on a soldier's driven properties. Immutable. A value of 1 changes nothing.</summary>
public sealed class StaminaBandEffects
{
    public static readonly StaminaBandEffects None = new StaminaBandEffects(1f, 1f, 1f, 1f);

    public StaminaBandEffects(float swing, float handling, float move, float aiAttack)
    {
        Swing = swing;
        Handling = handling;
        Move = move;
        AiAttack = aiAttack;
    }

    /// <summary>On SwingSpeedMultiplier and ThrustOrRangedReadySpeedMultiplier.</summary>
    public float Swing { get; }

    /// <summary>On HandlingMultiplier.</summary>
    public float Handling { get; }

    /// <summary>On MaxSpeedMultiplier and CombatMaxSpeedMultiplier. For Exhausted this is the walk factor. Never applied to a rider.</summary>
    public float Move { get; }

    /// <summary>On AIAttackOnDecideChance: a tired AI soldier decides to attack less often.</summary>
    public float AiAttack { get; }
}
