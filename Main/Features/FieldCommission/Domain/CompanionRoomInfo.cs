namespace TAOM.Features.FieldCommission.Domain;

/// <summary>Pure snapshot of companion-slot headroom for the player's clan.</summary>
public readonly struct CompanionRoomInfo
{
    public CompanionRoomInfo(int currentCompanions, int limit)
    {
        CurrentCompanions = currentCompanions;
        Limit = limit;
    }

    public int CurrentCompanions { get; }
    public int Limit { get; }

    /// <summary>True when there is room under (limit + retainerAllowance).</summary>
    public bool HasRoom(int retainerAllowance) => CurrentCompanions < Limit + System.Math.Max(0, retainerAllowance);
}
