namespace TAOM.Features.MarshalsWord.Domain;

internal enum FireMode
{
    Free,
    OnWord,
    Massed,
}

internal enum DrawState
{
    Idle,
    Drawing,
    Holding,
}

internal enum DrawAction
{
    Leave,
    Hold,
    Release,
}

/// <summary>What one callback read: the AI's own attack and defend bits, whether the soldier stands aside, whether a word is due, and the mission time.</summary>
internal readonly record struct DrawInput(bool Attacking, bool Cancelling, bool StandAside, bool WordDue, double Now);

/// <summary>The soldier's next draw state, when that state began, and what the callback writes (build-spec 7.2).</summary>
internal readonly record struct DrawStep(DrawState State, double Since, DrawAction Action);

/// <summary>Why a soldier stands aside and leaves the AI's flags alone (build-spec 7.2).</summary>
internal readonly record struct StandAsideFacts(bool ModeFree, bool NoFormation, bool NoRangedWielded, bool HoldFireOrder, bool Detached,
    bool UsingObject, double TargetDistance);

internal static class MarshalFireRules
{
    internal static DrawStep Step(DrawState state, double since, in DrawInput input)
    {
        if (input.StandAside)
            return state == DrawState.Holding ? new DrawStep(DrawState.Idle, input.Now, DrawAction.Release) : new DrawStep(DrawState.Idle, since, DrawAction.Leave);

        switch (state)
        {
            case DrawState.Idle:
                return input.Attacking ? new DrawStep(DrawState.Drawing, input.Now, DrawAction.Leave) : new DrawStep(DrawState.Idle, since, DrawAction.Leave);
            case DrawState.Drawing:
                if (input.Attacking)
                    return new DrawStep(DrawState.Drawing, since, DrawAction.Leave);
                return input.Cancelling ? new DrawStep(DrawState.Idle, since, DrawAction.Leave) : new DrawStep(DrawState.Holding, input.Now, DrawAction.Hold);
            default:
                return input.WordDue || input.Now - since >= MarshalTuning.HoldLimit
                    ? new DrawStep(DrawState.Idle, input.Now, DrawAction.Release)
                    : new DrawStep(DrawState.Holding, since, DrawAction.Hold);
        }
    }

    internal static bool WordDue(double wordTime, double since, double jitter, double now) => wordTime >= since && wordTime + jitter <= now;

    internal static bool StandsAside(in StandAsideFacts facts) =>
        facts.ModeFree || facts.NoFormation || facts.NoRangedWielded || facts.HoldFireOrder || facts.Detached || facts.UsingObject
        || !(facts.TargetDistance >= MarshalTuning.CloseQuarter);

    internal static FireMode NextMode(FireMode mode) => mode switch
    {
        FireMode.Free => FireMode.OnWord,
        FireMode.OnWord => FireMode.Massed,
        _ => FireMode.Free,
    };

    /// <summary>The soldiers a Massed word counts against: those who hold, draw or stand ready, less the ones whose draw is stale. A soldier who stands aside is not among them.</summary>
    internal static int Eligible(int holding, int drawing, int idle, int staleDrawing) => holding + drawing + idle - staleDrawing;

    internal static bool IsStaleDraw(double since, double now) => now - since > MarshalTuning.DrawLimit;

    internal static bool MassedWordDue(int holding, int eligible, double now, double lastWord) =>
        holding >= 1 && eligible >= holding && holding >= MarshalTuning.ReadyShare * eligible - 1e-9 && now - lastWord >= MarshalTuning.WordGap;
}
