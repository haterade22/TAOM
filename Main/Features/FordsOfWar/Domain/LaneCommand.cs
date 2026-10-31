namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// What a lane formation should do. One record with a Kind keeps value equality, which the engine layer uses to apply a
/// command only when it differs from the last one. Target is meaningful for MoveTo and HoldAt, TowerId for AttackTower.
/// </summary>
internal sealed record LaneCommand(LaneCommandKind Kind, Vec3f Target, string? TowerId)
{
    public static LaneCommand None { get; } = new(LaneCommandKind.None, default, null);

    public static LaneCommand Charge { get; } = new(LaneCommandKind.Charge, default, null);

    public static LaneCommand MoveTo(Vec3f target) => new(LaneCommandKind.MoveTo, target, null);

    public static LaneCommand HoldAt(Vec3f target) => new(LaneCommandKind.HoldAt, target, null);

    public static LaneCommand AttackTower(string towerId) => new(LaneCommandKind.AttackTower, default, towerId);
}
