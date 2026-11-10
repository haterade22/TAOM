namespace TAOM.Features.Realms.Domain.Laws;

/// <summary>Who inherits a title when its holder dies.</summary>
public enum SuccessionLaw
{
    /// <summary>Eldest child, then that child's line, then siblings' lines, then the clan.</summary>
    Primogeniture,

    /// <summary>As primogeniture but the youngest child first.</summary>
    Ultimogeniture,

    /// <summary>The eldest eligible member of the holder's clan.</summary>
    Seniority,

    /// <summary>Whoever leads the holder's clan next. This follows the game's own clan succession.</summary>
    ClanLeader,

    /// <summary>
    /// Kingdom and Empire titles follow the realm's ruler as the game's elections choose them.
    /// Below Kingdom rank this behaves as <see cref="ClanLeader"/>.
    /// </summary>
    Elective,
}

/// <summary>Which heirs are eligible and who is preferred.</summary>
public enum GenderLaw
{
    /// <summary>Men only, and only through the male line.</summary>
    Agnatic,

    /// <summary>Men are preferred; women inherit when no eligible man stands before them.</summary>
    AgnaticCognatic,

    /// <summary>Birth order alone decides.</summary>
    Absolute,
}

public sealed class TitleLaws
{
    public static readonly TitleLaws Default = new(SuccessionLaw.Primogeniture, GenderLaw.AgnaticCognatic);

    public TitleLaws(SuccessionLaw succession, GenderLaw gender)
    {
        Succession = succession;
        Gender = gender;
    }

    public SuccessionLaw Succession { get; }
    public GenderLaw Gender { get; }

    public override bool Equals(object? obj) =>
        obj is TitleLaws other && other.Succession == Succession && other.Gender == Gender;

    public override int GetHashCode() => ((int)Succession * 397) ^ (int)Gender;

    public override string ToString() => $"{Succession}, {Gender}";
}
