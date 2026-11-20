namespace TAOM.Features.Stamina.Domain;

/// <summary>
/// What an Exhausted soldier's input loses: the attack and defend bits of the movement flags, and the kick and jump
/// event flags. One function for the player's input (the end of the controller's input build) and for an AI soldier's
/// (AgentComponent.OnAIInputSet), so the two paths cannot drift. The masks are the engine's values
/// (Agent.MovementControlFlag.AttackMask, DefendMask and DefendBlock, Agent.EventControlFlag.Jump and Kick, v1.5.7
/// Agent.cs:233, 245, 409-417); StaminaInputRulesTests pins them against the engine enums, so an engine update that
/// moves a bit fails a test. The engine's DefendMask holds the four directions and DefendAuto but not DefendBlock, so
/// that bit is named here too: no managed v1.5.7 code sets it, and whether the native AI does is not known.
/// </summary>
public static class StaminaInputRules
{
    public const uint AttackMask = 0x3C0;
    public const uint DefendMask = 0x7C00;
    public const uint DefendBlock = 0x8000;
    public const uint JumpEvent = 0x8;
    public const uint KickEvent = 0x8000;

    /// <summary>The movement flags without attack and defend. Movement and turn bits stay: the soldier can still walk.</summary>
    public static uint StripMovement(uint movementFlags) => movementFlags & ~(AttackMask | DefendMask | DefendBlock);

    /// <summary>
    /// The event flags without kick, and without jump for a soldier on foot and on land. A rider keeps the jump because
    /// it is the horse's (mounts are left alone), and a swimmer keeps it because the engine uses it to rise in water.
    /// </summary>
    public static uint StripEvents(uint eventFlags, bool mounted, bool inWater)
    {
        var stripped = eventFlags & ~KickEvent;
        return mounted || inWater ? stripped : stripped & ~JumpEvent;
    }
}
