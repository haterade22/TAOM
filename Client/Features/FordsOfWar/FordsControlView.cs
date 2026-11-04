using System;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Autorun;
using TAOM.Features.FordsOfWar.Hooks;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// P takes control of the soldier at the pointer, and P hands it back. The soldier's fall hands control back by itself, as the engine clears
/// the main agent, and a match that is over hands the soldier back. The camera view and the panel view ask
/// <see cref="FordsAgentControl.IsControlling"/> each frame and stand aside. The key is read on the screen tick, because a fast forward frame
/// ticks the mission many times. The agent changes on the mission tick, because a view tick overlaps the agent tick (csharp-architecture.md).
/// An armed run with <c>control_check</c> drives the same steps with no keyboard (<see cref="FordsControlCheck"/>). Each change writes a
/// <c>[FordsOfWar][Control]</c> line, and a line <see cref="AfterReturnSeconds"/> later says whether the returned soldier's AI moves it.
/// A fault latches the view off and hands the soldier back, or the player would keep a soldier and have no panel.
/// </summary>
public sealed class FordsControlView : MissionView
{
    private const float AfterReturnSeconds = 10f;

    private FordsSession? _session;
    private FordsControlCheck? _check;
    private Agent? _agent;
    private Agent? _returned;
    private Vec3 _returnedAt;
    private float _returnedTime;
    private bool _pressed;
    private bool _checkRead;
    private bool _failed;

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        _pressed |= !_failed && !MBCommon.IsPaused && Input.IsKeyPressed(InputKey.P);
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        var pressed = _pressed;
        _pressed = false;
        if (_failed || Mission == null)
            return;

        try
        {
            _session ??= IoC.Resolve<FordsSession>();
            if (_session?.IsActive == true)
                Step(pressed);
        }
        catch (Exception ex)
        {
            _failed = true;
            Log()?.LogError($"[FordsOfWar][Control] failed, the soldier goes back to its AI: {ex}");
            FordsAgentControl.Return(Mission);
        }
    }

    private void Step(bool pressed)
    {
        var controlling = FordsAgentControl.IsControlling(Mission);
        if (_agent != null && !controlling)
            Lost(_agent);
        if (_returned != null && Mission.CurrentTime >= _returnedTime + AfterReturnSeconds)
            LogRejoin(_returned);

        if (controlling && _session!.IsOver)
        {
            FordsAgentControl.Return(Mission);
            return;
        }

        if (!_checkRead)
        {
            _checkRead = true;
            var plan = FordsAutorunPlan.Resolve();
            if (plan.Armed && plan.ControlCheckSeconds > 0.0)
                _check = new FordsControlCheck((float)plan.ControlCheckSeconds);
        }

        var step = _check?.Next(Mission.CurrentTime, controlling) ?? FordsControlStep.None;
        if (step == FordsControlStep.None && pressed)
            step = controlling ? FordsControlStep.Return : FordsControlStep.TakePointer;

        switch (step)
        {
            case FordsControlStep.TakePointer:
                Take(FordsPointerPick.AgentAt(Mission, MissionScreen.CombatCamera, Input.GetMousePositionPixel(), Log()), true);
                break;
            case FordsControlStep.TakeFront:
                Take(FordsAgentControl.Front(Mission), false);
                break;
            case FordsControlStep.Return:
                FordsAgentControl.Return(Mission);
                break;
        }
    }

    private void Take(Agent? agent, bool tellThePlayer)
    {
        if (_session!.IsOver)
            return;

        if (agent == null)
        {
            if (tellThePlayer)
                InformationManager.DisplayMessage(new InformationMessage(FordsHudText.ControlNone()));
            return;
        }

        FordsAgentControl.Take(Mission, agent);
        _agent = agent;
        _returned = null;
        Log()?.LogInfo($"[FordsOfWar][Control] took unit={agent.Character?.StringId} hero={(FordsUpgradeHooks.IsHero(agent.Character?.StringId) ? 1 : 0)} mt={Mission.CurrentTime:0.0} mode={Mission.Mode}");
        InformationManager.DisplayMessage(new InformationMessage(FordsHudText.ControlTaken(_session.DisplayName(agent.Character?.StringId ?? ""))));
    }

    private void Lost(Agent agent)
    {
        _agent = null;
        var id = agent.Character?.StringId;
        if (AgentSlotIdentity.IsCurrentOccupant(agent) && agent.IsActive())
        {
            _returned = agent;
            _returnedAt = agent.Position;
            _returnedTime = Mission.CurrentTime;
            Log()?.LogInfo($"[FordsOfWar][Control] returned unit={id} mt={_returnedTime:0.0}");
            return;
        }

        Log()?.LogInfo($"[FordsOfWar][Control] fell unit={id} mt={Mission.CurrentTime:0.0}");
        InformationManager.DisplayMessage(new InformationMessage(FordsHudText.ControlFell(_session!.DisplayName(id ?? ""))));
    }

    private void LogRejoin(Agent agent)
    {
        _returned = null;
        if (!AgentSlotIdentity.IsCurrentOccupant(agent) || !agent.IsActive())
        {
            Log()?.LogInfo($"[FordsOfWar][Control] rejoin unit gone mt={Mission.CurrentTime:0.0}");
            return;
        }

        Log()?.LogInfo($"[FordsOfWar][Control] rejoin unit={agent.Character?.StringId} moved={agent.Position.Distance(_returnedAt):0.0} ai={(agent.IsAIControlled ? 1 : 0)} brain={(agent.HumanAIComponent != null ? 1 : 0)} formation={(agent.Formation != null ? 1 : 0)} mt={Mission.CurrentTime:0.0}");
    }

    private static IModLogger? Log() => IoC.Resolve<IModLogger>();
}
