using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Adapters;

/// <summary>
/// The Seat's writes to the hero's agent (build-spec 5.5), through the engine's <c>Agent</c> and nothing else. With the controller
/// gate shut nothing rewrites what the controller last wrote, so a hero would keep walking, swinging or holding a block; and
/// <c>LookTick</c> wrote <c>HeadCameraMode</c>, so a first-person hero would hang in the sky view with no head. The agent stays
/// <c>AgentControllerType.Player</c>, which keeps <c>Agent.IsCameraAttachable</c> and the order panel's open check passing. It holds
/// no state.
/// </summary>
internal sealed class SeatHeroAdapter : ISeatHeroAdapter
{
    private readonly IModLogger _logger;

    public SeatHeroAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public void StandFast()
    {
        MissionThreadGuard.NoteCall("SeatHeroAdapter.StandFast", _logger.LogError);

        var agent = Mission.Current?.MainAgent;
        if (agent == null || !agent.IsActive())
            return;

        agent.MovementFlags = default;
        agent.MovementInputVector = Vec2.Zero;
        agent.EventControlFlags = default;
        agent.HeadCameraMode = false;
    }
}
