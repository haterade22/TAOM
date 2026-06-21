using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues.Templates;

/// <summary>
/// T1 "deliver goods" template: the player accumulates N of a config-sourced item and turns it in to
/// the issue giver. A generic, config-driven re-implementation of the vanilla delivery archetype
/// (e.g. HeadmanNeedsGrain), driven by a <see cref="LotrIssueDefinition"/> carried in by the behavior's
/// OnSelected closure. Entry-point layer (ADR-002): engine plumbing lives here; the pure decisions
/// (count/reward scaling, eligibility, reward grant) delegate to <see cref="ILotrIssueService"/>.
/// </summary>
public class DeliverGoodsLotrIssue : IssueBase
{
    [SaveableField(1)] private string _defId;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;
    private IModLogger _logger;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();
    private IModLogger Logger => _logger ??= IoC.Resolve<IModLogger>();

    public DeliverGoodsLotrIssue(Hero issueOwner, LotrIssueDefinition def)
        : base(issueOwner, CampaignTime.DaysFromNow(30f))
    {
        _def = def;
        _defId = def?.Id;
    }

    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
    }

    private int NeededCount
    {
        get { EnsureDef(); return _def == null ? 1 : Service.ComputeTargetCount(_def, base.IssueDifficultyMultiplier); }
    }

    private string DeliverItemId { get { EnsureDef(); return ResolveItemId(_def?.ItemSource); } }

    // Wave 0 supports "item:<id>"; "category:<id>" sourcing is added in Wave 1.
    internal static string ResolveItemId(string itemSource)
    {
        if (string.IsNullOrEmpty(itemSource)) return null;
        if (itemSource.StartsWith("item:", StringComparison.OrdinalIgnoreCase)) return itemSource.Substring(5);
        return null;
    }

    internal static ItemObject ResolveItem(string itemId)
        => string.IsNullOrEmpty(itemId) ? null : MBObjectManager.Instance?.GetObject<ItemObject>(itemId);

    private TextObject Tx(string key, string fallback)
    {
        EnsureDef();
        var t = new TextObject(string.IsNullOrEmpty(key) ? fallback : key);
        if (base.IssueSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", base.IssueSettlement.Name);
        t.SetTextVariable("COUNT", NeededCount);
        var item = ResolveItem(DeliverItemId);
        if (item != null) t.SetTextVariable("ITEM", item.Name);
        return t;
    }

    public override TextObject Title => Tx(_def?.Text.TitleKey, "{=taom_lotr_issue_fallback_title}A Request for Aid");
    public override TextObject Description => Tx(_def?.Text.DescriptionKey, "{=taom_lotr_issue_fallback_desc}A local needs goods delivered.");
    public override TextObject IssueBriefByIssueGiver => Tx(_def?.Text.BriefKey, "{=taom_lotr_issue_fallback_brief}We are in need, traveller.");
    public override TextObject IssueAcceptByPlayer => Tx(_def?.Text.AcceptKey, "{=taom_lotr_issue_fallback_accept}How can I help?");
    public override TextObject IssueQuestSolutionExplanationByIssueGiver => Tx(_def?.Text.ExplanationKey, "{=taom_lotr_issue_fallback_expl}Bring us {COUNT} {ITEM} and we will be saved.");
    public override TextObject IssueQuestSolutionAcceptByPlayer => Tx(_def?.Text.SolutionAcceptKey, "{=taom_lotr_issue_fallback_soln}I will bring what you need.");

    public override bool IsThereAlternativeSolution => false;
    public override bool IsThereLordSolution => false;

    // All 14 DeliverGoods configs share this one type; without this the engine's default same-type accept
    // gate (IssueBase.CheckPreconditions) would cap the player at ONE active DeliverGoods quest. Each
    // config is a distinct issue, so allow concurrent duplicates.
    protected override bool IssueQuestCanBeDuplicated => true;

    protected override int RewardGold
    {
        get { EnsureDef(); return _def == null ? 0 : Service.ComputeRewardGold(_def, base.IssueDifficultyMultiplier); }
    }

    public override IssueFrequency GetFrequency()
    {
        EnsureDef();
        if (_def == null) return IssueFrequency.Common;
        switch (_def.Frequency)
        {
            case IssueFrequencyTier.VeryCommon: return IssueFrequency.VeryCommon;
            case IssueFrequencyTier.Rare: return IssueFrequency.Rare;
            default: return IssueFrequency.Common;
        }
    }

    // Keep the issue alive only while its sourced item actually resolves — an unresolvable item_source
    // would otherwise spawn a permanently uncompletable quest. A null here removes the offer cleanly.
    public override bool IssueStayAliveConditions() => ResolveItem(DeliverItemId) != null;

    protected override float GetIssueEffectAmountInternal(IssueEffect issueEffect)
    {
        if (issueEffect == DefaultIssueEffects.SettlementProsperity) return -0.2f;
        if (issueEffect == DefaultIssueEffects.SettlementLoyalty) return -0.5f;
        return 0f;
    }

    protected override void OnGameLoad() => EnsureDef();

    protected override void HourlyTick() { }

    protected override void CompleteIssueWithTimedOutConsequences() { }

    protected override QuestBase GenerateIssueQuest(string questId)
    {
        EnsureDef();
        if (ResolveItem(DeliverItemId) == null)
            Logger?.LogWarning($"LotrIssues '{_defId}': item_source '{_def?.ItemSource}' did not resolve to an ItemObject — the quest would be uncompletable.");
        return new DeliverGoodsLotrIssueQuest(questId, base.IssueOwner, CampaignTime.DaysFromNow(18f),
            _defId, base.IssueDifficultyMultiplier, NeededCount, RewardGold, DeliverItemId);
    }

    protected override bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flag, out Hero relationHero, out SkillObject skill, out int requiredGold)
    {
        skill = null;
        relationHero = null;
        requiredGold = 0;
        flag = PreconditionFlags.None;
        EnsureDef();
        if (issueGiver.GetRelationWithPlayer() < (_def?.RelationMin ?? -10))
        {
            flag |= PreconditionFlags.Relation;
            relationHero = issueGiver;
        }
        if (issueGiver.CurrentSettlement != null
            && FactionManager.IsAtWarAgainstFaction(issueGiver.CurrentSettlement.MapFaction, Hero.MainHero.MapFaction))
        {
            flag |= PreconditionFlags.AtWar;
        }
        return flag == PreconditionFlags.None;
    }
}

/// <summary>
/// The quest spawned when the player takes the <see cref="DeliverGoodsLotrIssue"/> quest path: track the
/// count of the sourced item on the main party, turn it in at the giver's settlement for the reward.
/// Mirrors the vanilla delivery-quest shape (AddDiscreteLog progress + inventory/settlement event tracking
/// + a turn-in dialog), generalized over the config def. Reward grant routes through the pure service.
/// </summary>
public class DeliverGoodsLotrIssueQuest : QuestBase
{
    [SaveableField(1)] private string _defId;
    [SaveableField(2)] private string _itemId;
    [SaveableField(3)] private int _neededCount;
    [SaveableField(4)] private int _rewardGold;
    [SaveableField(5)] private float _difficulty;
    [SaveableField(6)] private JournalLog _acceptedLog;
    [SaveableField(7)] private JournalLog _readyLog;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;
    private ItemObject _cachedItem;
    private bool _itemResolved;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();

    // Resolve the tracked item once. Without this, CountOnPlayer + the logs + Success would each call
    // MBObjectManager.GetObject on every inventory/settlement event + hourly tick. Not saveable — it
    // re-resolves from the saved _itemId after load.
    private ItemObject Item()
    {
        if (_itemResolved) return _cachedItem;
        _cachedItem = DeliverGoodsLotrIssue.ResolveItem(_itemId);
        _itemResolved = true;
        return _cachedItem;
    }

    public DeliverGoodsLotrIssueQuest(string questId, Hero giverHero, CampaignTime duration,
        string defId, float difficulty, int neededCount, int rewardGold, string itemId)
        : base(questId, giverHero, duration, rewardGold)
    {
        _defId = defId;
        _difficulty = difficulty;
        _neededCount = neededCount;
        _rewardGold = rewardGold;
        _itemId = itemId;
        SetDialogs();
        InitializeQuestOnCreation();
    }

    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
    }

    public override TextObject Title
    {
        get
        {
            EnsureDef();
            var t = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.TitleKey)
                ? "{=taom_lotr_issue_fallback_title}A Request for Aid" : _def.Text.TitleKey);
            if (base.QuestGiver?.CurrentSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
            t.SetTextVariable("COUNT", _neededCount);
            return t;
        }
    }

    public override bool IsRemainingTimeHidden => false;

    private TextObject AcceptedLogText
    {
        get
        {
            var t = new TextObject("{=taom_lotr_issue_common_accepted}Bring {COUNT} {ITEM} to {QUEST_SETTLEMENT}.");
            t.SetTextVariable("COUNT", _neededCount);
            var item = Item();
            if (item != null) t.SetTextVariable("ITEM", item.Name);
            if (base.QuestGiver?.CurrentSettlement != null) t.SetTextVariable("QUEST_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
            return t;
        }
    }

    private TextObject ReadyLogText
    {
        get
        {
            var t = new TextObject("{=taom_lotr_issue_common_ready}You have enough to complete the task. Return to {QUEST_SETTLEMENT} to hand it over.");
            if (base.QuestGiver?.CurrentSettlement != null) t.SetTextVariable("QUEST_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
            return t;
        }
    }

    protected override void InitializeQuestOnGameLoad()
    {
        EnsureDef();
        SetDialogs();
    }

    protected override void HourlyTick() { }

    protected override void RegisterEvents()
    {
        CampaignEvents.PlayerInventoryExchangeEvent.AddNonSerializedListener(this, OnPlayerInventoryExchange);
        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        // Food-class deliverables (grain, meat, …) leave the roster via consumption/sharing, not just
        // trade — mirror vanilla HeadmanNeedsGrain's refresh hooks so tracked progress never goes stale.
        CampaignEvents.OnPartyConsumedFoodEvent.AddNonSerializedListener(this, OnPartyConsumedFood);
        CampaignEvents.OnHeroSharedFoodWithAnotherHeroEvent.AddNonSerializedListener(this, OnHeroSharedFoodWithAnotherHero);
        CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
        CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, OnVillageBeingRaided);
    }

    protected override void HourlyTickParty(MobileParty mobileParty)
    {
        if (mobileParty == MobileParty.MainParty) Refresh();
    }

    private int CountOnPlayer()
    {
        var item = Item();
        if (item == null) return 0;
        int n = PartyBase.MainParty.ItemRoster.GetItemNumber(item);
        return n <= _neededCount ? n : _neededCount;
    }

    private void Refresh()
    {
        if (_acceptedLog != null) _acceptedLog.UpdateCurrentProgress(CountOnPlayer());
        CheckReady();
    }

    private void CheckReady()
    {
        if (_readyLog == null && (_acceptedLog?.CurrentProgress ?? 0) >= _neededCount)
        {
            _readyLog = AddLog(ReadyLogText);
        }
        else if (_readyLog != null && (_acceptedLog?.CurrentProgress ?? 0) < _neededCount)
        {
            RemoveLog(_readyLog);
            _readyLog = null;
        }
    }

    private void OnPlayerInventoryExchange(List<(ItemRosterElement, int)> purchased, List<(ItemRosterElement, int)> sold, bool isTrading)
        => Refresh();

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        if (party == MobileParty.MainParty) Refresh();
    }

    private void OnPartyConsumedFood(MobileParty party)
    {
        if (party != null && party.IsMainParty) Refresh();
    }

    private void OnHeroSharedFoodWithAnotherHero(Hero supporter, Hero supported, float influence)
    {
        if (supporter == Hero.MainHero || supported == Hero.MainHero) Refresh();
    }

    private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
    {
        if (prisoner == Hero.MainHero) Refresh();
    }

    private void OnVillageBeingRaided(Village village)
    {
        if (base.QuestGiver?.CurrentSettlement?.Village == village)
            CompleteQuestWithCancel(CancelLogText);
    }

    private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
    {
        if (base.QuestGiver?.CurrentSettlement?.MapFaction != null
            && base.QuestGiver.CurrentSettlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
            CompleteQuestWithCancel(CancelLogText);
    }

    private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
    {
        QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest(this, faction1, faction2, detail, CancelLogText, CancelLogText, forceCancel: true);
    }

    private TextObject CancelLogText => new TextObject("{=taom_lotr_issue_common_cancel}The agreement has been cancelled.");

    protected override void OnTimedOut()
    {
        AddLog(new TextObject("{=taom_lotr_issue_common_timeout}You failed to deliver the goods in time."));
    }

    protected override void SetDialogs()
    {
        var npcAccept = new TextObject("{=taom_lotr_issue_common_offer_accept}Thank you. I will not forget this kindness.");
        var npcHave = new TextObject("{=taom_lotr_issue_common_have_you}Have you brought what we need?");
        var npcThanks = new TextObject("{=taom_lotr_issue_common_thanks}Bless you, traveller. You have saved us.");
        var npcAwait = new TextObject("{=taom_lotr_issue_common_await}We await your return.");

        OfferDialogFlow = DialogFlow.CreateDialogFlow("issue_classic_quest_start")
            .NpcLine(npcAccept)
            .Condition(() => CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject)
            .Consequence(QuestAcceptedConsequences)
            .CloseDialog();

        DiscussDialogFlow = DialogFlow.CreateDialogFlow("quest_discuss")
            .NpcLine(npcHave)
            .Condition(() => CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject)
            .BeginPlayerOptions()
            .PlayerOption(new TextObject("{=taom_lotr_issue_common_turnin}Yes. Here it is."))
            .ClickableCondition(TurnInClickableConditions)
            .NpcLine(npcThanks)
            .Consequence(() => Campaign.Current.ConversationManager.ConversationEndOneShot += Success)
            .CloseDialog()
            .PlayerOption(new TextObject("{=taom_lotr_issue_common_working}Not yet — I'm working on it."))
            .NpcLine(npcAwait)
            .CloseDialog()
            .EndPlayerOptions()
            .CloseDialog();
    }

    private bool TurnInClickableConditions(out TextObject explanation)
    {
        // Check live inventory, not the cached log progress — the player may have consumed/sold the
        // deliverable (grain is food) since the log last refreshed.
        if (CountOnPlayer() >= _neededCount)
        {
            explanation = null;
            return true;
        }
        explanation = new TextObject("{=taom_lotr_issue_common_not_enough}You don't have enough yet.");
        return false;
    }

    private void QuestAcceptedConsequences()
    {
        StartQuest();
        EnsureDef();
        var task = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.TaskKey)
            ? "{=taom_lotr_issue_common_task}Collect Goods" : _def.Text.TaskKey);
        task.SetTextVariable("COUNT", _neededCount);
        var item = Item();
        if (item != null) task.SetTextVariable("ITEM", item.Name);
        _acceptedLog = AddDiscreteLog(AcceptedLogText, task, CountOnPlayer(), _neededCount);
    }

    private void Success()
    {
        EnsureDef();
        var item = Item();
        if (item != null && base.QuestGiver?.CurrentSettlement?.Party != null)
        {
            var element = new ItemRosterElement(item, _neededCount);
            GiveItemAction.ApplyForParties(PartyBase.MainParty, base.QuestGiver.CurrentSettlement.Party, in element);
        }
        if (_def != null)
            Service.ApplyRewards(_def, _difficulty, new LotrIssueRewardAdapter(Hero.MainHero));
        RelationshipChangeWithQuestGiver = 5;
        CompleteQuestWithSuccess();
    }
}
