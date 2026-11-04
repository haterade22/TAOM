using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TAOM.Client.Adapters;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The field trial's window onto the engine and the Seat view (build-spec section 12): the reads and actions of <see cref="ICommandTrialHost"/>, on the main thread, with no write to an agent.</summary>
internal sealed partial class CommandTrialHost : ICommandTrialHost
{
    private const float GroundProbeStartHeight = 10000f;

    private readonly SeatView _view;
    private readonly SeatSession _session;
    private readonly SeatScreenAdapter _adapter;
    private readonly Mission _mission;
    private readonly MissionScreen _screen;
    private readonly string _outFolder;

    internal CommandTrialHost(SeatView view, SeatSession session, SeatScreenAdapter adapter, Mission mission, MissionScreen screen)
    {
        _view = view;
        _session = session;
        _adapter = adapter;
        _mission = mission;
        _screen = screen;
        _outFolder = CommandTrialRun.Options!.OutFolder;
    }

    public float MissionTime => _mission.CurrentTime;

    public bool InDeployment => _mission.Mode == MissionMode.Deployment;

    public bool InBattle => _mission.Mode == MissionMode.Battle;

    public bool HeroAlive => _mission.MainAgent != null && _mission.MainAgent.IsActive();

    public bool HeroIsPlayerControlled => _mission.MainAgent?.Controller == AgentControllerType.Player;

    public Vec3f? HeroPosition()
    {
        var agent = _mission.MainAgent;
        if (agent == null || !agent.IsActive())
            return null;

        var position = agent.Position;
        return new Vec3f(position.x, position.y, position.z);
    }

    public Vec3f? ViewPosition()
    {
        var camera = _screen.CombatCamera;
        if (camera == null)
            return null;

        var position = camera.Position;
        return new Vec3f(position.x, position.y, position.z);
    }

    public float GroundHeight(float x, float y)
    {
        var camera = _screen.CombatCamera;
        return _adapter.GroundHeight(x, y, camera != null ? camera.Position.z : GroundProbeStartHeight);
    }

    public void FinishDeployment() => _mission.GetMissionBehavior<DeploymentHandler>()?.FinishDeployment();

    public void Screenshot(string fileName)
    {
        Directory.CreateDirectory(_outFolder);
        Utilities.TakeScreenshot(System.IO.Path.Combine(_outFolder, fileName));
    }
}
