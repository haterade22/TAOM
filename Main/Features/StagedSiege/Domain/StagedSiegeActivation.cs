namespace TAOM.Features.StagedSiege.Domain;

/// <summary>Why a staged siege did or did not start; the first failed guard names the cause in the log.</summary>
public enum ActivationVerdict
{
    Armed,
    NotASiege,
    SallyOut,
    Client,
    NotWallsAssault,
    Disabled,
    NoPlayerSide,
    UnrecordedScene,
}

/// <summary>The arming decision, with every engine reading already taken as a plain bool.</summary>
public static class StagedSiegeActivation
{
    /// <summary>Guards are checked in parameter order.</summary>
    public static ActivationVerdict Decide(bool isSiege, bool hasSallyOut, bool isClient, bool isWallsAssault, bool enabled,
        bool hasPlayerSide, bool hasScene)
    {
        if (!isSiege) return ActivationVerdict.NotASiege;
        if (hasSallyOut) return ActivationVerdict.SallyOut;
        if (isClient) return ActivationVerdict.Client;
        if (!isWallsAssault) return ActivationVerdict.NotWallsAssault;
        if (!enabled) return ActivationVerdict.Disabled;
        if (!hasPlayerSide) return ActivationVerdict.NoPlayerSide;
        if (!hasScene) return ActivationVerdict.UnrecordedScene;
        return ActivationVerdict.Armed;
    }
}
