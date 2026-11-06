using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

internal enum CommanderAbilityKind
{
    Summon,
    WarCry,
}

/// <summary>
/// One commander ability, as <c>waves.json</c> "commanders.abilities" gives it. A <see cref="CommanderAbilityKind.WarCry"/> adds its two percents to every attacker's stat record for
/// <see cref="DurationSeconds"/>; a <see cref="CommanderAbilityKind.Summon"/> queues <see cref="Count"/> soldiers split over <see cref="Mix"/> by the wave's own rule.
/// </summary>
internal sealed record CommanderAbility(string Id, CommanderAbilityKind Kind, string Name, float FirstSeconds, float CooldownSeconds)
{
    public float DurationSeconds { get; init; }

    public float SwingSpeedPercent { get; init; }

    public float MoveSpeedPercent { get; init; }

    public int Count { get; init; }

    public IReadOnlyList<WaveTroop> Mix { get; init; } = Array.Empty<WaveTroop>();
}

/// <summary>One rank: the lord who stands as commander, the gold his kill pays, his health factor against a lord's own, and the ability ids he uses. <c>Name</c> is a raw <c>{=KEY}English</c> text.</summary>
internal sealed record CommanderRank(string Id, string Name, string TroopId, int Bounty, float Health, IReadOnlyList<string> AbilityIds);

/// <summary>What each step past the last rank adds (phase2-plan.md section 2): health, a shorter cooldown, more summoned soldiers, up to <c>MaxSteps</c> steps.</summary>
internal sealed record CommanderBeyond(float HealthPerStep, float CooldownFactorPerStep, int SummonPerStep, int MaxSteps)
{
    public static CommanderBeyond None { get; } = new(0f, 1f, 0, 0);
}

/// <summary>The commander of one wave: his rank, the steps past the ranks, his health factor with the steps in, and his abilities with the steps applied to cooldowns and summon counts.</summary>
internal sealed record CommanderSpec(int Wave, CommanderRank Rank, int RankIndex, int Steps, float Health, IReadOnlyList<CommanderAbility> Abilities);

/// <summary>
/// The commanders of a roster (phase2-plan.md section 2): one every <see cref="Every"/> waves, the ranks in order, and once they run out the last rank returns stronger.
/// <see cref="None"/>: no wave has a commander.
/// </summary>
internal sealed class CommanderPlan
{
    /// <summary>A cooldown never falls under this, however many steps shorten it.</summary>
    public const float MinCooldownSeconds = 5f;

    /// <summary>The most soldiers one summon queues, the cap of the reader's range.</summary>
    public const int MaxSummon = 50;

    public static CommanderPlan None { get; } = new(0, Array.Empty<CommanderRank>(), new Dictionary<string, CommanderAbility>(), CommanderBeyond.None);

    public CommanderPlan(int every, IReadOnlyList<CommanderRank> ranks, IReadOnlyDictionary<string, CommanderAbility> abilities, CommanderBeyond beyond)
    {
        Every = every;
        Ranks = ranks;
        Abilities = abilities;
        Beyond = beyond;
    }

    public int Every { get; }

    /// <summary>The ranks that passed validation; their count is the plan's <c>validRanks</c>.</summary>
    public IReadOnlyList<CommanderRank> Ranks { get; }

    public IReadOnlyDictionary<string, CommanderAbility> Abilities { get; }

    public CommanderBeyond Beyond { get; }

    /// <summary>Every troop a summon can spawn, each once: these pay a bounty when killed.</summary>
    public IEnumerable<string> SummonTroopIds => Abilities.Values.SelectMany(a => a.Mix.Select(m => m.TroopId)).Distinct();

    /// <summary>Every troop a commander wave can spawn: the rank troops and every summon troop, each once. For the launch check.</summary>
    public IEnumerable<string> TroopIds => Ranks.Select(r => r.TroopId).Concat(SummonTroopIds).Distinct();

    /// <summary>
    /// The commander of a wave, or null: none unless the wave is a multiple of <see cref="Every"/>. With <c>n = wave / Every</c> and <c>valid</c> the ranks that passed validation, the rank
    /// is <c>min(n - 1, valid - 1)</c> and the steps are <c>min(MaxSteps, max(0, n - valid))</c>, so the first rounds are at full strength with no step. A step multiplies the rank's health by
    /// <c>1 + HealthPerStep * steps</c> and each cooldown by <c>CooldownFactorPerStep ^ steps</c> (never under <see cref="MinCooldownSeconds"/>, and a war cry's never under its duration plus that), and adds <c>SummonPerStep * steps</c> to each summon (never over <see cref="MaxSummon"/>).
    /// </summary>
    public CommanderSpec? ForWave(int wave)
    {
        if (Every <= 0 || Ranks.Count == 0 || wave <= 0 || wave % Every != 0)
            return null;

        var n = wave / Every;
        var index = Math.Min(n - 1, Ranks.Count - 1);
        var steps = Math.Min(Math.Max(0, Beyond.MaxSteps), Math.Max(0, n - Ranks.Count));
        var rank = Ranks[index];
        var abilities = rank.AbilityIds.Where(Abilities.ContainsKey).Select(id => Stepped(Abilities[id], steps)).ToList();
        return new CommanderSpec(wave, rank, index, steps, rank.Health * (1f + Beyond.HealthPerStep * steps), abilities);
    }

    private CommanderAbility Stepped(CommanderAbility ability, int steps)
    {
        if (steps == 0)
            return ability;

        // The reader holds a war cry's duration under its raw cooldown, and Fire overwrites the end of one in force, so a stepped cooldown no longer than the duration would keep it up for good.
        var floor = ability.Kind == CommanderAbilityKind.WarCry ? ability.DurationSeconds + MinCooldownSeconds : MinCooldownSeconds;
        var cooldown = (float)Math.Max(floor, ability.CooldownSeconds * Math.Pow(Beyond.CooldownFactorPerStep, steps));
        var count = ability.Kind == CommanderAbilityKind.Summon ? Math.Min(MaxSummon, ability.Count + Beyond.SummonPerStep * steps) : ability.Count;
        return ability with { CooldownSeconds = cooldown, Count = count };
    }
}
