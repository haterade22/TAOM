using System;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Duties;

public class InteractiveDutyPresenter : IInteractiveDutyPresenter
{
    /// <summary>rankBonus = (int)rank * this, applied only when the option's RankBonusApplies is set.</summary>
    private const int RankBonusPerLevel = Content.SkillCheckService.RankBonusPerLevel;

    /// <summary>Incident pay-delay relief: release min(arrears, max(floor, arrears/2)).</summary>
    private const int DeferredReleaseFloor = 8;

    private const string GenericTitle = "A task for the company";
    private const string GenericBody = "Something needs attention.";
    private const string GenericOption = "Proceed";

    private readonly IInquiryAdapter _inquiry;
    private readonly ISkillCheckService _skillCheck;
    private readonly IHeroSkillXpAdapter _skillXp;
    private readonly IServiceRewardService _rewards;
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly ICoopSessionProvider _coopSession;

    public InteractiveDutyPresenter(
        IInquiryAdapter inquiry,
        ISkillCheckService skillCheck,
        IHeroSkillXpAdapter skillXp,
        IServiceRewardService rewards,
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        ICoopSessionProvider coopSession)
    {
        _inquiry = inquiry;
        _skillCheck = skillCheck;
        _skillXp = skillXp;
        _rewards = rewards;
        _store = store;
        _contentStore = contentStore;
        _coopSession = coopSession;
    }

    public void PresentInteractiveDuty(InteractiveDutyDefinition duty, ServiceProgressSnapshot progress, int trust)
    {
        if (duty == null)
            return;

        ShowInquiry(duty.Id, duty.Title, duty.Body, duty.OptionA, duty.OptionB,
            () => ResolveOption(duty.Id, duty.OptionA, progress, trust),
            () => ResolveOption(duty.Id, duty.OptionB, progress, trust));
    }

    public void PresentIncident(IncidentDefinition incident, ServiceProgressSnapshot progress, int trust)
    {
        if (incident == null)
            return;

        ShowInquiry(incident.Id, incident.Title, incident.Body, incident.OptionA, incident.OptionB,
            () => ResolveIncidentOption(incident, incident.OptionA, progress, trust),
            () => ResolveIncidentOption(incident, incident.OptionB, progress, trust));
    }

    /// <summary>
    /// Both option callbacks run on a LATER frame than the popup that offered them, so neither the
    /// authority check nor the enlisted check made when the duty was PRESENTED still covers the
    /// moment rewards are granted and the record is mutated. Same stale-authorisation shape as the
    /// desertion confirmation. A discharge landing in that window would otherwise pay out a duty
    /// for a service that has ended.
    /// </summary>
    private bool CanResolveNow() => _coopSession?.IsAuthority != false && _store.Record.IsEnlisted;

    private void ResolveOption(string dutyId, DutyOptionSpec option, ServiceProgressSnapshot progress, int trust)
    {
        if (!CanResolveNow())
            return;

        var passed = ResolveCheckAndReward(dutyId, "duty", option, progress, trust);
        ShowResultToast(dutyId, passed);
    }

    private void ResolveIncidentOption(IncidentDefinition incident, DutyOptionSpec option, ServiceProgressSnapshot progress, int trust)
    {
        if (!CanResolveNow())
            return;

        var passed = ResolveCheckAndReward(incident.Id, "incident", option, progress, trust);

        if (passed && incident.Effect == "ReleaseDeferredPay")
        {
            var record = _contentStore.Record;
            var release = Math.Min(record.DeferredWages, Math.Max(DeferredReleaseFloor, record.DeferredWages / 2));
            if (release > 0)
            {
                record.DeferredWages -= release;
                _rewards.Grant(new RewardSpec { Gold = release }, "incident:" + incident.Id + ":deferred-release");
            }
        }

        ShowResultToast(incident.Id, passed);
    }

    /// <summary>Runs the skill check, grants the resulting reward, and updates the success/failure counters. Returns the check result.</summary>
    private bool ResolveCheckAndReward(string sourceId, string sourceKind, DutyOptionSpec option, ServiceProgressSnapshot progress, int trust)
    {
        if (option == null)
            return false;

        var heroId = _store.Record.EnlistedHeroId;
        var primary = _skillXp.GetSkillValue(heroId, option.SkillId);
        int? secondary = string.IsNullOrEmpty(option.SecondarySkillId)
            ? (int?)null
            : _skillXp.GetSkillValue(heroId, option.SecondarySkillId);
        var rankBonus = option.RankBonusApplies ? (int)progress.Rank * RankBonusPerLevel : 0;

        var passed = _skillCheck.Passes(primary, secondary, trust, rankBonus, option.Difficulty);
        var reward = passed ? option.SuccessReward : option.FailureReward;
        _rewards.Grant(reward, sourceKind + ":" + sourceId + ":" + option.Key);

        var record = _contentStore.Record;
        if (passed)
            record.DutySuccesses++;

        return passed;
    }

    private void ShowResultToast(string sourceId, bool passed)
    {
        var key = DutyText.KeyFor(sourceId, passed ? DutyText.SuccessSuffix : DutyText.FailureSuffix);
        _inquiry.ShowMessage(key, passed ? DutyText.SharedSuccess : DutyText.SharedFailure, null, null);
    }

    private void ShowInquiry(
        string id, string title, string body, DutyOptionSpec optionA, DutyOptionSpec optionB,
        Action onOptionA, Action onOptionB)
    {
        _inquiry.ShowTwoOptionInquiry(
            DutyText.KeyFor(id, DutyText.TitleSuffix), DutyText.English(title, id, DutyText.TitleSuffix, GenericTitle),
            DutyText.KeyFor(id, DutyText.BodySuffix), DutyText.English(body, id, DutyText.BodySuffix, GenericBody),
            DutyText.KeyFor(id, DutyText.OptionASuffix), DutyText.English(optionA?.Label, id, DutyText.OptionASuffix, GenericOption),
            DutyText.KeyFor(id, DutyText.OptionBSuffix), DutyText.English(optionB?.Label, id, DutyText.OptionBSuffix, GenericOption),
            onOptionA, onOptionB);
    }
}
