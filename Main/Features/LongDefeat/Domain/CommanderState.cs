using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>How a wave's commander stands: still on the field (or not yet spawned), killed by a defender, felled by the stalemate rule, or gone without a loss (a rout or a deletion).</summary>
internal enum CommanderOutcome
{
    Standing,
    Killed,
    Felled,
    Left,
}

/// <summary>
/// The commander of the running wave (phase2-plan.md section 2): his spec, whether he has spawned, how he ended, and one clock per ability. Plain state of one <see cref="SurvivalRun"/>,
/// changed only by it, on the main thread.
/// </summary>
internal sealed class CommanderState
{
    private readonly Dictionary<string, int> _uses;
    private int _usesAtEnd;

    internal CommanderState(CommanderSpec spec)
    {
        Spec = spec;
        _uses = spec.Abilities.ToDictionary(a => a.Id, _ => 0);
    }

    public CommanderSpec Spec { get; }

    /// <summary>True once the engine has spawned him: the clocks start then.</summary>
    public bool Up { get; internal set; }

    public CommanderOutcome Outcome { get; internal set; } = CommanderOutcome.Standing;

    public bool Ended => Outcome != CommanderOutcome.Standing;

    /// <summary>Uses of each ability so far; every ability of the spec has an entry, 0 until it first fires.</summary>
    public IReadOnlyDictionary<string, int> Uses => _uses;

    /// <summary>Uses after his end: 0 by construction, measured against the count at the moment he ended so a clock that failed to stop would show.</summary>
    public int UsesAfterEnd => Ended ? _uses.Values.Sum() - _usesAtEnd : 0;

    /// <summary>Run wave seconds when he spawned, and how long he stood once he ended (0 before).</summary>
    public float UpSeconds { get; internal set; }

    public float AliveSeconds { get; internal set; }

    internal List<(CommanderAbility Ability, CommanderClock Clock)> Clocks { get; } = new();

    internal void Use(string abilityId) => _uses[abilityId]++;

    internal void End(CommanderOutcome outcome, float waveSeconds)
    {
        Outcome = outcome;
        AliveSeconds = Up ? waveSeconds - UpSeconds : 0f;
        _usesAtEnd = _uses.Values.Sum();
        foreach (var (_, clock) in Clocks)
            clock.Stop();
    }
}
