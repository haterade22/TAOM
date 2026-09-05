using System.Collections.Generic;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Boundary over the main party's roster + <c>CharacterObject</c> troop templates for
/// FieldCommission (ADR-007 — keeps <c>MobileParty</c>/<c>CharacterObject</c>/<c>SkillObject</c> out
/// of the service). Despite the "Query" name this also owns the two roster-count writes: the
/// decrement when a promotion completes (<see cref="RemoveOneFromRoster"/>) and the refund when
/// one is dismissed (<see cref="AddOneToRoster"/>, #540). FieldCommission's scope allows exactly
/// three named adapters, and a troop-count change is roster-troop data in every other sense, so a
/// fourth adapter purely for those writes would be ceremony over substance.
/// </summary>
public interface ITroopRosterQueryAdapter
{
    /// <summary>True when the main party currently exists and has a roster.</summary>
    bool HasMainParty { get; }

    /// <summary>Main party's total healthy troop count (0 when there is no main party).</summary>
    int GetMainPartyHealthyCount();

    /// <summary>Non-hero troop id -> current count in the main party roster. Empty when there is
    /// no main party.</summary>
    IReadOnlyDictionary<string, int> GetRosterSnapshot();

    /// <summary>Current count of the given troop id in the main party roster, or 0 when there is
    /// no main party or the troop isn't present.</summary>
    int GetTroopCount(string troopId);

    /// <summary>Removes one unit of the given troop id from the main party roster. Returns false
    /// (no removal happened) when there is no main party, the troop isn't present, or the id
    /// doesn't resolve.</summary>
    bool RemoveOneFromRoster(string troopId);

    /// <summary>Adds one unit of the given troop id to the main party roster, wounded when
    /// <paramref name="wounded"/>. Returns false (nothing added) when there is no main party, the
    /// id doesn't resolve, or it resolves to a hero template.</summary>
    bool AddOneToRoster(string troopId, bool wounded);

    /// <summary>Snapshot of the troop template, or <see cref="TroopInfo.Missing"/> when the id
    /// doesn't resolve to a <c>CharacterObject</c>.</summary>
    TroopInfo GetTroopInfo(string troopId);

    /// <summary>Upgrade-target troop ids for the given troop id, or an empty list when the id
    /// doesn't resolve or has no upgrade targets.</summary>
    IReadOnlyList<string> GetUpgradeTargetIds(string troopId);

    /// <summary>Skill StringId -> the troop template's value for every skill in
    /// <c>Campaign.Current.AllSkills</c>. Empty when the id doesn't resolve.</summary>
    IReadOnlyDictionary<string, int> GetTroopSkillValues(string troopId);
}
