using System;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues.Templates;

/// <summary>
/// "Deliver personnel" template: the player hands over N bandit prisoners to the giver (a gang pressing
/// recruits, or a landlord wanting forced mine labor). Same offer/turn-in shape as DeliverGoods, but the
/// objective tracks bandit prisoners in the player's <c>PrisonRoster</c> rather than an item. Entry-point
/// layer (ADR-002) — count/reward/eligibility decisions delegate to <see cref="ILotrIssueService"/>.
/// </summary>
public class DeliverPersonnelLotrIssue : IssueBase
{
    [SaveableField(1)] private string _defId;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;
    private IModLogger _logger;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();
    private IModLogger Logger => _logger ??= IoC.Resolve<IModLogger>();

    public DeliverPersonnelLotrIssue(Hero issueOwner, LotrIssueDefinition def)
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

    private TextObject Tx(string key, string fallback)
    {
        EnsureDef();
        var t = new TextObject(string.IsNullOrEmpty(key) ? fallback : key);
        if (base.IssueSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", base.IssueSettlement.Name);
        t.SetTextVariable("COUNT", NeededCount);
        return t;
    }

    public override TextObject Title => Tx(_def?.Text.TitleKey, "{=taom_lotr_issue_fallback_title}A Request for Aid");
    public override TextObject Description => Tx(_def?.Text.DescriptionKey, "{=taom_lotr_issue_fallback_desc}Someone needs captives delivered.");
    public override TextObject IssueBriefByIssueGiver => Tx(_def?.Text.BriefKey, "{=taom_lotr_issue_fallback_brief}I have need of strong backs, traveller.");
    public override TextObject IssueAcceptByPlayer => Tx(_def?.Text.AcceptKey, "{=taom_lotr_issue_fallback_accept}How can I help?");
    public override TextObject IssueQuestSolutionExplanationByIssueGiver => Tx(_def?.Text.ExplanationKey, "{=taom_lotr_issue_pers_expl}Bring me {COUNT} captives and I will pay you well.");
    public override TextObject IssueQuestSolutionAcceptByPlayer => Tx(_def?.Text.SolutionAcceptKey, "{=taom_lotr_issue_pers_soln}I will bring you captives.");

    public override bool IsThereAlternativeSolution => false;
    public override bool IsThereLordSolution => false;

    // Both DeliverPersonnel configs share this one type; without this the engine's default same-type accept
    // gate (IssueBase.CheckPreconditions) would cap the player at ONE active DeliverPersonnel quest. Each
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

    public override bool IssueStayAliveConditions() => true;

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
        return new DeliverPersonnelLotrIssueQuest(questId, base.IssueOwner, CampaignTime.DaysFromNow(20f),
            _defId, base.IssueDifficultyMultiplier, NeededCount, RewardGold);
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
/// The quest for <see cref="DeliverPersonnelLotrIssue"/>: track bandit prisoners in the player's prison
/// roster, turn them in at the giver for the reward. The turn-in gate reads live prison-roster count, not
/// a cached log, so battle/ransom changes can't stale it.
/// </summary>
public class DeliverPersonnelLotrIssueQuest : QuestBase
{
    [SaveableField(1)] private string _defId;
    [SaveableField(2)] private int _neededCount;
    [SaveableField(3)] private int _rewardGold;
    [SaveableField(4)] private float _difficulty;
    [SaveableField(5)] private JournalLog _acceptedLog;
    [SaveableField(6)] private JournalLog _readyLog;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();

    public DeliverPersonnelLotrIssueQuest(string questId, Hero giverHero, CampaignTime duration,
        string defId, float difficulty, int neededCount, int rewardGold)
        : base(questId, giverHero, duration, rewardGold)
    {
        _defId = defId;
        _difficulty = difficulty;
        _neededCount = neededCount;
        _rewardGold = rewardGold;
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
            var t = new TextObject("{=taom_lotr_issue_pers_accepted}Deliver {COUNT} bandit captives to {QUEST_SETTLEMENT}.");
            t.SetTextVariable("COUNT", _neededCount);
            if (base.QuestGiver?.CurrentSettlement != null) t.SetTextVariable("QUEST_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
            return t;
        }
    }

    private TextObject ReadyLogText
    {
        get
        {
            var t = new TextObject("{=taom_lotr_issue_pers_ready}You have enough captives. Return to {QUEST_SETTLEMENT} to hand them over.");
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
        CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
    }

    protected override void HourlyTickParty(MobileParty mobileParty)
    {
        if (mobileParty == MobileParty.MainParty) Refresh();
    }

    private int CountBanditPrisoners()
    {
        var roster = PartyBase.MainParty.PrisonRoster;
        if (roster == null) return 0;
        int sum = 0;
        for (int i = 0; i < roster.Count; i++)
        {
            var el = roster.GetElementCopyAtIndex(i);
            if (el.Character != null && el.Character.Occupation == Occupation.Bandit) sum += el.Number;
        }
        return sum <= _neededCount ? sum : _neededCount;
    }

    private void Refresh()
    {
        if (_acceptedLog != null) _acceptedLog.UpdateCurrentProgress(CountBanditPrisoners());
        CheckReady();
    }

    private void CheckReady()
    {
        if (_readyLog == null && (_acceptedLog?.CurrentProgress ?? 0) >= _neededCount)
            _readyLog = AddLog(ReadyLogText);
        else if (_readyLog != null && (_acceptedLog?.CurrentProgress ?? 0) < _neededCount)
        {
            RemoveLog(_readyLog);
            _readyLog = null;
        }
    }

    private void OnPlayerBattleEnd(MapEvent mapEvent) => Refresh();

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        if (party == MobileParty.MainParty) Refresh();
    }

    private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
    {
        if (base.QuestGiver?.CurrentSettlement?.MapFaction != null
            && base.QuestGiver.CurrentSettlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
            CompleteQuestWithCancel(CancelLogText);
    }

    private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        => QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest(this, faction1, faction2, detail, CancelLogText, CancelLogText, forceCancel: true);

    private TextObject CancelLogText => new TextObject("{=taom_lotr_issue_common_cancel}The agreement has been cancelled.");

    protected override void OnTimedOut()
        => AddLog(new TextObject("{=taom_lotr_issue_pers_timeout}You failed to deliver the captives in time."));

    protected override void SetDialogs()
    {
        var npcAccept = new TextObject("{=taom_lotr_issue_common_offer_accept}Thank you. I will not forget this kindness.");
        var npcHave = new TextObject("{=taom_lotr_issue_pers_have}Have you brought the captives?");
        var npcThanks = new TextObject("{=taom_lotr_issue_pers_thanks}They'll do nicely. You have my thanks.");
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
            .PlayerOption(new TextObject("{=taom_lotr_issue_pers_turnin}Yes. Here they are."))
            .ClickableCondition(TurnInClickableConditions)
            .NpcLine(npcThanks)
            .Consequence(() => Campaign.Current.ConversationManager.ConversationEndOneShot += Success)
            .CloseDialog()
            .PlayerOption(new TextObject("{=taom_lotr_issue_common_working}Not yet -- I'm working on it."))
            .NpcLine(npcAwait)
            .CloseDialog()
            .EndPlayerOptions()
            .CloseDialog();
    }

    private bool TurnInClickableConditions(out TextObject explanation)
    {
        if (CountBanditPrisoners() >= _neededCount)
        {
            explanation = null;
            return true;
        }
        explanation = new TextObject("{=taom_lotr_issue_pers_not_enough}You don't have enough captives yet.");
        return false;
    }

    private void QuestAcceptedConsequences()
    {
        StartQuest();
        EnsureDef();
        var task = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.TaskKey)
            ? "{=taom_lotr_issue_pers_task}Take bandit captives" : _def.Text.TaskKey);
        task.SetTextVariable("COUNT", _neededCount);
        _acceptedLog = AddDiscreteLog(AcceptedLogText, task, CountBanditPrisoners(), _neededCount);
    }

    private void Success()
    {
        EnsureDef();
        RemoveBanditPrisoners(_neededCount);
        if (_def != null)
            Service.ApplyRewards(_def, _difficulty, new LotrIssueRewardAdapter(Hero.MainHero));
        RelationshipChangeWithQuestGiver = 5;
        CompleteQuestWithSuccess();
    }

    private void RemoveBanditPrisoners(int count)
    {
        var roster = PartyBase.MainParty.PrisonRoster;
        if (roster == null) return;
        for (int i = roster.Count - 1; i >= 0 && count > 0; i--)
        {
            var el = roster.GetElementCopyAtIndex(i);
            if (el.Character == null || el.Character.Occupation != Occupation.Bandit) continue;
            int take = Math.Min(count, el.Number);
            roster.AddToCounts(el.Character, -take);
            count -= take;
        }
    }
}
