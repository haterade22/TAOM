using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.CreatureBandits;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues.Templates;

/// <summary>
/// HuntBand template: a named bandit party spawns near the village and the quest ends when the player destroys it
/// (battle, surrender or "serve under my command"). Each quest owns its party, so duplicates are allowed. The band's
/// clan is chosen by region (<see cref="LotrQuestBandRules.PickClan"/>). Direct IssueBase subclass, no base class
/// between (see <see cref="LotrIssueTemplateRules"/>). Entry-point layer (ADR-002).
/// </summary>
public class HuntBandLotrIssue : IssueBase
{
    [SaveableField(1)] private string _defId;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;
    private ICoopSessionProvider _coop;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();
    private ICoopSessionProvider Coop => _coop ??= IoC.Resolve<ICoopSessionProvider>();

    public HuntBandLotrIssue(Hero issueOwner, LotrIssueDefinition def)
        : base(issueOwner, CampaignTime.DaysFromNow(30f))
    {
        _def = def;
        _defId = def?.Id;
    }

    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
    }

    private int BandSize
    {
        get { EnsureDef(); return _def == null ? 1 : Service.ComputeTroopCount(_def, base.IssueDifficultyMultiplier); }
    }

    private TextObject Tx(string key, string fallback)
    {
        EnsureDef();
        var t = new TextObject(string.IsNullOrEmpty(key) ? fallback : key);
        if (base.IssueSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", base.IssueSettlement.Name);
        t.SetTextVariable("COUNT", 1);
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

    protected override bool IssueQuestCanBeDuplicated => true;

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
        return LotrIssueTemplateRules.StillUses(_def, LotrIssueTemplate.HuntBand)
            && (_def?.OffersTo(Hero.MainHero?.Culture?.StringId) ?? true)
            && LotrQuestBandRules.PickClan(ProjectClans(base.IssueOwner?.CurrentSettlement ?? base.IssueSettlement)) != null;
    }

    /// <summary>
    /// The bandit clans projected free of engine types: each clan's squared distance from the settlement to the nearest
    /// hideout of its culture, and whether it is a creature clan (spiders, trolls), which never plays swindlers.
    /// </summary>
    internal static LotrBandClanOption[] ProjectClans(Settlement anchor)
    {
        var options = new List<LotrBandClanOption>();
        foreach (var clan in Clan.BanditFactions)
        {
            bool creature = clan.StringId == CreatureBanditsConfig.BroodClanId || clan.StringId == CreatureBanditsConfig.TrollClanId;
            float distance = float.PositiveInfinity;
            if (anchor != null)
            {
                foreach (var hideout in Hideout.All)
                {
                    var settlement = hideout?.Settlement;
                    if (settlement == null || settlement.Culture != clan.Culture) continue;
                    float d = settlement.Position.DistanceSquared(anchor.Position);
                    if (d < distance) distance = d;
                }
            }
            options.Add(new LotrBandClanOption(clan.StringId, creature, distance));
        }
        return options.ToArray();
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
        return new HuntBandLotrIssueQuest(questId, base.IssueOwner, CampaignTime.DaysFromNow(25f),
            _defId, base.IssueDifficultyMultiplier, RewardGold, BandSize);
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
        if (Coop.ShouldDeferToHost) flag |= PreconditionFlags.NotInSameFaction;
        return flag == PreconditionFlags.None;
    }
}

/// <summary>
/// The quest for <see cref="HuntBandLotrIssue"/>. The party spawns on accept (a refused accept leaves nothing behind),
/// is tracked with the quest marker and is removed when the quest ends by timeout, failure or cancel. The destroyed
/// handler clears its saved reference before completing: the engine raises the event before it removes the party, so
/// the party still reads active there and the finalizer would destroy it a second time.
/// </summary>
public class HuntBandLotrIssueQuest : QuestBase
{
    private const string SpawnedOutcomeId = "lotr_issues.band_spawned";
    private const string DefeatedOutcomeId = "lotr_issues.band_defeated";

    [SaveableField(1)] private string _defId;
    [SaveableField(2)] private float _difficulty;
    [SaveableField(3)] private int _rewardGold;
    [SaveableField(4)] private MobileParty _band;
    [SaveableField(5)] private int _bandSize;
    [SaveableField(6)] private JournalLog _log;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;
    private ICoopSessionProvider _coop;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();
    private ICoopSessionProvider Coop => _coop ??= IoC.Resolve<ICoopSessionProvider>();

    public HuntBandLotrIssueQuest(string questId, Hero giverHero, CampaignTime duration,
        string defId, float difficulty, int rewardGold, int bandSize)
        : base(questId, giverHero, duration, rewardGold)
    {
        _defId = defId;
        _difficulty = difficulty;
        _rewardGold = rewardGold;
        _bandSize = bandSize;
        SetDialogs();
        InitializeQuestOnCreation();
    }

    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
    }

    private Settlement QuestSettlement => base.QuestGiver?.CurrentSettlement ?? base.QuestGiver?.HomeSettlement;

    public override TextObject Title
    {
        get
        {
            EnsureDef();
            var t = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.TitleKey)
                ? "{=taom_lotr_issue_fallback_title}A Request for Aid" : _def.Text.TitleKey);
            if (QuestSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", QuestSettlement.Name);
            t.SetTextVariable("COUNT", 1);
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
                ? "{=taom_lotr_issue_band_task}Hunt down the band ({COUNT})" : _def.Text.TaskKey);
            if (QuestSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", QuestSettlement.Name);
            t.SetTextVariable("COUNT", 1);
            return t;
        }
    }

    private TextObject CancelLogText => new TextObject("{=taom_lotr_issue_common_cancel}The agreement has been cancelled.");

    private TextObject BandLostLogText => new TextObject("{=taom_lotr_issue_band_lost}Someone else got to the band first. The agreement is void.");

    protected override void InitializeQuestOnGameLoad()
    {
        EnsureDef();
        SetDialogs();
        if (IsOngoing && (_band == null || !_band.IsActive)) CompleteQuestWithCancel(BandLostLogText);
    }

    protected override void HourlyTick() { }

    protected override void RegisterEvents()
    {
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
        CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
    }

    private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyer)
    {
        if (_band == null || party != _band) return;
        _band = null;
        var main = Campaign.Current?.MainParty;
        bool byMainParty = main != null && destroyer == main.Party;
        bool byArmy = main?.Army != null && destroyer?.MobileParty?.Army == main.Army;
        if (LotrQuestBandRules.IsDefeatedByPlayer(byMainParty, byArmy)) Success();
        else CompleteQuestWithCancel(BandLostLogText);
    }

    private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
    {
        if (base.QuestGiver?.CurrentSettlement?.MapFaction != null
            && base.QuestGiver.CurrentSettlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
            CompleteQuestWithCancel(CancelLogText);
    }

    private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        => QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest(this, faction1, faction2, detail, CancelLogText, CancelLogText, forceCancel: true);

    protected override void OnTimedOut()
    {
        EnsureDef();
        AddLog(new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.FailKey)
            ? "{=taom_lotr_issue_inn_fail}The swindlers slipped away with their winnings." : _def.Text.FailKey));
    }

    /// <summary>Removes the band when the quest ends any way but its destruction; a band in a fight is released instead.</summary>
    protected override void OnFinalize()
    {
        var band = _band;
        _band = null;
        if (band == null || !band.IsActive || Coop.ShouldDeferToHost) return;
        if (band.MapEvent != null)
        {
            band.SetPartyUsedByQuest(isActivelyUsed: false);
            band.IgnoreByOtherPartiesTill(CampaignTime.HoursFromNow(0f));
            band.Aggressiveness = 1f;
            band.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: false);
            band.Party.SetVisualAsDirty();
            return;
        }
        DestroyPartyAction.Apply(null, band);
    }

    protected override void SetDialogs()
    {
        var npcAccept = new TextObject("{=taom_lotr_issue_inn_offer_accept}They were seen on the road out of the village. Find them before they are gone.");
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
        string failure = Coop.ShouldDeferToHost ? "defer_to_host" : SpawnBand();
        if (failure != null)
        {
            CompleteQuestWithCancel(BandLostLogText);
            Outcomes.Fail(SpawnedOutcomeId, failure, $"quest {StringId}: no band could be spawned ({failure})");
            return;
        }
        var body = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.DescriptionKey)
            ? "{=taom_lotr_issue_combat_fallback_desc}A foe must be dealt with." : _def.Text.DescriptionKey);
        _log = AddDiscreteLog(body, TaskLogText, 0, 1);
        AddTrackedObject(_band);
        Outcomes.Pass(SpawnedOutcomeId, "", $"quest {StringId}: band of {_bandSize} spawned near '{QuestSettlement?.StringId}'");
    }

    /// <summary>Creates and flags the band, as vanilla's quest bandit parties are made. Null on success, else the reason.</summary>
    private string SpawnBand()
    {
        var anchor = QuestSettlement;
        if (anchor == null) return "no_settlement";
        string clanId = LotrQuestBandRules.PickClan(HuntBandLotrIssue.ProjectClans(anchor));
        var clan = clanId == null ? null : Clan.BanditFactions.FirstOrDefault(c => c.StringId == clanId);
        if (clan == null) return "no_clan";

        float radius = 0.5f * Campaign.Current.EstimatedAverageBanditPartySpeed * CampaignTime.HoursInDay;
        var point = NavigationHelper.FindPointAroundPosition(anchor.GatePosition, MobileParty.NavigationType.Default, radius);
        point = CreatureBandParties.OutOfPlayerSight(point, radius);
        if (!point.IsValid()) return "no_point";

        var band = BanditPartyComponent.CreateLooterParty("taom_lotr_band_" + StringId, clan, anchor, false, null, point);
        if (band == null) return "create_failed";
        FillBand(band, clan);
        EnsureDef();
        band.Party.SetCustomName(new TextObject(_def == null || string.IsNullOrEmpty(_def.BandNameKey)
            ? "{=taom_lotr_issue_band_fallback_name}Outlaws" : _def.BandNameKey));
        band.SetPartyUsedByQuest(isActivelyUsed: true);
        band.IgnoreByOtherPartiesTill(CampaignTime.Never);
        band.Aggressiveness = 0f;
        band.Party.SetVisualAsDirty();
        band.InitializePartyTrade(QuestHelper.CalculateInitialGoldForBanditQuestParty(band));
        band.SetMovePatrolAroundSettlement(anchor, MobileParty.NavigationType.Default, isTargetingPort: false);
        band.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: true);
        _band = band;
        return null;
    }

    /// <summary>
    /// The clan's template through the vanilla filler, or the clan's basic troop when the clan has no template or an
    /// empty one: the filler's top-up loop reads the template's first stack.
    /// </summary>
    private void FillBand(MobileParty band, Clan clan)
    {
        var template = clan.DefaultPartyTemplate;
        if (template != null && template.Stacks.Count > 0)
            MobilePartyHelper.FillPartyManuallyAfterCreation(band, template, _bandSize);
        if (band.MemberRoster.TotalManCount == 0)
            band.MemberRoster.AddToCounts(clan.BasicTroop, _bandSize);
    }

    private void Success()
    {
        EnsureDef();
        if (_def != null)
            Service.ApplyRewards(_def, _difficulty, new LotrIssueRewardAdapter(Hero.MainHero));
        RelationshipChangeWithQuestGiver = 5;
        _log?.UpdateCurrentProgress(1);
        if (_def != null && !string.IsNullOrEmpty(_def.Text.SuccessKey))
            AddLog(new TextObject(_def.Text.SuccessKey));
        CompleteQuestWithSuccess();
        Outcomes.Pass(DefeatedOutcomeId, "", $"quest {StringId}: the band was destroyed by the player");
    }
}
