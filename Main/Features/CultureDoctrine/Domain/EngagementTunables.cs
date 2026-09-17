namespace TAOM.Features.CultureDoctrine.Domain;

/// <summary>
/// The distances every TAOM foot behaviour and the high-ground race decide by, read once from
/// the <c>engagement</c> block of <c>culture_doctrines.json</c> (validated, defaults on any bad
/// field) and captured by each tactic and behaviour at install. From the first A/B battle
/// (2026-09-17): the foot chased every passing eored and the wall walked to a far hill while
/// the horse were on it. Horse matter only while riding at us inside
/// <see cref="CavalryMattersMetres"/> (about the form-up allowance at horse speed: a line needs
/// 8 to 12 s to become a square and horse cover 100 m in that time; the 15 s ETA horizon of
/// <c>CavalryThreat</c> keeps a walking horse formation out of it); a hill is worth a march
/// only inside <see cref="HighGroundMaxMetres"/> and only while no enemy foot formation is
/// inside <see cref="HoldWhenEnemyWithinMetres"/> (horse are the brace's business).
/// </summary>
public readonly struct EngagementTunables
{
    public const float MinMetres = 0f;
    public const float MaxMetres = 500f;

    public static readonly EngagementTunables Default = new EngagementTunables(
        cavalryMattersMetres: 100f, highGroundMaxMetres: 60f, holdWhenEnemyWithinMetres: 50f);

    public EngagementTunables(float cavalryMattersMetres, float highGroundMaxMetres, float holdWhenEnemyWithinMetres)
    {
        CavalryMattersMetres = cavalryMattersMetres;
        HighGroundMaxMetres = highGroundMaxMetres;
        HoldWhenEnemyWithinMetres = holdWhenEnemyWithinMetres;
    }

    /// <summary>An enemy melee cavalry formation riding at us (or already among us) inside this
    /// distance is a threat the wall braces for; a braced wall releases once they are beyond
    /// one and a half times it, then the line forms back up and keeps its foot target.</summary>
    public float CavalryMattersMetres { get; }

    /// <summary>The navmesh high ground is a position only when it is this close; farther is a
    /// march the wall loses the battle on.</summary>
    public float HighGroundMaxMetres { get; }

    /// <summary>Any enemy foot formation inside this distance ends the march: the wall forms
    /// where it stands. Horse do not end it; the wall squares up against them where it is.</summary>
    public float HoldWhenEnemyWithinMetres { get; }
}
