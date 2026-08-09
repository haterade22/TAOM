using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Duties;

namespace TAOM.Tests.Features.Enlistment.Duties;

/// <summary>
/// Field duties are camp work: accept one, it occupies a few hours, one skill check decides it.
///
/// The tests this file REPLACED pinned the opposite model — travel, spawned hunt targets, and a
/// detached player. `_attachment.Received(1).RestorePresence()` was among them, which is literally
/// the call that made a one-man party visible in contested territory and got a live tester captured
/// 41 seconds into a duty on 2026-08-08. Those assertions were deleted rather than repaired,
/// because they asserted the defect.
/// </summary>
[TestClass]
public class FieldDutyRuntimeTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentContentStore _contentStore = null!;
    private IEnlistmentContentConfigProvider _config = null!;
    private IServiceRewardService _rewards = null!;
    private ISkillCheckService _skillCheck = null!;
    private IHeroSkillXpAdapter _skillXp = null!;
    private IInquiryAdapter _inquiry = null!;
    private IRealTimeProvider _realTime = null!;
    private FieldDutyRuntime _runtime = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _contentStore = new EnlistmentContentStore(_logger);
        _config = Substitute.For<IEnlistmentContentConfigProvider>();
        _rewards = Substitute.For<IServiceRewardService>();
        _skillCheck = Substitute.For<ISkillCheckService>();
        _skillXp = Substitute.For<IHeroSkillXpAdapter>();
        _inquiry = Substitute.For<IInquiryAdapter>();
        _realTime = Substitute.For<IRealTimeProvider>();
        // Default: the clock never advances between hourly ticks, so no rate is ever established
        // and ShiftIsTooFastToAnnounce stays false. Every pre-existing test therefore keeps the
        // two-toast behaviour it was written against; the fold is opted into explicitly below.
        _realTime.ElapsedSeconds.Returns(0.0);

        _runtime = new FieldDutyRuntime(
            _store, _contentStore, _config, _rewards, _skillCheck, _skillXp, _inquiry, _realTime, _logger);

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
    }

    private void ConfigureDuties(params DutyDefinition[] duties)
        => _config.GetDuties().Returns(new EnlistmentDutiesConfig { FieldDuties = new List<DutyDefinition>(duties) });

    private static DutyDefinition Duty(
        string id = "recon_sweep", int hours = 6, int difficulty = 55, params string[] skills)
        => new DutyDefinition
        {
            Id = id,
            DurationHours = hours,
            Difficulty = difficulty,
            SupportSkills = new List<string>(skills.Length > 0 ? skills : new[] { "Scouting" }),
            ReportReward = new RewardSpec { ServiceXp = 45 },
            FailureReward = new RewardSpec { ServiceXp = 10, Trust = -1 },
        };

    private void StartAndReachShiftEnd(DutyDefinition duty, bool passes)
    {
        ConfigureDuties(duty);
        _skillCheck.Passes(default, default, default, default, default).ReturnsForAnyArgs(passes);
        Assert.IsTrue(_runtime.Start(duty, 10.0));
        _runtime.HourlyUpdate(10.0 + duty.DurationHours / 24.0);
    }

    // ---- Start ----

    [TestMethod]
    public void Start_SetsTheDutyAndItsShiftEnd()
    {
        var duty = Duty(hours: 6);
        ConfigureDuties(duty);

        Assert.IsTrue(_runtime.Start(duty, 10.0));

        Assert.AreEqual("recon_sweep", _contentStore.Record.ActiveDutyId);
        Assert.AreEqual(10.0 + 6.0 / 24.0, _contentStore.Record.ShiftEndDay.GetValueOrDefault(), 1e-9);
    }

    [TestMethod]
    public void Start_NullDuty_ReturnsFalse()
        => Assert.IsFalse(_runtime.Start(null, 10.0));

    [TestMethod]
    public void Start_DutyAlreadyActive_ReturnsFalseAndKeepsTheFirst()
    {
        var first = Duty("recon_sweep");
        ConfigureDuties(first);
        Assert.IsTrue(_runtime.Start(first, 10.0));

        Assert.IsFalse(_runtime.Start(Duty("road_patrol"), 10.0));
        Assert.AreEqual("recon_sweep", _contentStore.Record.ActiveDutyId);
    }

    [TestMethod]
    public void Start_DoesNotTouchPlayerState()
    {
        // The negative property that is the entire reason this model exists. The runtime no longer
        // holds an attachment service or a state machine at all, so there is nothing it COULD
        // detach with — asserted here as behaviour, and structurally by
        // FieldDutyRuntime_HasNoPresenceOrSpawnDependencies below.
        var duty = Duty();
        ConfigureDuties(duty);

        _runtime.Start(duty, 10.0);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State,
            "a field duty must never move the player out of the attached state");
    }

    // ---- Resolution ----

    [TestMethod]
    public void HourlyUpdate_BeforeShiftEnd_DoesNothing()
    {
        var duty = Duty(hours: 6);
        ConfigureDuties(duty);
        _runtime.Start(duty, 10.0);

        _runtime.HourlyUpdate(10.0 + 5.0 / 24.0);

        Assert.IsTrue(_contentStore.Record.HasActiveDuty);
        _rewards.DidNotReceiveWithAnyArgs().Grant(default, default);
    }

    [TestMethod]
    public void HourlyUpdate_ShiftEndAndCheckPasses_GrantsSuccessAndCountsIt()
    {
        var duty = Duty();
        StartAndReachShiftEnd(duty, passes: true);

        _rewards.Received(1).Grant(duty.ReportReward, "duty:recon_sweep");
        Assert.AreEqual(1, _contentStore.Record.DutySuccesses);
        Assert.AreEqual(0, _contentStore.Record.DutyFailures);
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void HourlyUpdate_ShiftEndAndCheckFails_GrantsFailureRewardAndCountsIt()
    {
        var duty = Duty();
        StartAndReachShiftEnd(duty, passes: false);

        _rewards.Received(1).Grant(duty.FailureReward, "duty-failed:recon_sweep");
        Assert.AreEqual(1, _contentStore.Record.DutyFailures);
        Assert.AreEqual(0, _contentStore.Record.DutySuccesses);
        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void HourlyUpdate_UsesTheRowsDifficultyAndSkills()
    {
        _skillXp.GetSkillValue("main_hero", "Tactics").Returns(70);
        _skillXp.GetSkillValue("main_hero", "Scouting").Returns(40);
        _contentStore.Record.Trust = 5;
        _contentStore.Record.Rank = ServiceRank.Veteran;

        var duty = Duty("hideout_strike", hours: 8, difficulty: 76, "Tactics", "Scouting");
        StartAndReachShiftEnd(duty, passes: true);

        _skillCheck.Received(1).Passes(
            70, 40, 5, (int)ServiceRank.Veteran * SkillCheckService.RankBonusPerLevel, 76);
    }

    [TestMethod]
    public void HourlyUpdate_SingleSupportSkill_PassesNullSecondary()
    {
        _skillXp.GetSkillValue("main_hero", "Scouting").Returns(33);
        var duty = Duty("recon_sweep", difficulty: 55, skills: "Scouting");

        StartAndReachShiftEnd(duty, passes: true);

        _skillCheck.Received(1).Passes(33, null, Arg.Any<int>(), Arg.Any<int>(), 55);
    }

    // ---- Hygiene exits ----

    [TestMethod]
    public void HourlyUpdate_NoLongerEnlisted_CancelsWithoutPayingAnything()
    {
        var duty = Duty();
        ConfigureDuties(duty);
        _runtime.Start(duty, 10.0);
        _store.Record.State = EnlistmentState.NotEnlisted;

        _runtime.HourlyUpdate(99.0);

        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
        _rewards.DidNotReceiveWithAnyArgs().Grant(default, default);
    }

    [TestMethod]
    public void HourlyUpdate_PlayerCaptive_CancelsAndPaysNothing()
    {
        // REGRESSION, #428. EnlistmentRecord.IsEnlisted deliberately INCLUDES
        // EnlistedPlayerCaptive, so the discharge guard above does not catch a prisoner. Without
        // the explicit captivity check the shift timer keeps running in a dungeon and pays out a
        // completed duty from captivity.
        var duty = Duty();
        ConfigureDuties(duty);
        _runtime.Start(duty, 10.0);
        _store.Record.State = EnlistmentState.EnlistedPlayerCaptive;

        _runtime.HourlyUpdate(99.0);

        Assert.IsFalse(_contentStore.Record.HasActiveDuty, "captivity must cancel, not orphan");
        _rewards.DidNotReceiveWithAnyArgs().Grant(default, default);
        Assert.AreEqual(0, _contentStore.Record.DutyFailures, "being taken prisoner is not a duty failure");
    }

    [TestMethod]
    public void HourlyUpdate_DutyDefinitionGone_Cancels()
    {
        var duty = Duty();
        ConfigureDuties(duty);
        _runtime.Start(duty, 10.0);
        ConfigureDuties();

        _runtime.HourlyUpdate(99.0);

        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
    }

    [TestMethod]
    public void HourlyUpdate_LegacyDutyWithNoShiftEnd_CancelsInsteadOfSticking()
    {
        // A save written under the travel model carries ActiveDutyId with no ShiftEndDay. Left
        // alone it can never satisfy the shift gate, so it would occupy the single duty slot
        // forever and block every future offer.
        var duty = Duty();
        ConfigureDuties(duty);
        _contentStore.Record.ActiveDutyId = duty.Id;
        _contentStore.Record.ShiftEndDay = null;

        _runtime.HourlyUpdate(99.0);

        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
        _rewards.DidNotReceiveWithAnyArgs().Grant(default, default);
        Assert.AreEqual(0, _contentStore.Record.DutyFailures, "a mechanic change is not the player's failure");
    }

    [TestMethod]
    public void HourlyUpdate_NonFiniteDay_SkipsAndKeepsTheDuty()
    {
        var duty = Duty();
        ConfigureDuties(duty);
        _runtime.Start(duty, 10.0);

        _runtime.HourlyUpdate(double.NaN);

        Assert.IsTrue(_contentStore.Record.HasActiveDuty, "a garbage tick must not resolve or destroy the duty");
        _rewards.DidNotReceiveWithAnyArgs().Grant(default, default);
    }

    [TestMethod]
    public void HourlyUpdate_NoActiveDuty_NoOp()
    {
        _runtime.HourlyUpdate(10.0);
        _rewards.DidNotReceiveWithAnyArgs().Grant(default, default);
    }

    [TestMethod]
    public void CancelActive_ClearsWithoutRewardOrPenalty()
    {
        var duty = Duty();
        ConfigureDuties(duty);
        _runtime.Start(duty, 10.0);

        _runtime.CancelActive("discharge");

        Assert.IsFalse(_contentStore.Record.HasActiveDuty);
        _rewards.DidNotReceiveWithAnyArgs().Grant(default, default);
        Assert.AreEqual(0, _contentStore.Record.DutyFailures);
    }

    // ---- Structural guard ----

    [TestMethod]
    public void FieldDutyRuntime_HasNoPresenceOrSpawnDependencies()
    {
        // A behavioural test cannot prove a NEGATIVE about code that no longer has the dependency
        // — it can only prove today's build does not call it. This reddens if anyone reintroduces
        // detachment or party spawning, which is the specific regression that would re-expose the
        // player (#428) and re-open the #375 re-entrancy surface.
        var src = File.ReadAllText(FromRepoRoot("Main/Features/Enlistment/Duties/FieldDutyRuntime.cs"))
            .Replace("\r\n", "\n");

        foreach (var banned in new[]
                 {
                     "RestorePresence", "IServiceAttachmentService", "ExitSettlementForDuty",
                     "SpawnLooterParty", "DestroyParty", "IDutyWorldAdapter",
                     "EnlistedDetachedOnDuty",
                 })
        {
            // Comments legitimately NAME these while explaining why they are gone, so only
            // executable references count: strip // and /// lines before matching.
            var code = string.Join("\n", System.Array.FindAll(
                src.Split('\n'), l => !l.TrimStart().StartsWith("//")));
            StringAssert.DoesNotMatch(code, new System.Text.RegularExpressions.Regex(
                System.Text.RegularExpressions.Regex.Escape(banned)),
                $"FieldDutyRuntime must not reference '{banned}' — field duties are camp work and "
                + "must never detach the player or spawn a party (#428, #375).");
        }
    }

    private static string FromRepoRoot(string relPath)
    {
        var dir = new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            // File.Exists too: in a git worktree `.git` is a FILE holding a gitdir: pointer (f1bc6b39).
            var gitPath = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
                return Path.Combine(dir.FullName, relPath.Replace('/', Path.DirectorySeparatorChar));
            dir = dir.Parent;
        }

        throw new System.InvalidOperationException(
            "repo root (.git) not found from " + System.AppDomain.CurrentDomain.BaseDirectory);
    }
    // ---- #436: the assignment toast is skipped when the shift is too fast to read two messages ----

    /// <summary>Drives two hourly ticks the given number of real seconds apart, which is what
    /// establishes the runtime's real-seconds-per-campaign-hour estimate.</summary>
    private void EstablishTickRate(double secondsPerCampaignHour)
    {
        _realTime.ElapsedSeconds.Returns(100.0, 100.0 + secondsPerCampaignHour);
        _runtime.HourlyUpdate(10.0);
        _runtime.HourlyUpdate(10.0 + (1.0 / 24.0));
    }

    private static DutyDefinition FourHourDuty() => new DutyDefinition
    {
        Id = "scout_route", DurationHours = 4, Difficulty = 50,
        SupportSkills = new List<string> { "Scouting" },
    };

    [TestMethod]
    public void Start_ShiftFasterThanTheToastWindow_SkipsTheAssignmentToast()
    {
        // 1 real second per campaign hour => a 4-hour shift is 4 real seconds, inside the 10s
        // window. This is the measured live case (#436): started 09:29:38, resolved 09:29:42.
        EstablishTickRate(1.0);

        _runtime.Start(FourHourDuty(), 10.0);

        _inquiry.DidNotReceive().ShowMessage(
            "taom_enlist_duty_assigned", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Start_ShiftSlowerThanTheToastWindow_AnnouncesNormally()
    {
        // 10 real seconds per campaign hour => a 4-hour shift is 40 real seconds, well outside the
        // window. The player has time to read both messages, so both are shown.
        EstablishTickRate(10.0);

        _runtime.Start(FourHourDuty(), 10.0);

        _inquiry.Received(1).ShowMessage(
            "taom_enlist_duty_assigned", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Start_NoTickRateObservedYet_AnnouncesNormally()
    {
        // The save/load and first-duty-of-session case. An unknown rate must resolve to ANNOUNCE:
        // a redundant toast is cosmetic, a missing one is the bug. No EstablishTickRate call here.
        _runtime.Start(FourHourDuty(), 10.0);

        _inquiry.Received(1).ShowMessage(
            "taom_enlist_duty_assigned", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Start_NonFiniteClock_AnnouncesNormally()
    {
        // NaN gate polarity (csharp-architecture.md): every comparison against NaN is false, so a
        // poisoned clock must fall out on the ANNOUNCE side rather than silently suppressing.
        _realTime.ElapsedSeconds.Returns(double.NaN);
        _runtime.HourlyUpdate(10.0);
        _runtime.HourlyUpdate(10.0 + (1.0 / 24.0));

        _runtime.Start(FourHourDuty(), 10.0);

        _inquiry.Received(1).ShowMessage(
            "taom_enlist_duty_assigned", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Resolve_AlwaysEmitsASelfContainedResultNamingTheDuty()
    {
        // The load-bearing half of #436. Whether or not the assignment toast was shown, the result
        // must carry both the duty and the outcome — that is what makes skipping the assignment
        // safe, and what stops a player who blinked from losing trust to an unnamed duty.
        EstablishTickRate(1.0);
        var duty = FourHourDuty();
        ConfigureDuties(duty);
        _skillCheck.Passes(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(false);

        _runtime.Start(duty, 10.0);
        _runtime.HourlyUpdate(10.0 + 1.0);

        _inquiry.Received(1).ShowMessage(
            "taom_enlist_duty_result", Arg.Any<string>(),
            "DUTY", Arg.Any<string>(),
            "RESULT", Arg.Any<string>());
    }

}
