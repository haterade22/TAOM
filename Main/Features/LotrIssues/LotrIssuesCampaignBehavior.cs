using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LotrIssues.Domain;
using TAOM.Features.LotrIssues.Templates;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// The single host behavior for all LOTR custom issues. One <c>OnCheckForIssueEvent</c> listener asks
/// the pure <see cref="ILotrIssueService"/> which configured issues a polled hero is eligible for, then
/// registers each via <c>IssueManager.AddPotentialIssueData</c>. The definition is carried into the
/// constructed issue through <c>PotentialIssueData.RelatedObject</c> (no closure), so one template type
/// serves many configured issues. Thin entry point (ADR-002) — all decisions live in the service.
/// </summary>
public class LotrIssuesCampaignBehavior : CampaignBehaviorBase
{
    private readonly ILotrIssueService _service;
    private readonly IModLogger _logger;

    public LotrIssuesCampaignBehavior(ILotrIssueService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnCheckForIssueEvent.AddNonSerializedListener(this, OnCheckForIssue);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
    }

    public override void SyncData(IDataStore dataStore) { }

    /// <summary>
    /// Safety-net sweep for saves made while vanilla issue behaviors were still live (the pre-fix
    /// Type.GetType resolution left the 7 SandBox issues unsuppressed — 2026-07-21 crash report):
    /// cancel every lingering vanilla issue the player has NOT committed to, because accepting one can
    /// CTD on TAOM data (the daughter quest's ctor NREs on the XSLT-deleted vanilla bandit clans).
    /// In-progress commitments — an accepted quest, a dispatched alternative solution, a lord
    /// solution — are left alone: their start paths already ran safely and cancelling them would
    /// destroy player progress (an alternative-solution cancel recalls the committed companion+troops).
    /// </summary>
    private void OnGameLoaded(CampaignGameStarter starter)
    {
        try
        {
            var issues = Campaign.Current?.IssueManager?.Issues;
            if (issues == null) return;

            var toCancel = new System.Collections.Generic.List<IssueBase>();
            foreach (var pair in issues)
            {
                var issue = pair.Value;
                if (issue == null) continue;
                // Any in-progress player commitment is kept: an accepted quest (ctor already ran
                // safely), a dispatched companion+troops (alternative solution), or a lord solution.
                // Cancelling those would destroy progress; only untouched offers are crash bombs.
                if (issue.IssueQuest != null || issue.IsSolvingWithAlternative || issue.IsSolvingWithLordSolution) continue;
                if (!LotrIssueSuppression.IsVanillaIssueType(issue.GetType())) continue;
                toCancel.Add(issue); // collect first — cancelling mutates the Issues dictionary
            }

            foreach (var issue in toCancel)
            {
                var name = issue.GetType().Name;
                try
                {
                    issue.CompleteIssueWithCancel();
                    _logger.LogInfo($"LotrIssues: swept lingering vanilla issue '{name}' (owner '{issue.IssueOwner?.Name}')");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"LotrIssues: failed to sweep vanilla issue '{name}': {ex.Message}");
                }
            }

            if (toCancel.Count > 0)
                _logger.LogError($"LotrIssues: swept {toCancel.Count} vanilla issue(s) lingering in this save — suppression gap in the build that created it");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"LotrIssues: vanilla-issue sweep failed: {ex.Message}");
        }
    }

    private void OnCheckForIssue(Hero hero)
    {
        if (hero == null) return;
        var eligible = _service.GetEligibleIssues(new LotrIssueGiverAdapter(hero));
        if (eligible == null || eligible.Count == 0) return;

        foreach (var def in eligible)
        {
            var type = TemplateType(def.Template);
            if (type == null) continue; // template not yet implemented (later waves)
            Campaign.Current.IssueManager.AddPotentialIssueData(hero,
                new PotentialIssueData(OnSelected, type, MapFrequency(def.Frequency), def));
        }
    }

    private static IssueBase OnSelected(in PotentialIssueData pid, Hero issueOwner)
    {
        var def = pid.RelatedObject as LotrIssueDefinition;
        return def == null ? null : CreateIssue(def, issueOwner);
    }

    private static IssueBase CreateIssue(LotrIssueDefinition def, Hero owner)
    {
        switch (def.Template)
        {
            case LotrIssueTemplate.DeliverGoods: return new DeliverGoodsLotrIssue(owner, def);
            case LotrIssueTemplate.DeliverPersonnel: return new DeliverPersonnelLotrIssue(owner, def);
            case LotrIssueTemplate.Combat: return new CombatLotrIssue(owner, def);
            default: return null; // implemented in later waves
        }
    }

    private static Type TemplateType(LotrIssueTemplate t)
    {
        switch (t)
        {
            case LotrIssueTemplate.DeliverGoods: return typeof(DeliverGoodsLotrIssue);
            case LotrIssueTemplate.DeliverPersonnel: return typeof(DeliverPersonnelLotrIssue);
            case LotrIssueTemplate.Combat: return typeof(CombatLotrIssue);
            default: return null;
        }
    }

    private static IssueBase.IssueFrequency MapFrequency(IssueFrequencyTier tier)
    {
        switch (tier)
        {
            case IssueFrequencyTier.VeryCommon: return IssueBase.IssueFrequency.VeryCommon;
            case IssueFrequencyTier.Rare: return IssueBase.IssueFrequency.Rare;
            default: return IssueBase.IssueFrequency.Common;
        }
    }
}
