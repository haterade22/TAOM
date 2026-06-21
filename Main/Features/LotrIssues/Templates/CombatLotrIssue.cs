using System;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues.Templates;

/// <summary>
/// Combat template: an event-driven count objective that auto-completes on reaching the target. The
/// definition's <c>Variant</c> selects the objective -- "CaptureLords" counts at-war enemy lords the
/// player's party takes prisoner; anything else ("DefeatRaids", default) counts battles the player wins.
/// Covers the vanilla clear-hideout + defeat/capture-target archetypes. No turn-in dialog -- it resolves
/// itself when the count is met. Entry-point layer (ADR-002); decisions delegate to <see cref="ILotrIssueService"/>.
/// </summary>
public class CombatLotrIssue : IssueBase
{
    [SaveableField(1)] private string _defId;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();

    public CombatLotrIssue(Hero issueOwner, LotrIssueDefinition def)
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
    public override TextObject Description => Tx(_def?.Text.DescriptionKey, "{=taom_lotr_issue_fallback_desc}A foe must be dealt with.");
    public override TextObject IssueBriefByIssueGiver => Tx(_def?.Text.BriefKey, "{=taom_lotr_issue_fallback_brief}We are beset by enemies, traveller.");
    public override TextObject IssueAcceptByPlayer => Tx(_def?.Text.AcceptKey, "{=taom_lotr_issue_fallback_accept}What needs doing?");
    public override TextObject IssueQuestSolutionExplanationByIssueGiver => Tx(_def?.Text.ExplanationKey, "{=taom_lotr_issue_combat_expl}Deal with our enemies and you will be rewarded.");
    public override TextObject IssueQuestSolutionAcceptByPlayer => Tx(_def?.Text.SolutionAcceptKey, "{=taom_lotr_issue_combat_soln}I will deal with them.");

    public override bool IsThereAlternativeSolution => false;
    public override bool IsThereLordSolution => false;

    // All 27 Combat configs share this one type, so the engine's default same-type accept gate
    // (IssueBase.CheckPreconditions, IssueQuestCanBeDuplicated=>false) would cap the player at ONE active
    // Combat quest across every config. Each config is a distinct issue, so allow concurrent duplicates.
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
        return new CombatLotrIssueQuest(questId, base.IssueOwner, CampaignTime.DaysFromNow(25f),
            _defId, base.IssueDifficultyMultiplier, NeededCount, RewardGold, _def?.Variant ?? "");
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
/// The quest for <see cref="CombatLotrIssue"/>: count battles won (or at-war lords captured) until the
/// target is met, then auto-grant the reward and complete. No turn-in dialog. Count logic mirrors the
/// validated CareerQuest WinBattles / HeroPrisonerTaken handlers.
/// </summary>
public class CombatLotrIssueQuest : QuestBase
{
    [SaveableField(1)] private string _defId;
    [SaveableField(2)] private int _neededCount;
    [SaveableField(3)] private int _rewardGold;
    [SaveableField(4)] private float _difficulty;
    [SaveableField(5)] private string _variant;
    [SaveableField(6)] private int _progress;
    [SaveableField(7)] private JournalLog _log;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();

    private bool IsCaptureLords => string.Equals(_variant, "CaptureLords", StringComparison.OrdinalIgnoreCase);
    private bool IsWinTournaments => string.Equals(_variant, "WinTournaments", StringComparison.OrdinalIgnoreCase);

    public CombatLotrIssueQuest(string questId, Hero giverHero, CampaignTime duration,
        string defId, float difficulty, int neededCount, int rewardGold, string variant)
        : base(questId, giverHero, duration, rewardGold)
    {
        _defId = defId;
        _difficulty = difficulty;
        _neededCount = neededCount;
        _rewardGold = rewardGold;
        _variant = variant ?? "";
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

    private TextObject TaskLogText
    {
        get
        {
            var t = new TextObject(IsCaptureLords
                ? "{=taom_lotr_issue_combat_task_capture}Capture enemy lords ({COUNT})"
                : IsWinTournaments
                    ? "{=taom_lotr_issue_combat_task_tourney}Win tournaments ({COUNT})"
                    : "{=taom_lotr_issue_combat_task_defeat}Defeat the raiders ({COUNT})");
            t.SetTextVariable("COUNT", _neededCount);
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
        if (IsCaptureLords)
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        else if (IsWinTournaments)
            CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
        else
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
        CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
    }

    private void OnPlayerBattleEnd(MapEvent mapEvent)
    {
        if (mapEvent == null || mapEvent.WinningSide == BattleSideEnum.None || mapEvent.WinningSide != mapEvent.PlayerSide) return;
        Bump();
    }

    private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
    {
        if (capturer != PartyBase.MainParty || prisoner == null || !prisoner.IsLord) return;
        var playerFaction = Hero.MainHero.MapFaction;
        if (playerFaction == null || prisoner.MapFaction == null || !prisoner.MapFaction.IsAtWarWith(playerFaction)) return;
        Bump();
    }

    private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
    {
        if (winner == CharacterObject.PlayerCharacter) Bump();
    }

    private void Bump()
    {
        if (!IsOngoing) return;
        _progress++;
        if (_progress > _neededCount) _progress = _neededCount;
        _log?.UpdateCurrentProgress(_progress);
        if (_progress >= _neededCount) Success();
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
        => AddLog(new TextObject("{=taom_lotr_issue_combat_timeout}You failed to deal with the threat in time."));

    protected override void SetDialogs()
    {
        var npcAccept = new TextObject("{=taom_lotr_issue_common_offer_accept}Thank you. I will not forget this kindness.");
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
        var body = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.DescriptionKey)
            ? "{=taom_lotr_issue_fallback_desc}A foe must be dealt with." : _def.Text.DescriptionKey);
        _log = AddDiscreteLog(body, TaskLogText, _progress, _neededCount);
    }

    private void Success()
    {
        EnsureDef();
        if (_def != null)
            Service.ApplyRewards(_def, _difficulty, new LotrIssueRewardAdapter(Hero.MainHero));
        RelationshipChangeWithQuestGiver = 5;
        CompleteQuestWithSuccess();
    }
}
