using System;
using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Behaviors;

/// <summary>
/// The engine's formation-behaviour lifecycle (<c>BehaviorComponent</c>, `BehaviorComponent.cs`)
/// written once for TAOM's own behaviours, with the same wrapping <c>TaomTacticBase</c> gives
/// the tactics. <c>FormationAI.TickOccasionally</c> (`FormationAI.cs:240-292`) calls
/// <c>PrecalculateMovementOrder</c> (our <see cref="Plan"/>) on any candidate whose provisional
/// weight beats the running maximum (`:169-210`), activates the best (<see cref="Activate"/>), and ticks only the active one
/// (<see cref="OnActiveTick"/> then <see cref="Plan"/>, then the orders are pushed to the
/// formation as every vanilla behaviour does in its own <c>TickOccasionally</c>). So
/// <see cref="Plan"/> must be cheap and side-effect free on any state machine: it reads the
/// current stage and writes the orders; stages advance in <see cref="OnActiveTick"/> only.
///
/// <para>
/// Everything here runs on the team-AI tick (the async AI thread in play; the main thread
/// during deployment, in fast-forward, and for one inline <c>AI.Tick()</c> when the player
/// delegates a formation, `Formation.cs:817-824`): only engine query systems and values captured
/// at construction, no IoC, settings or logger; a throw in any override sets
/// <see cref="Failed"/>, after which the weight is 0 and the formation's other behaviours take
/// it at the next 0.5 s tick. A TAOM behaviour derives from this class, never
/// from a vanilla behaviour: <c>FormationAI.GetBehavior&lt;T&gt;</c> and
/// <c>SetBehaviorWeight&lt;T&gt;</c> match with <c>is T</c> (`FormationAI.cs:120-155`), so a
/// subclass of <c>BehaviorDefend</c> would be found by every vanilla tactic that sets
/// <c>BehaviorDefend</c>'s weight. The sergeant popup reads the type name through
/// <c>str_formation_ai_sergeant_instruction_behavior_text</c>; every subclass has a row.
/// </para>
/// </summary>
public abstract class TaomBehaviorBase : BehaviorComponent
{
    private volatile bool _failed;
    private volatile string _status = "";

    /// <summary>The per-tick target choice of a foot behaviour (<see cref="EnemyScan.Pick"/>);
    /// one per behaviour, refilled on the behaviour's own tick.</summary>
    protected readonly TargetSelection Targets = new TargetSelection();

    /// <summary>The engagement distances, written by <c>BehaviorWeightApplier</c> on the
    /// team-AI tick each time a plan names this behaviour, before the behaviour's first tick on
    /// that same call (<c>Team.Tick</c> runs <c>TeamAI.Tick</c> to completion, then every
    /// formation's tick, sequentially, `Team.cs:585-623`); never written from anywhere else.
    /// Which thread runs that call varies (the async AI thread in play, the main thread for
    /// the one <c>Team.Tick(0)</c> of deployment), but writer and reader are always the same
    /// call, so no two threads ever touch one behaviour's state at once.</summary>
    public EngagementTunables Engagement = EngagementTunables.Default;

    protected TaomBehaviorBase(Formation formation)
        : base(formation)
    {
    }

    public bool Failed => _failed;

    /// <summary>One word for the status line: the behaviour's current stage or stance.</summary>
    public string Status => _status;

    protected abstract float Weigh();

    /// <summary>Write <c>CurrentOrder</c> and <c>CurrentFacingOrder</c> from the current stage.</summary>
    protected abstract void Plan();

    /// <summary>Arrangement, form and firing orders for the behaviour's first tick as the active one.</summary>
    protected virtual void Activate()
    {
    }

    /// <summary>Advance any stage machine and re-issue arrangement; active behaviour only, before <see cref="Plan"/>.</summary>
    protected virtual void OnActiveTick()
    {
    }

    protected virtual void Canceled()
    {
    }

    protected void SetStatus(string status) => _status = status;

    protected sealed override float GetAiWeight()
    {
        if (_failed)
            return 0f;
        try
        {
            return Weigh();
        }
        catch (Exception ex)
        {
            Fail(ex);
            return 0f;
        }
    }

    protected sealed override void CalculateCurrentOrder()
    {
        if (_failed)
            return;
        try
        {
            Plan();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    protected sealed override void OnBehaviorActivatedAux()
    {
        if (_failed)
            return;
        try
        {
            // Activate first: it resets the brace, target and stage fields a previous
            // activation left behind, which the first Plan would otherwise push as an order.
            Activate();
            Plan();
            Formation.SetMovementOrder(CurrentOrder);
            Formation.SetFacingOrder(CurrentFacingOrder);
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    public sealed override void TickOccasionally()
    {
        if (_failed)
            return;
        try
        {
            OnActiveTick();
            Plan();
            Formation.SetMovementOrder(CurrentOrder);
            Formation.SetFacingOrder(CurrentFacingOrder);
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    public sealed override void OnBehaviorCanceled()
    {
        try
        {
            Canceled();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    // The soldier popup prints ToString() with "MBModule.Behavior" removed; vanilla types print
    // their full name there too, so the simple name is the readable choice.
    public override string ToString() => GetType().Name;

    private void Fail(Exception ex)
    {
        _failed = true;
        _status = "failed: " + ex.GetType().Name + ": " + ex.Message;
    }
}
