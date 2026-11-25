using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.CoopInterop;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues.Templates;

/// <summary>
/// TrainRetainers template: the giver lends N troops of his own culture's basic line, the player wins K battles
/// with them in the party, and the survivors go home. The vanilla retainer quest lends a unique Dunland-faced
/// troop, which is wrong for an orc, dwarf or elf notable, so TAOM lends the culture's basic troop instead.
/// Not a duplicate-capable type: two loans of one culture line would count each other's troops. Direct IssueBase
/// subclass, no base class between (the save system keys saved fields by class level, see
/// <see cref="LotrIssueTemplateRules"/>). Entry-point layer (ADR-002); decisions delegate to
/// <see cref="ILotrIssueService"/> and <see cref="LotrRetainerLoanRules"/>.
/// </summary>
public class TrainRetainersLotrIssue : IssueBase
{
    [SaveableField(1)] private string _defId;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;
    private IDesertionRecruitGuard _guard;
    private ICoopSessionProvider _coop;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();
    private IDesertionRecruitGuard Guard => _guard ??= IoC.Resolve<IDesertionRecruitGuard>();
    private ICoopSessionProvider Coop => _coop ??= IoC.Resolve<ICoopSessionProvider>();

    public TrainRetainersLotrIssue(Hero issueOwner, LotrIssueDefinition def)
        : base(issueOwner, CampaignTime.DaysFromNow(30f))
    {
        _def = def;
        _defId = def?.Id;
    }

    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
    }

    private int NeededWins
    {
        get { EnsureDef(); return _def == null ? 1 : Service.ComputeTargetCount(_def, base.IssueDifficultyMultiplier); }
    }

    private int LoanSize
    {
        get { EnsureDef(); return _def == null ? 0 : Service.ComputeTroopCount(_def, base.IssueDifficultyMultiplier); }
    }

    private CharacterObject LentTroop
    {
        get
        {
            var troop = base.IssueOwner?.Culture?.BasicTroop ?? base.IssueSettlement?.Culture?.BasicTroop;
            return troop == null || troop.IsHero ? null : troop;
        }
    }

    private TextObject Tx(string key, string fallback)
    {
        EnsureDef();
        var t = new TextObject(string.IsNullOrEmpty(key) ? fallback : key);
        if (base.IssueSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", base.IssueSettlement.Name);
        t.SetTextVariable("COUNT", NeededWins);
        return t;
    }

    public override TextObject Title => Tx(_def?.Text.TitleKey, "{=taom_lotr_issue_fallback_title}A Request for Aid");
    public override TextObject Description => Tx(_def?.Text.DescriptionKey, "{=taom_lotr_issue_combat_fallback_desc}A foe must be dealt with.");
    public override TextObject IssueBriefByIssueGiver => Tx(_def?.Text.BriefKey, "{=taom_lotr_issue_combat_fallback_brief}We are beset by enemies, traveller.");
    public override TextObject IssueAcceptByPlayer => Tx(_def?.Text.AcceptKey, "{=taom_lotr_issue_combat_fallback_accept}What needs doing?");
    public override TextObject IssueQuestSolutionExplanationByIssueGiver => Tx(_def?.Text.ExplanationKey, "{=taom_lotr_issue_combat_expl}Deal with our enemies and you will be rewarded.");
    public override TextObject IssueQuestSolutionAcceptByPlayer => Tx(_def?.Text.SolutionAcceptKey, "{=taom_lotr_issue_combat_soln}I will deal with them.");

    public override bool IsThereAlternativeSolution => false;
    public override bool IsThereLordSolution => false;

    protected override bool IssueQuestCanBeDuplicated => false;

    protected override int RewardGold
    {
        get { EnsureDef(); return _def == null ? 0 : Service.ComputeRewardGold(_def, base.IssueDifficultyMultiplier); }
    }

    public override IssueFrequency GetFrequency()
    {
        EnsureDef();
        return LotrIssueTemplateRules.FrequencyOf(_def);
    }

    public override bool IssueStayAliveConditions()
    {
        EnsureDef();
        var troop = LentTroop;
        return LotrIssueTemplateRules.StillUses(_def, LotrIssueTemplate.TrainRetainers)
            && (_def?.OffersTo(Hero.MainHero?.Culture?.StringId) ?? true)
            && troop != null;
    }

    protected override float GetIssueEffectAmountInternal(IssueEffect issueEffect)
    {
        if (issueEffect == DefaultIssueEffects.SettlementSecurity) return -0.3f;
        if (issueEffect == DefaultIssueEffects.SettlementProsperity) return -0.2f;
        return 0f;
    }

    protected override void OnGameLoad() => EnsureDef();

    protected override void HourlyTick() { }

    protected override void CompleteIssueWithTimedOutConsequences() { }

    protected override QuestBase GenerateIssueQuest(string questId)
    {
        EnsureDef();
        return new TrainRetainersLotrIssueQuest(questId, base.IssueOwner, CampaignTime.DaysFromNow(40f),
            _defId, base.IssueDifficultyMultiplier, NeededWins, RewardGold, LoanSize, LentTroop?.StringId);
    }

    protected override bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flag, out Hero relationHero, out SkillObject skill, out int requiredGold)
    {
        skill = null;
        relationHero = null;
        requiredGold = 0;
        flag = PreconditionFlags.None;
        EnsureDef();
        if (LotrIssueTemplateRules.IsBelowRelationMin(issueGiver.GetRelationWithPlayer(), _def))
        {
            flag |= PreconditionFlags.Relation;
            relationHero = issueGiver;
        }
        if (issueGiver.CurrentSettlement != null
            && FactionManager.IsAtWarAgainstFaction(issueGiver.CurrentSettlement.MapFaction, Hero.MainHero.MapFaction))
        {
            flag |= PreconditionFlags.AtWar;
        }
        var party = Campaign.Current?.MainParty;
        if (party != null && party.MemberRoster.TotalManCount + LoanSize > party.Party.PartySizeLimit)
            flag |= PreconditionFlags.PartySizeLimit;
        if (Coop.ShouldDeferToHost || Guard.WouldDesertFromParty(party?.Party, LentTroop))
            flag |= PreconditionFlags.NotInSameFaction;
        return flag == PreconditionFlags.None;
    }
}

/// <summary>
/// The quest for <see cref="TrainRetainersLotrIssue"/>. The loan joins the main party on accept. Each won battle with a
/// survivor still in the party counts; at the target the highest-tier troops of the line go home and the reward is
/// graded by how many returned, as in vanilla. Known limit: the roster cannot tell lent troops from the player's own of
/// the same line, so the survivor count only ever falls and the return may take the player's own troops.
/// </summary>
public class TrainRetainersLotrIssueQuest : QuestBase
{
    private const string LentOutcomeId = "lotr_issues.retainers_lent";
    private const string ReturnedOutcomeId = "lotr_issues.retainers_returned";

    [SaveableField(1)] private string _defId;
    [SaveableField(2)] private float _difficulty;
    [SaveableField(3)] private int _rewardGold;
    [SaveableField(4)] private int _neededWins;
    [SaveableField(5)] private int _wins;
    [SaveableField(6)] private string _troopId;
    [SaveableField(7)] private int _lent;
    [SaveableField(8)] private int _survivors;
    [SaveableField(9)] private JournalLog _log;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;
    private ICoopSessionProvider _coop;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();
    private ICoopSessionProvider Coop => _coop ??= IoC.Resolve<ICoopSessionProvider>();

    public TrainRetainersLotrIssueQuest(string questId, Hero giverHero, CampaignTime duration,
        string defId, float difficulty, int neededWins, int rewardGold, int lent, string troopId)
        : base(questId, giverHero, duration, rewardGold)
    {
        _defId = defId;
        _difficulty = difficulty;
        _neededWins = neededWins;
        _rewardGold = rewardGold;
        _lent = lent;
        _troopId = troopId;
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
            t.SetTextVariable("COUNT", _neededWins);
            return t;
        }
    }

    public override bool IsRemainingTimeHidden => false;

    private TextObject TaskLogText
    {
        get
        {
            EnsureDef();
            var t = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.TaskKey)
                ? "{=taom_lotr_issue_retainers_task}Train the lent retainers ({COUNT})" : _def.Text.TaskKey);
            t.SetTextVariable("COUNT", _neededWins);
            return t;
        }
    }

    private TextObject CancelLogText => new TextObject("{=taom_lotr_issue_common_cancel}The agreement has been cancelled.");

    private TextObject FailLogText
    {
        get
        {
            EnsureDef();
            return new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.FailKey)
                ? "{=taom_lotr_issue_training_fail}None of the retainers lived to come home." : _def.Text.FailKey);
        }
    }

    protected override void InitializeQuestOnGameLoad()
    {
        EnsureDef();
        SetDialogs();
    }

    protected override void HourlyTick() => Refresh();

    protected override void RegisterEvents()
    {
        CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
        CampaignEvents.OnTroopsDesertedEvent.AddNonSerializedListener(this, OnTroopsDeserted);
        CampaignEvents.PlayerUpgradedTroopsEvent.AddNonSerializedListener(this, OnPlayerUpgradedTroops);
        CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
    }

    private CharacterObject LentTroop => string.IsNullOrEmpty(_troopId) ? null : MBObjectManager.Instance.GetObject<CharacterObject>(_troopId);

    /// <summary>The lent troop and every troop it can upgrade into; a tree can merge, so each is visited once.</summary>
    private HashSet<CharacterObject> Line()
    {
        var line = new HashSet<CharacterObject>();
        var troop = LentTroop;
        if (troop == null) return line;
        var queue = new Queue<CharacterObject>();
        queue.Enqueue(troop);
        line.Add(troop);
        while (queue.Count > 0)
        {
            var next = queue.Dequeue();
            if (next.UpgradeTargets == null) continue;
            foreach (var target in next.UpgradeTargets)
                if (target != null && line.Add(target)) queue.Enqueue(target);
        }
        return line;
    }

    /// <summary>The line's stacks in the main party, with the matching characters in the same order.</summary>
    private LotrTroopStack[] ProjectLine(out CharacterObject[] characters)
    {
        var roster = Campaign.Current?.MainParty?.MemberRoster;
        var line = Line();
        var stacks = new List<LotrTroopStack>();
        var found = new List<CharacterObject>();
        if (roster != null)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                var el = roster.GetElementCopyAtIndex(i);
                if (el.Character == null || !line.Contains(el.Character)) continue;
                stacks.Add(new LotrTroopStack(el.Character.StringId, el.Character.Tier, el.Number, el.Character.IsHero));
                found.Add(el.Character);
            }
        }
        characters = found.ToArray();
        return stacks.ToArray();
    }

    private void Refresh()
    {
        if (!IsOngoing || _lent <= 0) return;
        int inParty = 0;
        foreach (var stack in ProjectLine(out _)) inParty += stack.Number;
        _survivors = LotrRetainerLoanRules.TrackSurvivors(_survivors, inParty);
        if (_survivors == 0) Fail();
    }

    private void OnTroopsDeserted(MobileParty party, TroopRoster desertedTroops)
    {
        if (party == Campaign.Current?.MainParty) Refresh();
    }

    private void OnPlayerUpgradedTroops(CharacterObject from, CharacterObject to, int number) => Refresh();

    private void OnPlayerBattleEnd(MapEvent mapEvent)
    {
        Refresh();
        if (!IsOngoing || mapEvent == null) return;
        bool won = mapEvent.WinningSide != BattleSideEnum.None && mapEvent.WinningSide == mapEvent.PlayerSide;
        if (!LotrRetainerLoanRules.CountsAsTrainingWin(won, _survivors)) return;
        _wins++;
        if (_wins > _neededWins) _wins = _neededWins;
        _log?.UpdateCurrentProgress(_wins);
        if (_wins >= _neededWins) ReturnAndReward();
    }

    private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
    {
        if (base.QuestGiver?.CurrentSettlement?.MapFaction != null
            && base.QuestGiver.CurrentSettlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
            CompleteQuestWithCancel(CancelLogText);
    }

    private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        => QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest(this, faction1, faction2, detail, CancelLogText, CancelLogText, forceCancel: true);

    /// <summary>Sends up to <paramref name="count"/> troops of the line home; returns how many left the roster.</summary>
    private int SendHome(int count)
    {
        if (count <= 0 || Coop.ShouldDeferToHost) return 0;
        var roster = Campaign.Current?.MainParty?.MemberRoster;
        if (roster == null) return 0;
        var plan = LotrRetainerLoanRules.PlanReturn(ProjectLine(out var characters), count);
        int sent = 0;
        for (int i = plan.Length - 1; i >= 0; i--)
        {
            if (plan[i] <= 0) continue;
            roster.AddToCounts(characters[i], -plan[i]);
            sent += plan[i];
        }
        return sent;
    }

    protected override void OnTimedOut()
    {
        SendHome(_survivors);
        RelationshipChangeWithQuestGiver = -5;
        AddLog(new TextObject("{=taom_lotr_issue_training_timeout}You kept the retainers too long, and their master called them home."));
    }

    public override void OnCanceled() => SendHome(_survivors);

    protected override void SetDialogs()
    {
        var npcAccept = new TextObject("{=taom_lotr_issue_training_offer_accept}Good. I will send the lads to your company. Bring back as many as you can.");
        OfferDialogFlow = DialogFlow.CreateDialogFlow("issue_classic_quest_start")
            .NpcLine(npcAccept)
            .Condition(() => CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject)
            .Consequence(QuestAcceptedConsequences)
            .CloseDialog();
    }

    private void QuestAcceptedConsequences()
    {
        StartQuest();
        EnsureDef();
        var troop = LentTroop;
        var roster = Campaign.Current?.MainParty?.MemberRoster;
        if (troop == null || roster == null || _lent <= 0 || Coop.ShouldDeferToHost
            || roster.AddToCounts(troop, _lent) < 0)
        {
            CompleteQuestWithCancel(CancelLogText);
            Outcomes.Fail(LentOutcomeId, "no_loan", $"quest {StringId}: the loan of {_lent} '{_troopId}' could not join the party");
            return;
        }
        _survivors = _lent;
        var body = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.DescriptionKey)
            ? "{=taom_lotr_issue_combat_fallback_desc}A foe must be dealt with." : _def.Text.DescriptionKey);
        _log = AddDiscreteLog(body, TaskLogText, _wins, _neededWins);
        Outcomes.Pass(LentOutcomeId, "", $"quest {StringId}: {_lent} x '{_troopId}' joined the party");
    }

    private void ReturnAndReward()
    {
        EnsureDef();
        int returned = SendHome(_survivors);
        var grade = LotrRetainerLoanRules.Grade(returned, _lent);
        if (_def != null)
            Service.ApplyRewards(_def, _difficulty, new LotrIssueRewardAdapter(Hero.MainHero));
        RelationshipChangeWithQuestGiver = LotrRetainerLoanRules.RelationFor(grade);
        if (_def != null && !string.IsNullOrEmpty(_def.Text.SuccessKey))
            AddLog(new TextObject(_def.Text.SuccessKey));
        var back = new TextObject("{=taom_lotr_issue_training_returned}{RETURNED} of the {LENT} retainers went home with you.");
        back.SetTextVariable("RETURNED", returned);
        back.SetTextVariable("LENT", _lent);
        AddLog(back);
        CompleteQuestWithSuccess();
        Outcomes.Pass(ReturnedOutcomeId, "", $"quest {StringId}: {returned} of {_lent} went home, grade {grade}");
    }

    private void Fail()
    {
        RelationshipChangeWithQuestGiver = LotrRetainerLoanRules.RelationFor(LoanReturnGrade.Failed);
        CompleteQuestWithFail(FailLogText);
    }
}
