using System.Collections.Generic;

namespace TAOM.Features.CultureDoctrine.Domain;

/// <summary>
/// Deserialization target for <c>culture_doctrine/culture_doctrines.json</c>. Deserialized with
/// <c>ObjectCreationHandling.Replace</c> so a JSON list or dictionary replaces the compiled value
/// instead of Json.NET's append-merge. Every row is validated by
/// <see cref="CultureDoctrineConfigProvider"/> before it becomes a <see cref="Doctrine"/>.
///
/// Keys of <see cref="Doctrines"/> are culture StringIds, and TAOM re-skins six vanilla cultures
/// without changing their ids: Rohirrim are <c>vlandia</c>, Dunlendings <c>empire</c>, Haradrim
/// <c>aserai</c>, Rhun <c>khuzait</c>, Dale <c>sturgia</c>, Khand <c>battania</c>. A doctrine
/// keyed on the LOTR name applies to nobody. The <c>default</c> key is the fallback for every
/// culture without a row.
/// </summary>
public class CultureDoctrineConfig
{
    public bool Enabled { get; set; } = true;

    public Dictionary<string, DoctrineConfig> Doctrines { get; set; } = new Dictionary<string, DoctrineConfig>();
}

public class DoctrineConfig
{
    public List<TacticEntryConfig> Tactics { get; set; } = new List<TacticEntryConfig>();
}

public class TacticEntryConfig
{
    /// <summary>A <see cref="DoctrineTactic"/> name, case-insensitive, without the engine's
    /// <c>Tactic</c> prefix: <c>Charge</c>, <c>FrontalCavalryCharge</c>, <c>ShieldWall</c>.</summary>
    public string? Id { get; set; }

    /// <summary>Factor on the tactic's own weight, 0 to 5. 0 keeps the row but never picks it.</summary>
    public float Multiplier { get; set; } = 1f;

    /// <summary>Commander Tactics skill the side needs for this row, 0 to 300. Vanilla's own gates
    /// are 20 and 50.</summary>
    public int MinTactics { get; set; }

    /// <summary><c>any</c> (default), <c>attacker</c> or <c>defender</c>.</summary>
    public string? Side { get; set; }
}
