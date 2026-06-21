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
    }

    public override void SyncData(IDataStore dataStore) { }

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
