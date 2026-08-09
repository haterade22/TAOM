using System.Linq;
using TaleWorlds.Localization;
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
    /// <summary>Rank contribution to the check, aliased from the one definition both duty
    /// systems use. Field duties apply it unconditionally; see SkillCheckService for why the
    /// interactive path does not.</summary>
    private const int RankBonusPerLevel = SkillCheckService.RankBonusPerLevel;

    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly IServiceRewardService _rewards;
    private readonly ISkillCheckService _skillCheck;
    private readonly IHeroSkillXpAdapter _skillXp;
    private readonly IInquiryAdapter _inquiry;
    private readonly IRealTimeProvider _realTime;
    private readonly IModLogger _logger;

    /// <summary>
    /// A duty whose whole shift elapses within this many REAL seconds cannot show the player two
    /// readable messages, so it shows one. Bannerlord's quick-information toast lives about three
    /// seconds; measured live on 2026-08-09 at 4x, a 4-hour shift started 09:29:38 and resolved
    /// 09:29:42 — the assignment toast had faded as the result arrived, and the player reported
    /// having been given one duty when the log records two (#436).
    ///
    /// Ten rather than six. The two failure directions are not symmetric: folding when we did not
    /// need to costs one combined message instead of two, while not folding when we should have is
    /// the reported bug — trust moving off a notification nobody could read. At 1x a 4-hour shift
    /// takes minutes, so this never fires for a player who is not accelerating.
    /// </summary>
    private const double CombinedToastWindowSeconds = 10.0;

    /// <summary>
    /// Real seconds observed between the last two hourly ticks — i.e. how long one campaign hour
    /// currently takes the player, which is what the time-acceleration multiplier controls. Null
    /// until two ticks have been seen.
    ///
    /// Deliberately in-memory and NOT persisted. After a save/load it is null, which predicts an
    /// infinite shift and therefore ALWAYS announces. That is the conservative direction: an extra
    /// assignment toast is a cosmetic redundancy, a missing one is #436.
    /// </summary>
    private double? _realSecondsPerCampaignHour;

    /// <summary><see cref="IRealTimeProvider.ElapsedSeconds"/> at the previous hourly tick.</summary>
    private double? _lastHourlyRealSeconds;

    public FieldDutyRuntime(
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        IEnlistmentContentConfigProvider config,
        IServiceRewardService rewards,
        ISkillCheckService skillCheck,
        IHeroSkillXpAdapter skillXp,
        IInquiryAdapter inquiry,
        IRealTimeProvider realTime,
        IModLogger logger)
    {
        _store = store;
        _contentStore = contentStore;
        _config = config;
        _rewards = rewards;
        _skillCheck = skillCheck;
        _skillXp = skillXp;
        _inquiry = inquiry;
        _realTime = realTime;
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

        // TELL THE PLAYER, and do it here rather than at the offer site so EVERY start path
        // announces — the daily tick assigns duties on its own, not just the wait-menu request.
        // Under the old model a duty was self-announcing by accident: it made the player visible
        // and sent them travelling, so it was impossible to miss. This one is invisible by design,
        // which means the first evidence a duty ever existed would otherwise be the result toast
        // hours later, after it had already moved trust.
        //
        // SKIPPED when the shift will resolve faster than two messages can be read (#436). The
        // result toast names the duty either way, so nothing is lost — the player who reads one
        // message gets the whole story instead of half of it. At 1x this never fires.
        if (!ShiftIsTooFastToAnnounce(duty.DurationHours))
            _inquiry?.ShowMessage(
                "taom_enlist_duty_assigned",
                "You are given orders: {DUTY}.",
                "DUTY",
                DutyTitle(duty.Id));

        _logger?.LogInfo(
            $"[Enlistment.Duties] duty '{duty.Id}' started — {duty.DurationHours}h shift, " +
            $"difficulty {duty.Difficulty}, skills [{string.Join(",", duty.SupportSkills)}], " +
            $"ends day {record.ShiftEndDay:F2} (state {_store.Record.State}, attached throughout)");
        return true;
    }

    /// <summary>
    /// Records real seconds between consecutive hourly ticks. Deltas outside (0, 3600) are
    /// discarded: a non-positive one means the clock did not advance between two calls in the same
    /// frame, and anything past an hour means the process was paused, minimised or breakpointed —
    /// neither describes the player's actual pace, and either would poison the estimate toward
    /// "shifts are slow", which is the direction that reintroduces #436.
    /// </summary>
    private void SampleTickRate()
    {
        var now = _realTime?.ElapsedSeconds;
        if (!now.HasValue || double.IsNaN(now.Value) || double.IsInfinity(now.Value))
            return;

        if (_lastHourlyRealSeconds.HasValue)
        {
            var delta = now.Value - _lastHourlyRealSeconds.Value;
            if (delta > 0.0 && delta < 3600.0)
                _realSecondsPerCampaignHour = delta;
        }

        _lastHourlyRealSeconds = now.Value;
    }

    /// <summary>
    /// The duty's display title. ONE place, because the assignment toast and the result toast must
    /// name a duty identically — two call sites composing the same runtime key is how a raw id like
    /// `recon_sweep` reached the UI once already.
    /// </summary>
    private static string DutyTitle(string dutyId)
        => new TextObject("{=taom_enlist_duty_" + dutyId + "_title}a task for the company").ToString();

    /// <summary>
    /// True when the whole shift is predicted to elapse too fast for the player to read two
    /// separate messages, so the assignment toast is skipped and the result toast carries both
    /// halves. Written as a POSITIVE requirement — <c>predicted &lt;= window</c> rather than
    /// <c>!(predicted &gt; window)</c> — so a NaN or a missing estimate falls out as "announce",
    /// the conservative side. See `.claude/rules/csharp-architecture.md` on NaN gate polarity.
    /// </summary>
    private bool ShiftIsTooFastToAnnounce(int durationHours)
    {
        if (!_realSecondsPerCampaignHour.HasValue)
            return false;

        var predicted = durationHours * _realSecondsPerCampaignHour.Value;
        return predicted > 0.0 && predicted <= CombinedToastWindowSeconds;
    }

    public void HourlyUpdate(double nowDays)
    {
        // BEFORE every guard, deliberately. This samples how long a campaign hour is taking the
        // player in real seconds, and Start needs that estimate at a moment when there is by
        // definition NO active duty — so sampling below the HasActiveDuty guard would mean the
        // estimate only ever existed while it was already too late to use.
        //
        // It is pure measurement: no campaign state is read or written, so it cannot affect the
        // guards it precedes. Kept to one statement for that reason.
        SampleTickRate();

        var record = _contentStore.Record;
        if (!record.HasActiveDuty)
            return;

        if (!_store.Record.IsEnlisted)
        {
            CancelActive("discharge");
            return;
        }

        // A duty can only be WORKED while attached to the column, and `IsEnlisted` is far too wide
        // a predicate for that — it deliberately spans five states, two of which are not service:
        //
        //   EnlistedPlayerCaptive   you are a prisoner. Without this the shift timer keeps running
        //                           in a dungeon and pays out a completed duty from captivity.
        //   CommanderUnavailable    your commander has lost their party; this is a 7-day grace in
        //                           which the reconciler has already called RestorePresence() and
        //                           set you loose. There is no company left to report to, so a duty
        //                           must not pay, fail, or keep ticking through it.
        //
        // Both cancel rather than orphan. The redesign made the state-machine orphan
        // unrepresentable; these two guards remove the RECORD orphan, which is the half of #428
        // that the redesign alone does not fix.
        var state = _store.Record.State;
        if (state != EnlistmentState.EnlistedAttached && state != EnlistmentState.EnlistedBattle)
        {
            CancelActive(state == EnlistmentState.EnlistedPlayerCaptive ? "captive" : "no-commander");
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

        // SELF-CONTAINED (#436). The result names the duty as well as the outcome, so it reads on
        // its own whether or not the assignment toast was shown — which is what lets Start skip
        // that toast at speed, and what stops a player who blinked from losing trust to a duty
        // they never saw named. The per-duty outcome line is resolved here and passed as a
        // variable rather than being concatenated into the template, so translators keep both
        // halves as independent, reorderable strings.
        _inquiry?.ShowMessage(
            "taom_enlist_duty_result",
            "Orders: {DUTY}. {RESULT}",
            "DUTY", DutyTitle(duty.Id),
            "RESULT", new TextObject(
                "{=taom_enlist_duty_" + duty.Id + (passed ? "_success" : "_failure") + "}"
                + (passed ? "It went well." : "It didn't go as planned.")).ToString());

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
