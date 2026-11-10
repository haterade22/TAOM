using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.HeroDefence.Domain;

/// <summary>
/// One Hero Defence run: a pause, a wave, then another pause, until the lives run out. Plain state, touched only on the main thread.
/// The director applies each tick's <see cref="DefenceEffects"/> to the engine and reports spawns, deaths and leaks back.
/// </summary>
internal sealed partial class HeroDefenceRun
{
    private sealed class HeroEntry
    {
        public HeroEntry(HeroDef def) => Def = def;

        public HeroDef Def { get; }

        public DefenceHeroState State { get; set; } = DefenceHeroState.NotOwned;

        public Vec3f? Post { get; set; }

        public HeroOrder Order => new(Def.Id, Post!.Value);
    }

    private readonly DefencePlan _plan;
    private readonly DefenceRules _r;
    private readonly List<HeroEntry> _roster;

    private DefenceEffects _pending = new();
    private DefencePhase _phase = DefencePhase.Pause;
    private float _left;

    public HeroDefenceRun(DefencePlan plan)
    {
        _plan = plan;
        _r = plan.Rules;
        _roster = plan.Heroes.Select(def => new HeroEntry(def)).ToList();
        Gold = _r.StartingGold;
        Lives = _r.StartingLives;
        _left = _r.FirstPauseSeconds;
    }

    public DefencePhase Phase => _phase;

    /// <summary>The wave in play, or the last wave during a pause. 0 in the first pause.</summary>
    public int Wave { get; private set; }

    public int WavesCleared { get; private set; }

    public int Gold { get; private set; }

    public int Lives { get; private set; }

    public int Kills { get; private set; }

    public float PauseLeft => _left;

    public DefenceResult? Result { get; private set; }

    public DefenceHeroState StateOf(string heroId) => Find(heroId)?.State ?? DefenceHeroState.NotOwned;

    public Vec3f? PostOf(string heroId) => Find(heroId)?.Post;

    /// <summary>Advances the run by one engine tick. A bad <c>Dt</c> (zero, negative, NaN, infinite) changes nothing.</summary>
    public DefenceEffects Tick(DefenceSnapshot s)
    {
        if (!(s.Dt > 0f) || float.IsInfinity(s.Dt))
            return new DefenceEffects();

        var dt = s.Dt;
        if (_phase == DefencePhase.Pause)
            dt = TickPause(dt);

        if (_phase == DefencePhase.Wave)
            TickWave(s with { Dt = dt });

        var effects = _pending;
        _pending = new DefenceEffects();
        return effects;
    }

    /// <summary>A hero left the field. In a wave the hero is out until the next pause. In a pause no wave is at stake, so the hero returns at once.</summary>
    public void HeroDown(string heroId)
    {
        var hero = Find(heroId);
        if (Result != null || hero?.State != DefenceHeroState.Placed)
            return;

        if (_phase == DefencePhase.Pause)
            _pending.HeroSpawns.Add(hero.Order);
        else
            hero.State = DefenceHeroState.Fallen;
    }

    private HeroEntry? Find(string heroId) => _roster.FirstOrDefault(hero => hero.Def.Id == heroId);

    /// <summary>Returns the part of <paramref name="dt"/> after the pause ran out, so the wave is charged only for its own time.</summary>
    private float TickPause(float dt)
    {
        _left -= dt;
        if (_left > 0f)
            return 0f;

        var rest = -_left;
        StartWave();
        return rest;
    }

    /// <summary>The purse stops at <see cref="int.MaxValue"/>. A negative or NaN amount adds nothing.</summary>
    private void AddGold(double amount)
    {
        if (amount > 0d)
            Gold = (int)Math.Min(int.MaxValue, Gold + amount);
    }

    private void End()
    {
        _phase = DefencePhase.Ended;
        Result = new DefenceResult(WavesCleared, Kills);
    }
}
