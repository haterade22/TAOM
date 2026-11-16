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

    /// <summary>Troop tier for a promotable soldier (0 for companions). Shown in the picker: the
    /// tier was the only datum distinguishing two same-named stacks in the source's flow.</summary>
    public int Tier;
}

/// <summary>
/// Warden lifecycle. Promotion consumes one soldier and mints a companion hero
/// (culture-matched template, renamed to the troop).
///
/// <para>On dismantle a warden who was already a companion rejoins the party as a companion. A
/// PROMOTED warden goes back to the ranks: the minted hero is removed with the ApplyByRemove
/// UnwindPromotion uses (no death pop-up; the engine still logs "NAME was lost." to chat) and
/// one of his source troop rejoins the party. Keeping him
/// as a companion made found, promote, dismantle a free companion every time (owner, 15 Nov
/// 2026). A promoted warden who gained family or a role (spouse, children, clan leader,
/// or left the player's companions) stays a companion, because removing him would break
/// those ties. A warden who is not with the refuge party at dismantle (captured, dead) is left
/// where fate put him and the dismantle proceeds. A refuge lost in battle never calls this.</para>
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

    /// <summary>Returns a warden home on dismantle when he is with the refuge: he rejoins the
    /// main party via AddHeroToPartyAction, and a promoted warden without family or a role is
    /// then turned back into one <paramref name="promotedFromTroopId"/> (or his culture's basic
    /// troop when that troop is unknown; he stays a companion when neither resolves). No-op when
    /// the hero is elsewhere (captured, dead), and the caller proceeds.</summary>
    void ReleaseWarden(string wardenHeroId, bool promoted, string promotedFromTroopId);

    /// <summary>Unwinds a promotion whose founding then failed before the hero ever reached the
    /// refuge (engine spawn refusal): removes the never-attached minted companion and refunds the
    /// soldier. No-op for a companion candidate (promoted == false at resolve).</summary>
    void UnwindPromotion(string wardenHeroId, string promotedFromTroopId);
}
