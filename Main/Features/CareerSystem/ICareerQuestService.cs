using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

/// <summary>
/// Career-quest business logic (pure; no TaleWorlds types). Owns the quest-definition lookup, the
/// hybrid tier gate, per-objective progress math, completion evaluation, and reward application.
/// The <c>CareerQuest</c> QuestBase shell + the campaign behavior are thin and delegate here.
/// </summary>
public interface ICareerQuestService
{
    /// <summary>The authored quest for (career, tier), or null if none — that tier then uses the level gate only.</summary>
    CareerQuestDefinition GetQuestFor(string careerId, int tier);

    /// <summary>Resolve a quest by its id (used by the QuestBase shell to rebuild its definition on load). Null if not found.</summary>
    CareerQuestDefinition GetQuestById(string questId);

    IReadOnlyList<CareerQuestDefinition> GetAllQuests();

    /// <summary>
    /// Hybrid gate: a tier is available when the hero meets the level threshold OR has unlocked it
    /// (e.g. by completing its quest, recorded in the persisted tier-unlock list). Consumers call
    /// this instead of <see cref="ICareerRegistry.IsTierAvailable"/> to pick up quest unlocks.
    /// </summary>
    bool IsTierUnlocked(int heroLevel, int tier, string heroId);

    /// <summary>
    /// New progress for an objective given a freshly observed value. Count objectives accumulate
    /// (current + observed); threshold objectives bank the highest seen (max), so a later drop in
    /// gold/renown can't un-progress the quest.
    /// </summary>
    int ComputeProgress(CareerQuestObjectiveType type, int currentProgress, int observedValue);

    bool IsObjectiveSatisfied(CareerQuestObjectiveDefinition objective, int progress);

    /// <summary>True when every objective's progress (aligned by index) meets its target.</summary>
    bool AreAllObjectivesSatisfied(CareerQuestDefinition quest, IReadOnlyList<int> progress);

    /// <summary>Apply a completed quest's rewards: tier-unlock + flags via career data, engine rewards via the adapter.</summary>
    void ApplyRewards(string heroId, CareerQuestDefinition quest, IQuestHeroAdapter hero);
}
