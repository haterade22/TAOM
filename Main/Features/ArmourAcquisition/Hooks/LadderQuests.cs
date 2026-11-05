using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Quests;

namespace TAOM.Features.ArmourAcquisition.Hooks;

/// <summary>
/// The lord's gear ladder's quests in the engine's quest manager (#693): one CareerQuest per rung and owner, run
/// by the career-quest shell, found and started here for the boundary classes that need them
/// (<see cref="LordsLadderBehavior"/>, <see cref="LadderPresenter"/>). Every decision is
/// <see cref="LordsLadderService"/>'s. Main hero, campaign thread.
/// </summary>
internal static class LadderQuests
{
    /// <summary>
    /// The owner's running quest with this definition id. Per owner, like the ladder's state: another hero's quest on
    /// the same rung (a former player hero's, after an heir took over) must not block this one, and two quests may
    /// share a definition id (the quest list is a plain list). The engine keys map markers and the journal record
    /// (<c>JournalLogEntry.IsRelatedTo</c>) by quest StringId, so <see cref="Start"/> gives each quest its own.
    /// </summary>
    public static CareerQuest? Find(string questId, string ownerHeroId) =>
        Campaign.Current?.QuestManager?.Quests?.OfType<CareerQuest>().FirstOrDefault(q => q.IsOngoing
            && q.CareerQuestDefId == questId && q.OwnerHeroStringId == ownerHeroId);

    /// <summary>Starts a rung's quest for the main hero; false, logged, when its definition is missing or the start throws.</summary>
    public static bool Start(ICareerQuestService quests, string questId, IModLogger logger)
    {
        var def = quests.GetQuestById(questId);
        if (def == null || Hero.MainHero == null)
        {
            logger.LogError($"[ArmourAcquisition] The lord's gear ladder has no career-quest definition '{questId}'.");
            return false;
        }
        try
        {
            var id = CareerQuestService.NewQuestStringId(def.Id, Hero.MainHero.StringId, (long)CampaignTime.Now.ToMilliseconds);
            new CareerQuest(id, Hero.MainHero, def).StartQuest();
            logger.LogInfo($"[ArmourAcquisition] Ladder quest {questId} started for {Hero.MainHero.StringId}.");
            return true;
        }
        catch (Exception ex)
        {
            // Nothing is recorded before the quest starts, so the armoury simply offers it again.
            logger.LogError($"[ArmourAcquisition] Ladder quest {questId} could not start ({ex.GetType().Name}: {ex.Message}).");
            return false;
        }
    }
}
