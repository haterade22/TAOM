namespace TAOM.Features.ArmourAcquisition;

/// <summary>MCM live values over the config defaults; every read is null-safe against TaomSettings.Instance.</summary>
public interface IArmourAcquisitionSettingsProvider
{
    /// <summary>The master switch. Read at every game init, so a change applies from the next game load.</summary>
    bool IsEnabled { get; }

    bool LordEventEnabled { get; }

    bool VisitingArmourerEnabled { get; }

    /// <summary>
    /// "Lords' Ladder for co-op players": the co-op server runs the ladder for its players' heroes. Read live in the
    /// server process; a player's own value is not read.
    /// </summary>
    bool CoopGuestLadderEnabled { get; }
}
