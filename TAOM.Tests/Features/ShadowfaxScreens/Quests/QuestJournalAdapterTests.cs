using System;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Quests;
using TAOM.Features.ShadowfaxScreens.Quests.Hooks;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ShadowfaxScreens.Quests;

/// <summary>
/// The real <see cref="QuestJournalAdapter"/> against the installed engine: Bind names a missing guard patch, and a patch
/// removed after the install turns deferral off on the next open. The four patches are applied under a test Harmony id.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class QuestJournalAdapterTests
{
    private const string HarmonyId = "taom.tests.questjournaladapter";

    private static readonly Type[] PatchTypes =
    {
        typeof(QuestItemVM_PopulateQuestLog_QuestLogPatch),
        typeof(QuestsVM_SetSelectedItem_QuestLogPatch),
        typeof(QuestItemVM_UpdateIsUpdated_QuestLogPatch),
        typeof(QuestItemComparerBase_GetJournalLogAt_QuestLogPatch),
    };

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _)
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestCleanup]
    public void Cleanup() => new Harmony(HarmonyId).UnpatchAll(HarmonyId);

    private static void Attach(params Type[] patchTypes)
    {
        var harmony = new Harmony(HarmonyId);
        foreach (var type in patchTypes)
            harmony.CreateClassProcessor(type).Patch();
    }

    [TestMethod]
    public void Bind_AllFourPatchesAttached_ReturnsNullAndIsBound()
    {
        Attach(PatchTypes);
        var sut = new QuestJournalAdapter();

        Assert.IsNull(sut.Bind());
        Assert.IsTrue(sut.IsBound);
    }

    [TestMethod]
    public void PatchedGetJournalLogAt_WithTheIntPostfix_JitCompiles()
    {
        Attach(PatchTypes);

        var patched = QuestItemComparerBase_GetJournalLogAt_QuestLogPatch.TargetMethod();

        System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(patched.MethodHandle);
    }

    [TestMethod]
    public void Bind_NothingAttached_NamesEveryMissingPatchAndIsNotBound()
    {
        var sut = new QuestJournalAdapter();

        var problem = sut.Bind();

        Assert.IsNotNull(problem);
        foreach (var type in PatchTypes)
            StringAssert.Contains(problem, type.Name);
        Assert.IsFalse(sut.IsBound);
    }

    [TestMethod]
    public void Bind_OnePatchMissing_NamesOnlyThatPatch()
    {
        Attach(PatchTypes.Where(t => t != typeof(QuestItemVM_UpdateIsUpdated_QuestLogPatch)).ToArray());
        var sut = new QuestJournalAdapter();

        var problem = sut.Bind();

        Assert.AreEqual("not attached: " + nameof(QuestItemVM_UpdateIsUpdated_QuestLogPatch), problem);
    }

    [TestMethod]
    public void IsBound_APatchStrippedAfterBind_IsFalseAndTheNextOpenDoesNotDefer()
    {
        Attach(PatchTypes);
        var adapter = new QuestJournalAdapter();
        Assert.IsNull(adapter.Bind());
        var settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        settings.QuestLogFastOpen.Returns(true);
        var service = new QuestLogDeferService(settings, adapter, new FakeStampClock { Now = 100 }, new RecordingLogger());
        service.BeginOpen();
        Assert.IsTrue(service.TryDefer(new object(), true), "attached: the open defers");
        service.EndOpen();

        var harmony = new Harmony(HarmonyId);
        harmony.Unpatch(QuestItemComparerBase_GetJournalLogAt_QuestLogPatch.TargetMethod(),
            AccessTools.Method(typeof(QuestItemComparerBase_GetJournalLogAt_QuestLogPatch), "Postfix"));
        service.BeginOpen();

        Assert.IsFalse(adapter.IsBound);
        Assert.IsFalse(service.TryDefer(new object(), true), "a guard patch is gone: the next open must not defer");
    }
}
