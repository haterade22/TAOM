namespace TAOM.Features.StagedSiege.Domain;

/// <summary>The mission result a staged end becomes. Custom Battle shows a pushed-back result as a defeat, so it never gets that one.</summary>
public enum StagedResultKind
{
    DefenderPushedBack,
    Successful,
    Defeated,
}

/// <summary>Maps how a staged siege ended to the result the mission behavior hands the engine.</summary>
public static class StagedResult
{
    /// <summary>Null for <see cref="StagedEnd.None"/>: nothing has ended.</summary>
    public static StagedResultKind? For(StagedEnd end)
    {
        switch (end)
        {
            case StagedEnd.DefenderPushedBack: return StagedResultKind.DefenderPushedBack;
            case StagedEnd.AttackerWon: return StagedResultKind.Successful;
            case StagedEnd.PlayerDefeated: return StagedResultKind.Defeated;
            default: return null;
        }
    }
}
