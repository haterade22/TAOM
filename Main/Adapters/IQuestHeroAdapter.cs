namespace TAOM.Adapters;

/// <summary>
/// The career-quest system's view of a hero: read-only accessors used to evaluate threshold
/// objectives (skill / renown / gold) plus the reward sinks applied on quest completion.
/// ADR-007 boundary — the quest service depends on this, never on <c>Hero</c>.
/// </summary>
public interface IQuestHeroAdapter
{
    string StringId { get; }
    bool IsValid { get; }

    // ── Reads (threshold objective evaluation) ──
    int GetSkillValue(string skillName);
    int ClanRenown { get; }
    int Gold { get; }

    // ── Reward sinks ──
    void AddItemToInventory(string itemId, int count);
    void AddRenown(int amount);
    void AddInfluence(int amount);
}
