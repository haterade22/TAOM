using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Duties;

/// <summary>
/// Field duties as CAMP WORK: accept one, it occupies you for a few hours, then a single skill
/// check decides how it went. The player stays parked with the column throughout.
///
/// WHY THIS SHAPE (2026-08-08 — do not re-derive by re-adding travel):
/// The previous model detached the player for DAYS. <c>Start</c> called
/// <c>IServiceAttachmentService.RestorePresence()</c>, which set <c>IsActive</c> and
/// <c>IsVisible</c> true — turning an enlisted player, whose roster is one hero with no troops,
/// into a fully targetable party in contested territory. A live session recorded the consequence
/// precisely: duty started 22:02:38, player captured by 22:03:19. Worse, the duty then survived
/// the captivity round-trip (state went DetachedOnDuty → Captive → Attached while the record kept
/// its duty) and would have charged trust for a failure the player was physically prevented from
/// avoiding — issue #428.
///
/// Both defects are unrepresentable now: nothing here touches presence, so there is no exposure,
/// and captivity cancels rather than orphans.
///
/// The roll reuses <see cref="ISkillCheckService"/> verbatim — the same service the interactive
/// duties use — rather than inventing a second randomness path. Skills come from the row's own
/// <c>supportSkills</c>, which was authored for all 13 rows and had no consumer until now.
/// </summary>
public class FieldDutyRuntime : IFieldDutyRuntime
{
    /// <summary>Rank contribution to the check. Shared with the interactive duties so the two cannot drift.</summary>
    private const int RankBonusPerLevel = SkillCheckService.RankBonusPerLevel;

    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly IServiceRewardService _rewards;
    private readonly ISkillCheckService _skillCheck;
    private readonly IHeroSkillXpAdapter _skillXp;
    private readonly IInquiryAdapter _inquiry;
    private readonly IModLogger _logger;

    public FieldDutyRuntime(
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        IEnlistmentContentConfigProvider config,
        IServiceRewardService rewards,
        ISkillCheckService skillCheck,
        IHeroSkillXpAdapter skillXp,
        IInquiryAdapter inquiry,
        IModLogger logger)
    {
        _store = store;
        _contentStore = contentStore;
        _config = config;
        _rewards = rewards;
        _skillCheck = skillCheck;
        _skillXp = skillXp;
        _inquiry = inquiry;
        _logger = logger;
    }

    public bool Start(DutyDefinition duty, double nowDays)
    {
        if (duty == null)
            return false;

        var record = _contentStore.Record;
        if (record.HasActiveDuty)
            return false;

        record.ActiveDutyId = duty.Id;
        record.ShiftEndDay = nowDays + duty.DurationHours / 24.0;

        _logger?.LogInfo(
            $"[Enlistment.Duties] duty '{duty.Id}' started — {duty.DurationHours}h shift, " +
            $"difficulty {duty.Difficulty}, skills [{string.Join(",", duty.SupportSkills)}], " +
            $"ends day {record.ShiftEndDay:F2} (state {_store.Record.State}, attached throughout)");
        return true;
    }

    public void HourlyUpdate(double nowDays)
    {
        var record = _contentStore.Record;
        if (!record.HasActiveDuty)
            return;

        if (!_store.Record.IsEnlisted)
        {
            CancelActive("discharge");
            return;
        }

        // Captivity cancels rather than orphans. `IsEnlisted` deliberately INCLUDES
        // EnlistedPlayerCaptive (EnlistmentRecord.cs), so the discharge guard above does not
        // catch a prisoner — without this the shift timer would keep running in a dungeon and pay
        // out a completed duty from captivity. That is the surviving half of #428: the redesign
        // removes the state-machine orphan, this removes the record orphan.
        if (_store.Record.State == EnlistmentState.EnlistedPlayerCaptive)
        {
            CancelActive("captive");
            return;
        }

        // A non-finite day makes the shift comparison false forever, stranding the duty. Skip the
        // tick; it resolves on the next finite one.
        if (double.IsNaN(nowDays) || double.IsInfinity(nowDays))
        {
            _logger?.LogError($"[Enlistment.Duties] non-finite campaign day ({nowDays}) — skipping duty update for '{record.ActiveDutyId}'");
            return;
        }

        var duty = FindDuty(record.ActiveDutyId);
        if (duty == null)
        {
            CancelActive("missing-definition");
            return;
        }

        // LEGACY SAVE MIGRATION, and a self-heal. `Start` always sets ShiftEndDay, so an active
        // duty without one can only come from a save written under the old travel model, where
        // the duty was tracked by ActiveDutyDeadlineDay + a target instead. Left alone it would
        // never satisfy the gate below and would sit in the record forever, blocking every future
        // duty offer (`Start` refuses while one is active). Cancel it — no reward, no penalty; the
        // player did not fail anything, the mechanic changed underneath them.
        if (!record.ShiftEndDay.HasValue)
        {
            CancelActive("legacy-duty-without-shift");
            return;
        }

        // Positive requirement, so a non-finite ShiftEndDay fails the gate rather than resolving
        // instantly (NaN >= NaN is false either way, but the shape is the one the rules mandate).
        if (!(nowDays >= record.ShiftEndDay.Value))
            return;

        Resolve(duty);
    }

    public void CancelActive(string reason)
    {
        var record = _contentStore.Record;
        if (!record.HasActiveDuty)
            return;

        var id = record.ActiveDutyId;
        record.ClearActiveDuty();
        _logger?.LogInfo($"[Enlistment.Duties] duty '{id}' cancelled ({reason}) — no reward, no penalty");
    }

    /// <summary>
    /// One check, one outcome. Success and failure differ ONLY in which <c>RewardSpec</c> reaches
    /// <see cref="IServiceRewardService.Grant"/> — the single reward chokepoint — so a duty cannot
    /// pay through a side channel.
    /// </summary>
    private void Resolve(DutyDefinition duty)
    {
        var record = _contentStore.Record;
        var heroId = _store.Record.EnlistedHeroId;

        var primary = SkillValue(heroId, duty.SupportSkills.ElementAtOrDefault(0));
        int? secondary = duty.SupportSkills.Count > 1
            ? SkillValue(heroId, duty.SupportSkills[1])
            : (int?)null;

        var passed = _skillCheck.Passes(
            primary, secondary, record.Trust, (int)record.Rank * RankBonusPerLevel, duty.Difficulty);

        record.ClearActiveDuty();

        if (passed)
        {
            _rewards.Grant(duty.ReportReward, "duty:" + duty.Id);
            record.DutySuccesses++;
        }
        else
        {
            _rewards.Grant(duty.FailureReward, "duty-failed:" + duty.Id);
            record.DutyFailures++;
        }

        _inquiry?.ShowMessage(
            passed ? "taom_enlist_duty_" + duty.Id + "_success" : "taom_enlist_duty_" + duty.Id + "_failure",
            passed ? "It went well." : "It didn't go as planned.",
            null, null);

        _logger?.LogInfo(
            $"[Enlistment.Duties] duty '{duty.Id}' {(passed ? "completed" : "failed")} — " +
            $"skill {primary}{(secondary.HasValue ? "/" + secondary.Value : "")} " +
            $"trust {record.Trust} rank {record.Rank} vs difficulty {duty.Difficulty}");
    }

    private int SkillValue(string heroId, string skillId)
        => string.IsNullOrEmpty(skillId) ? 0 : (_skillXp?.GetSkillValue(heroId, skillId) ?? 0);

    private DutyDefinition FindDuty(string dutyId)
    {
        if (string.IsNullOrEmpty(dutyId))
            return null;
        return _config.GetDuties().FieldDuties.FirstOrDefault(d => d.Id == dutyId);
    }
}
