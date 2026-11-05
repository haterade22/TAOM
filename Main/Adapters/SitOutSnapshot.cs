namespace TAOM.Adapters;

/// <summary>
/// One flat read of what the sit-out policy needs from the live encounter (#830). Every field is
/// nullable because each is read in its own try: a field the adapter could not read is null, and the
/// policy treats a null it needs as "no opinion", which leaves vanilla's option alone. A default
/// snapshot therefore reads as "no opinion" too.
/// </summary>
public readonly struct SitOutSnapshot
{
    public SitOutSnapshot(bool? wounded, bool? attackAvailable, bool? isSiege, float? playerSideShare)
    {
        Wounded = wounded;
        AttackAvailable = attackAvailable;
        IsSiege = isSiege;
        PlayerSideShare = playerSideShare;
    }

    /// <summary>The main hero is wounded.</summary>
    public bool? Wounded { get; }

    /// <summary>Vanilla's own 'Attack' option would be enabled right now (honour check and attack condition).</summary>
    public bool? AttackAvailable { get; }

    /// <summary>
    /// The player's map event is a siege assault or a sally-out. Every other fortification battle (a
    /// relief battle outside the walls, a siege ambush, a naval blockade) is false and follows the
    /// field rules.
    /// </summary>
    public bool? IsSiege { get; }

    /// <summary>The player's side strength divided by both sides' strength, in [0, 1]; null when it cannot be computed.</summary>
    public float? PlayerSideShare { get; }
}
