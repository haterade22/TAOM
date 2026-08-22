using System.Collections.Generic;

namespace TAOM.Features.Refuge;

/// <summary>One assignable warden candidate for the picker.</summary>
public sealed class WardenCandidate
{
    /// <summary>Hero StringId for a companion; troop CharacterObject StringId for a promotable soldier.</summary>
    public string Id;

    public string DisplayName;

    /// <summary>True = an existing companion; false = a soldier who would be promoted.</summary>
    public bool IsCompanion;
}

/// <summary>
/// Warden lifecycle. The source module's promoted-warden handling destroyed heroes:
/// dismantling KILLED the promoted hero via KillCharacterAction and refunded the troop, and a
/// refuge lost to a raid orphaned him. Reworked policy, because heroes are precious in TAOM:
///
/// <para>Promotion consumes one soldier and mints a companion hero (culture-matched template,
/// renamed to the troop). On dismantle the promoted warden REMAINS a clan companion; the soldier
/// is not refunded, he became somebody. A warden who is not with the refuge party at dismantle
/// (captured, hospitalised) is left where fate put him and the dismantle proceeds.</para>
/// </summary>
public interface IWardenService
{
    /// <summary>Companions in the party, then promotable soldiers (respecting the clan companion
    /// limit for promotions). Empty when nobody can lead.</summary>
    IReadOnlyList<WardenCandidate> Candidates();

    /// <summary>True when at least one candidate exists.</summary>
    bool AnyAvailable();

    /// <summary>Resolves a candidate into a warden Hero StringId, promoting a soldier when the
    /// candidate is a troop. Null on failure (companion limit reached, troop gone).</summary>
    string ResolveWarden(WardenCandidate candidate, out bool promoted, out string promotedFromTroopId);

    /// <summary>Returns a warden home on dismantle: a companion rejoins the main party when he is
    /// with the refuge; a promoted warden stays a companion (never killed). No-op when the hero is
    /// elsewhere (captured), and the caller proceeds.</summary>
    void ReleaseWarden(string wardenHeroId, bool promoted);
}
