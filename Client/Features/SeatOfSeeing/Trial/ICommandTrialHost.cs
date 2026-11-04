using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>Everything the field trial's script reads from the engine and does to it (build-spec section 12), so the script runs against a stand-in in a test.</summary>
internal partial interface ICommandTrialHost
{
    /// <summary>Mission seconds: <c>Mission.CurrentTime</c>. Every step's timer runs on this.</summary>
    float MissionTime { get; }

    bool InDeployment { get; }

    bool InBattle { get; }

    /// <summary>The main agent exists and is active.</summary>
    bool HeroAlive { get; }

    /// <summary>The main agent's controller is still <c>AgentControllerType.Player</c>.</summary>
    bool HeroIsPlayerControlled { get; }

    /// <summary>The main agent's position, or null with no main agent.</summary>
    Vec3f? HeroPosition();

    /// <summary>The mission camera's position, or null when it cannot be read.</summary>
    Vec3f? ViewPosition();

    /// <summary>The ground height under a point, the way the Seat measures it.</summary>
    float GroundHeight(float x, float y);

    /// <summary>Ends the deployment phase, which removes two mission behaviours, so the script calls it from the screen tick only.</summary>
    void FinishDeployment();

    /// <summary>Saves a picture of the screen into the run folder.</summary>
    void Screenshot(string fileName);
}
