using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Duties;

public class InteractiveDutyPresenter : IInteractiveDutyPresenter
{
    /// <summary>rankBonus = (int)rank * this, applied only when the option's RankBonusApplies is set.</summary>
    private const int RankBonusPerLevel = 4;

    /// <summary>Incident pay-delay relief: release min(arrears, max(floor, arrears/2)).</summary>
    private const int DeferredReleaseFloor = 8;

    private static readonly Dictionary<string, (string Title, string Body)> DutyCopy = new(StringComparer.Ordinal)
    {
        ["night_patrol"] = ("Night Watch", "The sergeant needs someone to walk the night rounds before the watch changes."),
        ["treat_wounded"] = ("The Wounded Need Tending", "Several wounded need attention before the surgeon reaches them all."),
        ["train_recruits"] = ("Drill the Recruits", "A knot of green recruits stands idle, waiting for someone to put them through their paces."),
        ["equipment_check"] = ("Kit Inspection", "The quartermaster wants every soldier's kit checked before the muster."),
        ["inspect_defenses"] = ("Walk the Works", "Someone needs to walk the defenses and report back on their condition."),
        ["gate_watch"] = ("Gate Duty", "The gate needs a steady pair of eyes on who comes and goes."),
        ["quartermaster_shortage"] = ("Short on Stores", "The quartermaster is short on supplies and needs a hand sorting it out."),
        ["lead_patrol"] = ("Take the Patrol", "The next patrol needs someone to lead it out and bring it back whole."),
        ["coordinate_supply"] = ("Supply Lines", "The wagons are backed up and someone needs to get them moving again."),
        ["command_squad"] = ("Command a Squad", "A squad needs a steady hand to drill and ready them for the line."),
        ["strategic_planning"] = ("Plan the Route", "The officers are arguing over the next march and want another voice at the table."),
        ["pay_delay"] = ("Pay Delayed Again", "Word spreads that pay is late again, and the men are grumbling."),
        ["short_rations"] = ("Short Rations", "The stores are thin tonight and there isn't enough to go around."),
        ["camp_discipline"] = ("Trouble in Camp", "A shouting match is drawing a crowd before it turns into something worse."),
    };

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

        var (title, body) = CopyFor(duty.Id);
        _inquiry.ShowTwoOptionInquiry(
            KeyFor(duty.Id, "title"), title,
            KeyFor(duty.Id, "body"), body,
            KeyFor(duty.Id, "opta"), Humanize(duty.OptionA?.Key),
            KeyFor(duty.Id, "optb"), Humanize(duty.OptionB?.Key),
            () => ResolveOption(duty.Id, duty.OptionA, progress, trust),
            () => ResolveOption(duty.Id, duty.OptionB, progress, trust));
    }

    public void PresentIncident(IncidentDefinition incident, ServiceProgressSnapshot progress, int trust)
    {
        if (incident == null)
            return;

        var (title, body) = CopyFor(incident.Id);
        _inquiry.ShowTwoOptionInquiry(
            KeyFor(incident.Id, "title"), title,
            KeyFor(incident.Id, "body"), body,
            KeyFor(incident.Id, "opta"), Humanize(incident.OptionA?.Key),
            KeyFor(incident.Id, "optb"), Humanize(incident.OptionB?.Key),
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
        else
            record.DutyFailures++;

        return passed;
    }

    private void ShowResultToast(string sourceId, bool passed)
    {
        var key = "taom_enlist_duty_" + sourceId + "_" + (passed ? "success" : "failure");
        var fallback = passed
            ? "It went well."
            : "It didn't go as planned.";
        _inquiry.ShowMessage(key, fallback, null, null);
    }

    private static (string Title, string Body) CopyFor(string id)
    {
        return DutyCopy.TryGetValue(id, out var copy) ? copy : ("Camp Business", "Something needs attention.");
    }

    private static string KeyFor(string id, string suffix) => "taom_enlist_duty_" + id + "_" + suffix;

    private static string Humanize(string snakeCase)
    {
        if (string.IsNullOrEmpty(snakeCase))
            return "Proceed";

        var parts = snakeCase.Split('_');
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
        }
        return string.Join(" ", parts);
    }
}
