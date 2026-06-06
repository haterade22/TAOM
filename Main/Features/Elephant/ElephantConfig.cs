namespace TAOM.Features.Elephant;

/// <summary>
/// Tuning for the Harad war-elephant. Values are a 1-for-1 port of ADOD_Beasts'
/// <c>ADODBeastsElephantAgentComponent.OnTickAsAI</c> trample + <c>ADODAgentStatCalculateModel</c> mount-lock
/// (decompiled 2026-06-05). "1 for 1 then improve": the trample is a fixed radial knockdown like ADOD's;
/// velocity-scaling / bone-collision polish is the later improve step. See docs/features/elephant.md.
/// </summary>
public static class ElephantConfig
{
    /// <summary>The elephant Monster's StringId (authored in LOTRLOME_Armory). Drives mount identification + lock.</summary>
    public const string ElephantMonsterId = "taom_war_elephant";

    /// <summary>MountDifficulty forced on the elephant so non-rider AI can't take it (ADOD: 999f).</summary>
    public const float MountDifficulty = 999f;

    // --- AI auto-trample gates (1-for-1 ADOD ADODBeastsElephantAgentComponent.OnTickAsAI) ---
    /// <summary>The rider's target must be within this distance of the elephant for a trample (ADOD: 3f).</summary>
    public const float TrampleTargetRange = 3f;
    /// <summary>The elephant must face the target: dot(toTarget, lookDir) above this (ADOD: 0.25f).</summary>
    public const float TrampleFacingDot = 0.25f;
    /// <summary>Per-AI-tick probability the trample fires when gated in (ADOD: 0.001f).</summary>
    public const float TrampleChancePerTick = 0.001f;
    /// <summary>Radius around the target inside which enemies are trampled (ADOD: 2f).</summary>
    public const float TrampleRadius = 2f;
    /// <summary>Base trample damage; halved-to-a-quarter vs blocking targets, doubled for inflicted (ADOD: 10).</summary>
    public const int TrampleBaseDamage = 10;
    /// <summary>Blow magnitude passed to the damage primitive (ADOD blows use ~the base magnitude; 50f is TAOM's default).</summary>
    public const float TrampleBlowMagnitude = 50f;
}
