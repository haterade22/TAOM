using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>
/// The nine vanilla field tactics, each subclassed once so a doctrine can scale its weight.
/// <c>GetTacticWeight</c> is <c>protected internal virtual</c> on the base and every vanilla
/// tactic overrides it, so <c>base * multiplier</c> preserves the vanilla formula, including the
/// side effects some of them carry (<c>TacticDefensiveLine.DetermineMainDefensiveLine</c>,
/// <c>TacticDefensiveEngagement</c> assigning <c>_mainInfantry</c>), and costs no Harmony.
///
/// <para>
/// The wrappers keep the vanilla SIMPLE type names on purpose. When the player fights as a
/// sergeant, <c>TeamAIComponent.MakeDecision</c> shows
/// <c>GameTexts.FindText("str_team_ai_tactic_text", tactic.GetType().Name)</c>
/// (`TeamAIComponent.cs:330-334`), and a name with no variation renders an <c>ERROR:</c> string;
/// the same simple name in this namespace resolves the vanilla text with no localisation work.
/// <c>MakeDecision</c>'s forced-charge branch tests <c>item is TacticCharge</c> (`:280`), which a
/// subclass satisfies. Nothing calls <c>RemoveTacticOption(Type)</c> in the engine.
/// </para>
///
/// <para>
/// Every member here runs on the team-AI tick (`Team.Tick` -> `TeamAI.Tick`: the async AI
/// thread in normal play, the main thread during deployment and fast-forward). The only state
/// is a <c>readonly float</c> captured at construction: no IoC, no settings, no logging.
/// </para>
/// </summary>
public sealed class TacticCharge : global::TaleWorlds.MountAndBlade.TacticCharge
{
    private readonly float _multiplier;
    public TacticCharge(Team team, float multiplier) : base(team) => _multiplier = multiplier;
    protected override float GetTacticWeight() => base.GetTacticWeight() * _multiplier;
}

public sealed class TacticFullScaleAttack : global::TaleWorlds.MountAndBlade.TacticFullScaleAttack
{
    private readonly float _multiplier;
    public TacticFullScaleAttack(Team team, float multiplier) : base(team) => _multiplier = multiplier;
    protected override float GetTacticWeight() => base.GetTacticWeight() * _multiplier;
}

public sealed class TacticDefensiveEngagement : global::TaleWorlds.MountAndBlade.TacticDefensiveEngagement
{
    private readonly float _multiplier;
    public TacticDefensiveEngagement(Team team, float multiplier) : base(team) => _multiplier = multiplier;
    protected override float GetTacticWeight() => base.GetTacticWeight() * _multiplier;
}

public sealed class TacticDefensiveLine : global::TaleWorlds.MountAndBlade.TacticDefensiveLine
{
    private readonly float _multiplier;
    public TacticDefensiveLine(Team team, float multiplier) : base(team) => _multiplier = multiplier;
    protected override float GetTacticWeight() => base.GetTacticWeight() * _multiplier;
}

public sealed class TacticDefensiveRing : global::TaleWorlds.MountAndBlade.TacticDefensiveRing
{
    private readonly float _multiplier;
    public TacticDefensiveRing(Team team, float multiplier) : base(team) => _multiplier = multiplier;
    protected override float GetTacticWeight() => base.GetTacticWeight() * _multiplier;
}

public sealed class TacticFrontalCavalryCharge : global::TaleWorlds.MountAndBlade.TacticFrontalCavalryCharge
{
    private readonly float _multiplier;
    public TacticFrontalCavalryCharge(Team team, float multiplier) : base(team) => _multiplier = multiplier;
    protected override float GetTacticWeight() => base.GetTacticWeight() * _multiplier;
}

public sealed class TacticRangedHarrassmentOffensive : global::TaleWorlds.MountAndBlade.TacticRangedHarrassmentOffensive
{
    private readonly float _multiplier;
    public TacticRangedHarrassmentOffensive(Team team, float multiplier) : base(team) => _multiplier = multiplier;
    protected override float GetTacticWeight() => base.GetTacticWeight() * _multiplier;
}

public sealed class TacticHoldChokePoint : global::TaleWorlds.MountAndBlade.TacticHoldChokePoint
{
    private readonly float _multiplier;
    public TacticHoldChokePoint(Team team, float multiplier) : base(team) => _multiplier = multiplier;
    protected override float GetTacticWeight() => base.GetTacticWeight() * _multiplier;
}

public sealed class TacticCoordinatedRetreat : global::TaleWorlds.MountAndBlade.TacticCoordinatedRetreat
{
    private readonly float _multiplier;
    public TacticCoordinatedRetreat(Team team, float multiplier) : base(team) => _multiplier = multiplier;
    protected override float GetTacticWeight() => base.GetTacticWeight() * _multiplier;
}
