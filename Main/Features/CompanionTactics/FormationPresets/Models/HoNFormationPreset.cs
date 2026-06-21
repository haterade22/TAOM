using System;
using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.CompanionTactics.FormationPresets.Models;

/// <summary>
/// Persistable formation preset. The <see cref="SaveableField"/> attributes mark fields for
/// SaveSystem serialization; class id is registered in <see cref="FormationPresetSaveableTypeDefiner"/>
/// (BaseId 726900601, class 101 — matches the original developer mod for save-import compat).
///
/// NOTE: field id 3 is intentionally retired. It previously held a <c>DateTime _createdAt</c>, which
/// the TaleWorlds SaveSystem cannot serialize (DateTime is not a registered basic/struct type) — every
/// campaign save crashed in <c>GameData.Write</c> with a null serialized buffer once a preset existed.
/// The remaining field ids (1,2,4,5,6) are unchanged so any already-persisted data still maps; the gap
/// at 3 is deliberate (do not reuse it for a non-equivalent field). See docs/features/companion-tactics.md.
/// </summary>
public class HoNFormationPreset
{
    [SaveableField(1)]
    private string _id;

    [SaveableField(2)]
    private string _name;

    [SaveableField(4)]
    private Dictionary<string, int> _heroFormationAssignments;

    [SaveableField(5)]
    private List<string> _captainHeroIds;

    [SaveableField(6)]
    private Dictionary<int, int> _formationClasses;

    public string Id { get => _id; set => _id = value; }
    public string Name { get => _name; set => _name = value; }

    public Dictionary<string, int> HeroFormationAssignments
    {
        get => _heroFormationAssignments ??= new Dictionary<string, int>();
        set => _heroFormationAssignments = value;
    }

    public List<string> CaptainHeroIds
    {
        get => _captainHeroIds ??= new List<string>();
        set => _captainHeroIds = value;
    }

    public Dictionary<int, int> FormationClasses
    {
        get => _formationClasses ??= new Dictionary<int, int>();
        set => _formationClasses = value;
    }

    public HoNFormationPreset()
    {
        _id = Guid.NewGuid().ToString();
        _heroFormationAssignments = new Dictionary<string, int>();
        _captainHeroIds = new List<string>();
        _formationClasses = new Dictionary<int, int>();
    }

    public HoNFormationPreset(string name) : this()
    {
        _name = name;
    }

    public bool IsCaptain(string heroId) =>
        _captainHeroIds != null && _captainHeroIds.Contains(heroId);

    public int GetFormationClass(int formationIndex) =>
        _formationClasses != null && _formationClasses.TryGetValue(formationIndex, out var v) ? v : -1;

    public string GetSummary()
    {
        var captainCount = _captainHeroIds?.Count ?? 0;
        var totalAssign = _heroFormationAssignments?.Count ?? 0;
        var troopCount = totalAssign - captainCount;
        var formCount = 0;
        if (_formationClasses != null)
        {
            foreach (var kvp in _formationClasses)
                if (kvp.Value >= 0) formCount++;
        }
        return $"{formCount} formations, {captainCount} captains, {troopCount} troops";
    }
}
