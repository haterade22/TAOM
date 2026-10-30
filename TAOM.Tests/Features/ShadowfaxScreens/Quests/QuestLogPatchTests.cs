using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TAOM.Adapters;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Features.ShadowfaxScreens.Quests;
using TAOM.Features.ShadowfaxScreens.Quests.Hooks;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ShadowfaxScreens.Quests;

/// <summary>
/// The six quest-log patch entry points: the plain calls on bare (uninitialised) engine objects, and the two Harmony
/// shapes that need the real runtime, the constructor's prefix and finalizer pair (including a stripped prefix) and the
/// sort postfix whose parameter is an <c>int</c>. RequiresGame because the patch classes name engine types.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class QuestLogPatchTests
{
    private const string HarmonyId = "taom.tests.questlogpatch";

    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private IShadowfaxScreensSettingsProvider _settings = null!;
    private IQuestJournalAdapter _adapter = null!;
    private QuestLogDeferService _service = null!;

    public enum StandInIndex
    {
        First,
        Last,
    }

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _)
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestInitialize]
    public void Setup()
    {
        _clock = new FakeStampClock { Now = 100 };
        _logger = new RecordingLogger();
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.QuestLogFastOpen.Returns(true);
        _settings.LogScreenOpenTimes.Returns(true);
        _adapter = Substitute.For<IQuestJournalAdapter>();
        _adapter.IsBound.Returns(true);
        _service = new QuestLogDeferService(_settings, _adapter, _clock, _logger);
        QuestLogDeferCalls.Initialize(_service);
        ScreenOpenTiming.Initialize(new ScreenOpenTimer(_clock, _logger, _settings));
    }

    [TestCleanup]
    public void Cleanup()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        QuestLogDeferCalls.Initialize(null);
        ScreenOpenTiming.Initialize(null);
    }

    private static QuestItemVM BareItem(bool withJournalEntry)
    {
        var item = (QuestItemVM)FormatterServices.GetUninitializedObject(typeof(QuestItemVM));
        if (withJournalEntry)
        {
            var entry = FormatterServices.GetUninitializedObject(typeof(JournalLogEntry));
            AccessTools.Field(typeof(QuestItemVM), "<QuestLogEntry>k__BackingField")!.SetValue(item, entry);
        }

        return item;
    }

    // ---- PopulateQuestLog ----

    [TestMethod]
    public void PopulateQuestLogPrefix_InScopeForAJournalItem_SkipsTheOriginalAndMarksIt()
    {
        var item = BareItem(true);
        _service.BeginOpen();

        var runOriginal = QuestItemVM_PopulateQuestLog_QuestLogPatch.Prefix(item, BareLog());

        Assert.IsFalse(runOriginal);
        Assert.IsTrue(_service.IsDeferred(item));
    }

    [TestMethod]
    public void PopulateQuestLogPrefix_ItemWithoutAJournalEntry_RunsTheOriginal()
    {
        var item = BareItem(false);
        _service.BeginOpen();

        Assert.IsTrue(QuestItemVM_PopulateQuestLog_QuestLogPatch.Prefix(item, BareLog()));
        Assert.IsFalse(_service.IsDeferred(item));
    }

    [TestMethod]
    public void PopulateQuestLogPrefix_OutOfScope_RunsTheOriginal()
    {
        Assert.IsTrue(QuestItemVM_PopulateQuestLog_QuestLogPatch.Prefix(BareItem(true), BareLog()));
    }

    [TestMethod]
    public void PopulateQuestLogPrefix_Unwired_RunsTheOriginal()
    {
        QuestLogDeferCalls.Initialize(null);

        Assert.IsTrue(QuestItemVM_PopulateQuestLog_QuestLogPatch.Prefix(BareItem(true), BareLog()));
    }

    // ---- UpdateIsUpdated ----

    [TestMethod]
    public void UpdateIsUpdatedPostfix_DeferredItem_WritesTheAdapterAnswerThroughTheSetter()
    {
        var item = BareItem(true);
        _service.BeginOpen();
        _service.TryDefer(item, true);
        _adapter.AnyUnexamined(item).Returns(true);

        QuestItemVM_UpdateIsUpdated_QuestLogPatch.Postfix(item);

        Assert.IsTrue(item.IsUpdated);
    }

    [TestMethod]
    public void UpdateIsUpdatedPostfix_UnmarkedItem_LeavesVanillasValue()
    {
        var item = BareItem(true);
        _adapter.AnyUnexamined(item).Returns(true);

        QuestItemVM_UpdateIsUpdated_QuestLogPatch.Postfix(item);

        Assert.IsFalse(item.IsUpdated);
        _adapter.DidNotReceive().AnyUnexamined(Arg.Any<object>());
    }

    // ---- SetSelectedItem ----

    [TestMethod]
    public void SetSelectedItemPrefix_DeferredItem_BuildsItBeforeVanillaRuns()
    {
        var item = BareItem(true);
        _service.BeginOpen();
        _service.TryDefer(item, true);

        QuestsVM_SetSelectedItem_QuestLogPatch.Prefix(item);

        _adapter.Received(1).Rebuild(item);
    }

    [TestMethod]
    public void SetSelectedItemPrefix_Null_DoesNothing()
    {
        QuestsVM_SetSelectedItem_QuestLogPatch.Prefix(null!);

        _adapter.DidNotReceive().Rebuild(Arg.Any<object>());
    }

    [TestMethod]
    public void SetSelectedItemPrefix_ToggleOff_StillBuildsAMarkedItem()
    {
        var item = BareItem(true);
        _service.BeginOpen();
        _service.TryDefer(item, true);
        _settings.QuestLogFastOpen.Returns(false);

        QuestsVM_SetSelectedItem_QuestLogPatch.Prefix(item);

        _adapter.Received(1).Rebuild(item);
    }

    // ---- the journal constructor's timing ----

    [TestMethod]
    public void JournalCtorPair_CountsOneItemAndItsTime()
    {
        _service.BeginOpen();

        QuestItemVM_JournalCtor_QuestLogPatch.Prefix(out var state);
        _clock.Advance(7);
        QuestItemVM_JournalCtor_QuestLogPatch.Finalizer(state);

        StringAssert.StartsWith(_service.EndOpen(), "fast open on; old=1,");
    }

    [TestMethod]
    public void JournalCtorFinalizer_PrefixStripped_AddsNothing()
    {
        _service.BeginOpen();

        QuestItemVM_JournalCtor_QuestLogPatch.Finalizer(0);

        StringAssert.EndsWith(_service.EndOpen(), "journal items 0.0 ms");
    }

    // ---- the QuestsVM constructor pair, through real Harmony ----

    public static string? SeenDuringTarget;

    public static void OpenTarget(Action closeQuestsScreen)
    {
        SeenDuringTarget = QuestLogDeferCalls.ShouldSkipPopulate(new object(), true, null) ? "deferring" : "vanilla";
        if (closeQuestsScreen != null)
            closeQuestsScreen();
    }

    private void PatchOpenTarget(bool withPrefix)
    {
        new Harmony(HarmonyId).Patch(
            AccessTools.Method(typeof(QuestLogPatchTests), nameof(OpenTarget)),
            prefix: withPrefix ? new HarmonyMethod(AccessTools.Method(typeof(QuestsVM_Ctor_QuestLogPatch), "Prefix")) : null,
            finalizer: new HarmonyMethod(AccessTools.Method(typeof(QuestsVM_Ctor_QuestLogPatch), "Finalizer")));
    }

    [TestMethod]
    public void CtorPair_Succeeds_OpensTheScopeForTheTargetAndWritesOneLine()
    {
        SeenDuringTarget = null;
        PatchOpenTarget(withPrefix: true);

        OpenTarget(() => _clock.Advance(30));

        Assert.AreEqual("deferring", SeenDuringTarget);
        StringAssert.StartsWith(_logger.Lines.Single(), "INFO [ScreenOpen] Quests open: 30.0 ms (fast open on; old=0, deferred=1,");
        Assert.IsFalse(QuestLogDeferCalls.ShouldSkipPopulate(new object(), true, null), "the finalizer closed the scope");
    }

    [TestMethod]
    public void CtorPair_TargetThrows_RethrowsTheSameExceptionWithItsThrowSiteAndStillClosesTheScope()
    {
        PatchOpenTarget(withPrefix: true);

        var thrown = Assert.ThrowsException<InvalidOperationException>(() => OpenTarget(Thrower));

        Assert.AreEqual("vm failed", thrown.Message);
        StringAssert.Contains(thrown.StackTrace, nameof(Thrower));
        StringAssert.Contains(_logger.Lines.Single(), ", threw InvalidOperationException");
        Assert.IsFalse(QuestLogDeferCalls.ShouldSkipPopulate(new object(), true, null));
    }

    [TestMethod]
    public void CtorFinalizer_PrefixStripped_WritesNothingAndTouchesNoScope()
    {
        SeenDuringTarget = null;
        PatchOpenTarget(withPrefix: false);
        _service.BeginOpen();

        OpenTarget(null!);

        Assert.AreEqual("deferring", SeenDuringTarget, "the scope belongs to the caller who opened it");
        Assert.AreEqual(0, _logger.Lines.Count);
        Assert.IsTrue(QuestLogDeferCalls.ShouldSkipPopulate(new object(), true, null), "a finalizer without its prefix must not close another scope");
    }

    // ---- the journal constructor's pair, through real Harmony ----

    public static void JournalCtorTarget(Action advance) => advance();

    private void PatchJournalCtorTarget()
    {
        new Harmony(HarmonyId).Patch(
            AccessTools.Method(typeof(QuestLogPatchTests), nameof(JournalCtorTarget)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(QuestItemVM_JournalCtor_QuestLogPatch), "Prefix")),
            finalizer: new HarmonyMethod(AccessTools.Method(typeof(QuestItemVM_JournalCtor_QuestLogPatch), "Finalizer")));
    }

    [TestMethod]
    public void JournalCtorPair_ThroughHarmony_CountsEachItemAndTotalsItsTime()
    {
        PatchJournalCtorTarget();
        _service.BeginOpen();

        JournalCtorTarget(() => _clock.Advance(4));
        JournalCtorTarget(() => _clock.Advance(6));

        var detail = _service.EndOpen();
        StringAssert.Contains(detail, "old=2,");
        StringAssert.EndsWith(detail, "journal items 10.0 ms");
    }

    [TestMethod]
    public void JournalCtorPair_ThroughHarmony_TargetThrows_StillClosesTheItemAndRethrows()
    {
        PatchJournalCtorTarget();
        _service.BeginOpen();

        Assert.ThrowsException<InvalidOperationException>(() => JournalCtorTarget(Thrower));

        var detail = _service.EndOpen();
        StringAssert.Contains(detail, "old=1,");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Thrower() => throw new InvalidOperationException("vm failed");

    // ---- GetJournalLogAt postfix, through real Harmony (the engine's enum parameter is read as an int) ----

    public JournalLog? StandInResult;

    public JournalLog? GetJournalLogAt(QuestItemVM questItem, StandInIndex logIndex) => StandInResult;

    private void PatchSortPostfix()
    {
        new Harmony(HarmonyId).Patch(
            AccessTools.Method(typeof(QuestLogPatchTests), nameof(GetJournalLogAt)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(QuestItemComparerBase_GetJournalLogAt_QuestLogPatch), "Postfix")));
    }

    private static JournalLog BareLog() => (JournalLog)FormatterServices.GetUninitializedObject(typeof(JournalLog));

    [TestMethod]
    public void SortPostfix_VanillaReturnedNullForADeferredItem_ReadsTheFirstOrLastEntry()
    {
        PatchSortPostfix();
        var item = BareItem(true);
        var first = BareLog();
        var last = BareLog();
        _service.BeginOpen();
        _service.TryDefer(item, true, first);
        _service.TryDefer(item, true, last);

        Assert.AreSame(first, GetJournalLogAt(item, StandInIndex.First));
        Assert.AreSame(last, GetJournalLogAt(item, StandInIndex.Last));
    }

    [TestMethod]
    public void SortPostfix_VanillaReturnedAnEntry_IsLeftAlone()
    {
        PatchSortPostfix();
        var item = BareItem(true);
        var vanilla = BareLog();
        StandInResult = vanilla;
        _service.BeginOpen();
        _service.TryDefer(item, true, BareLog());

        Assert.AreSame(vanilla, GetJournalLogAt(item, StandInIndex.Last));
    }

    [TestMethod]
    public void SortPostfix_UnmarkedItem_StaysNull()
    {
        PatchSortPostfix();

        Assert.IsNull(GetJournalLogAt(BareItem(true), StandInIndex.First));
    }

    [TestMethod]
    public void SortPostfix_AdapterGivesSomethingThatIsNotAJournalLog_StaysNull()
    {
        PatchSortPostfix();
        var item = BareItem(true);
        _service.BeginOpen();
        _service.TryDefer(item, true, "not a log");

        Assert.IsNull(GetJournalLogAt(item, StandInIndex.First));
    }

    [TestMethod]
    public void SortPostfix_TargetMethodResolvesTheEnginesComparerBase()
    {
        var target = (MethodBase)typeof(QuestItemComparerBase_GetJournalLogAt_QuestLogPatch)
            .GetMethod("TargetMethod", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(null, null)!;

        Assert.AreEqual("GetJournalLogAt", target.Name);
        Assert.AreEqual("QuestItemComparerBase", target.DeclaringType!.Name);
    }
}
