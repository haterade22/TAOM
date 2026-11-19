namespace TAOM.Features.Elephant;

/// <summary>How one crew seat keeps its archer on a moving platform, in the #1482 measuring build.</summary>
internal enum CrewCarryArm
{
    /// <summary>Today's behaviour: a teleport to the seat once the archer is past the seat's band.</summary>
    Baseline,
    /// <summary>No correction by the seat: what the engine does with an archer standing on a moving platform. The
    /// scripted position still teleports it while Mission.IsTeleportingAgents is set (deployment), uncounted, so
    /// read only the periods after deployment.</summary>
    NoCorrection,
    /// <summary>Agent.SetForceAttachedEntity on the platform root, as vanilla ClimbingMachine does on a ship.</summary>
    ForceAttach,
    /// <summary>A position target every tick (Agent.SetTargetPosition and SetTargetZ), never a teleport.</summary>
    DrivenTarget,
    /// <summary>A placement at the archer's platform-local offset whenever the platform moved from under it.</summary>
    ManagedCarry,
}

/// <summary>
/// TEMPORARY (#1482): which seat tests which way of carrying a crew, so one battle compares them on the same beast
/// at the same moment, speed and target set. Delete this file, its tests and every losing arm once the run has
/// chosen; the seats then keep one behaviour.
/// </summary>
internal static class CrewCarryExperiment
{
    /// <summary>Status periods per block of the platform-wide arm. Four 5 s periods, so an edge is a status line.</summary>
    private const int PeriodsPerBlock = 4;

    /// <summary>
    /// How far from its seat an archer of a no-correction arm may get before the seat places it back, in metres.
    /// Far outside every band under test: this only brings back an archer that fell off its deck.
    /// </summary>
    public const float RescueBandMetres = 3f;

    /// <summary>The arm of a mumakil seat, by its entity name in taom_mumakil_platform.xml.</summary>
    public static CrewCarryArm ForMumakilSeat(string seatEntityName)
    {
        switch (seatEntityName)
        {
            // The rear mirror pair: one attached, one as today.
            case "mumakil_crew_main_1": return CrewCarryArm.ForceAttach;
            case "mumakil_crew_main_4": return CrewCarryArm.Baseline;
            // The front mirror pair: the two managed ways.
            case "mumakil_crew_main_2": return CrewCarryArm.DrivenTarget;
            case "mumakil_crew_main_3": return CrewCarryArm.ManagedCarry;
            case "mumakil_crew_main_0": return CrewCarryArm.NoCorrection;
            case "mumakil_crew_upper_0": return CrewCarryArm.ForceAttach;
            case "mumakil_crew_upper_1": return CrewCarryArm.NoCorrection;
            // The nest shot in both 2026-11-18 runs with no teleport: it stays the control.
            default: return CrewCarryArm.Baseline;
        }
    }

    /// <summary>The arm of a howdah seat, by its entity name in taom_howdah_platform.xml.</summary>
    public static CrewCarryArm ForHowdahSeat(string seatEntityName) =>
        seatEntityName == "howdah_crew_back" ? CrewCarryArm.ForceAttach : CrewCarryArm.Baseline;

    /// <summary>
    /// True while the platform is moved with isTeleportation false, the way vanilla SiegeWeaponMovementComponent
    /// moves a siege tower. It alternates in blocks so every seat arm is measured with and without it.
    /// </summary>
    public static bool NonTeleportBlock(int statusPeriodsElapsed) =>
        statusPeriodsElapsed >= 0 && statusPeriodsElapsed / PeriodsPerBlock % 2 == 1;
}
