using System.Collections.Generic;

namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// Tuning for the trolls' Brute Force smash (#649). First guesses, tuned in the Custom Battle smoke.
/// Distances are metres at body scale 1 and grow with the troll's body size (<c>AgentScale</c> times its Monster's eye
/// height over <see cref="ReferenceEyeHeight"/>, <see cref="ITrollBruteForceService.BodySize"/>), capped at
/// <see cref="MaxBodyScale"/>. The action and its bindings live in the UNVERSIONED LOTRLOME_Armory
/// (<c>action_types.xml</c>; <c>as_cave_troll_warrior</c> through <c>tools/bind_troll_action_set.py</c>,
/// <c>as_hill_troll_warrior</c> through <c>tools/bind_hill_troll_action_set.py</c>).
/// </summary>
public static class TrollBruteForceConfig
{
    /// <summary>The cave troll's battle Monster. The settlement and child variants never get the tree.</summary>
    public const string CaveTrollMonsterId = "cave_troll";

    /// <summary>The hill troll's battle Monster (its own <c>troll_skeleton_a</c> since 2026-09-24); same rule for its variants.</summary>
    public const string HillTrollMonsterId = "hill_troll";

    public const string ActionName = "act_troll_brute_force";
    public const string CaveTrollActionSetId = "as_cave_troll_warrior";
    public const string HillTrollActionSetId = "as_hill_troll_warrior";

    /// <summary>
    /// Every Monster that gets the tree, with the standalone action set that binds <see cref="ActionName"/> for it.
    /// The agent plays the action by name and its own set picks the clip: the Fab heavy attack on the cave troll's
    /// human skeleton, the same clip retargeted onto the hill troll's own rig.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ActionSetsByMonster = new Dictionary<string, string>
    {
        [CaveTrollMonsterId] = CaveTrollActionSetId,
        [HillTrollMonsterId] = HillTrollActionSetId,
    };

    /// <summary>"Medium" (Mike): between Sauron's sweep (12 s) and slam (20 s). Mission time, so a pause does not count.</summary>
    public const float CooldownSeconds = 15f;

    /// <summary>An enemy this close and in front starts the smash.</summary>
    public const float TriggerRange = 2.6f;

    /// <summary>Strictly above this cosine counts as in front (about 60 degrees either side).</summary>
    public const float FacingDot = 0.5f;

    /// <summary>The ring's centre, this far ahead of the troll: where the weapon lands.</summary>
    public const float ImpactForward = 1.8f;

    public const float InnerRadius = 1.5f;
    public const float OuterRadius = 3.5f;

    /// <summary>Blunt damage inside the inner radius; the engine's area falloff takes it to a ninth at the edge.</summary>
    public const int CentreDamage = 40;

    /// <summary>A victim blocking with a shield takes this share and is pushed, not floored.</summary>
    public const float ShieldBlockedMultiplier = 0.5f;

    public const float BlowMagnitude = 60f;

    /// <summary>
    /// Clip progress (0 to 1) at which the weapon lands. For the prototype on the Fab <c>anim_troll_attack1</c>
    /// (source <c>troll_danger_attack_0.fbx</c>, 73 frames at 30 fps): the right hand peaks overhead at frame
    /// index 27 (1.90 m) and bottoms out at 42 (0.65 m), 42/72 = 0.58, measured in Blender 2026-09-24. Re-measure
    /// on the refined clip; the smoke's log line shows the progress at which the ring fired.
    /// </summary>
    public const float ImpactFraction = 0.58f;

    public const float MaxBodyScale = 3f;

    /// <summary>
    /// The human Monster's <c>standing_eye_height</c>, which the cave troll's Monster keeps: its size is all
    /// <c>AgentScale</c> (human skeleton, <c>min_scale</c> 1.9). The hill troll's size is in its own skeleton (eye
    /// height 3.58 at a scale near 1.09), so body size is <c>AgentScale</c> times eye height over this (Mike,
    /// 2026-09-25); the cave troll's tuning is unchanged by construction.
    /// </summary>
    public const float ReferenceEyeHeight = 1.70f;
}
