using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Features.ShadowfaxScreens.Party;
using TAOM.Features.ShadowfaxScreens.Party.Hooks;
using TAOM.Tests.Features.LoadTimeStamps;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.ShadowfaxScreens.Party;

/// <summary>
/// The Party Fast Open prefix, postfix and finalizer pairs, applied with real Harmony around stand-ins that carry the
/// engine methods' parameter names and types: a cache hit skips the original, a miss runs it once and stores, a
/// stripped prefix leaves the finalizer a no-op, and the constructor bracket ends its session on a throw. RequiresGame
/// because the patch classes' attributes name engine types.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class PartyFastOpenPatchShapeTests
{
    private const string HarmonyId = "taom.tests.partyfastopen";
    private static int _codeCalls;
    private static bool _throw;

    private IShadowfaxScreensSettingsProvider _settings = null!;
    private PartyScreenFastPathService _service = null!;
    private RecordingLogger _logger = null!;
    private FakeStampClock _clock = null!;

    public static void Ctor()
    {
        if (_throw) throw new InvalidOperationException("ctor failed");
    }

    public static CharacterCode Code(CharacterObject character, PartyScreenLogic.TroopType type,
        PartyScreenLogic.PartyRosterSide side)
    {
        _codeCalls++;
        return CharacterCode.CreateEmpty();
    }

    public static void Window() => Inside?.Invoke();

    public static void InitializeTroopLists() => Inside?.Invoke();

    public static void RefreshValues() => Inside?.Invoke();

    public static void TransferAllTroops(PartyScreenLogic.PartyCommand command) => Inside?.Invoke();

    public static Action? Inside { get; set; }

    [TestInitialize]
    public void Setup()
    {
        _codeCalls = 0;
        _throw = false;
        Inside = null;
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.PartyFastOpen.Returns(true);
        _settings.LogScreenOpenTimes.Returns(true);
        _logger = new RecordingLogger();
        _clock = new FakeStampClock { Now = 100 };
        _service = new PartyScreenFastPathService(_settings, _clock);
        PartyFastOpenCalls.Initialize(_service);
        ScreenOpenTiming.Initialize(new ScreenOpenTimer(_clock, _logger, _settings));
    }

    [TestCleanup]
    public void Cleanup()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        PartyFastOpenCalls.Initialize(null);
        ScreenOpenTiming.Initialize(null);
    }

    private static void Patch(string target, Type patch, bool prefix, bool postfix, bool finalizer)
    {
        HarmonyMethod? Of(string name, bool on) => on ? new HarmonyMethod(AccessTools.Method(patch, name)) : null;
        new Harmony(HarmonyId).Patch(AccessTools.Method(typeof(PartyFastOpenPatchShapeTests), target),
            prefix: Of("Prefix", prefix), postfix: Of("Postfix", postfix), finalizer: Of("Finalizer", finalizer));
    }

    [TestMethod]
    public void CharacterCode_MissRunsOnceAndStores_ThenHitSkipsTheOriginal()
    {
        Patch(nameof(Code), typeof(PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch), true, true, false);
        _service.BeginScreen();
        var character = new CharacterObject();

        var first = Code(character, PartyScreenLogic.TroopType.Member, PartyScreenLogic.PartyRosterSide.Right);
        var second = Code(character, PartyScreenLogic.TroopType.Member, PartyScreenLogic.PartyRosterSide.Right);
        Code(character, PartyScreenLogic.TroopType.Prisoner, PartyScreenLogic.PartyRosterSide.Right);

        Assert.AreSame(first, second);
        Assert.AreEqual(2, _codeCalls);
    }

    [TestMethod]
    public void CharacterCode_NoSession_AlwaysRunsTheOriginal()
    {
        Patch(nameof(Code), typeof(PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch), true, true, false);
        var character = new CharacterObject();

        Code(character, PartyScreenLogic.TroopType.Member, PartyScreenLogic.PartyRosterSide.Left);
        Code(character, PartyScreenLogic.TroopType.Member, PartyScreenLogic.PartyRosterSide.Left);

        Assert.AreEqual(2, _codeCalls);
    }

    [TestMethod]
    public void CharacterCode_NoServiceWired_RunsVanilla()
    {
        Patch(nameof(Code), typeof(PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch), true, true, false);
        PartyFastOpenCalls.Initialize(null);

        Code(new CharacterObject(), PartyScreenLogic.TroopType.Member, PartyScreenLogic.PartyRosterSide.Left);

        Assert.AreEqual(1, _codeCalls);
    }

    [TestMethod]
    public void Ctor_OpensTheSession_AndKeepsItOnSuccess_WritingOneOpenLine()
    {
        Patch(nameof(Ctor), typeof(PartyVM_Ctor_ShadowfaxParty_Patch), true, false, true);

        Ctor();

        Assert.IsTrue(_service.SessionOpen);
        Assert.AreEqual(1, _logger.Lines.Count);
        StringAssert.StartsWith(_logger.Lines[0], "INFO [ScreenOpen] Party open: ");
        StringAssert.Contains(_logger.Lines[0], "(fast open on; lookups 0, codes 0 hit/0 miss");
    }

    [TestMethod]
    public void Ctor_Throw_EndsTheSession_AndRethrowsTheSameException()
    {
        Patch(nameof(Ctor), typeof(PartyVM_Ctor_ShadowfaxParty_Patch), true, false, true);
        _throw = true;

        var thrown = Assert.ThrowsException<InvalidOperationException>(Ctor);

        Assert.AreEqual("ctor failed", thrown.Message);
        Assert.IsFalse(_service.SessionOpen);
        StringAssert.Contains(_logger.Lines[0], ", threw InvalidOperationException");
    }

    [TestMethod]
    public void Ctor_StrippedPrefix_FinalizerWritesNothingAndKeepsTheSession()
    {
        Patch(nameof(Ctor), typeof(PartyVM_Ctor_ShadowfaxParty_Patch), false, false, true);
        _service.BeginScreen();

        Ctor();

        Assert.IsTrue(_service.SessionOpen);
        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void OnFinalize_EndsTheSession()
    {
        Patch(nameof(Window), typeof(PartyVM_OnFinalize_ShadowfaxParty_Patch), true, false, false);
        _service.BeginScreen();

        Window();

        Assert.IsFalse(_service.SessionOpen);
        var lines = new List<string>(_logger.Lines);
        Assert.AreEqual(0, lines.Count);
    }

    [TestMethod]
    public void InitializeTroopLists_TimesTheInitPhase_AndAThrowStillEndsIt()
    {
        Patch(nameof(InitializeTroopLists), typeof(PartyVM_InitializeTroopLists_ShadowfaxParty_Patch), true, false, true);
        _service.BeginScreen();
        Inside = () => _clock.Advance(30);

        InitializeTroopLists();
        Inside = () => { _clock.Advance(5); throw new InvalidOperationException("boom"); };
        Assert.ThrowsException<InvalidOperationException>(InitializeTroopLists);

        StringAssert.Contains(_service.BuildOpenDetail(), "init 35.0 ms");
    }

    [TestMethod]
    public void InitializeTroopLists_StrippedPrefix_FinalizerAddsNothing()
    {
        Patch(nameof(InitializeTroopLists), typeof(PartyVM_InitializeTroopLists_ShadowfaxParty_Patch), false, false, true);
        _service.BeginScreen();
        Inside = () => _clock.Advance(30);

        InitializeTroopLists();

        StringAssert.Contains(_service.BuildOpenDetail(), "init 0.0 ms");
    }

    [TestMethod]
    public void RefreshValues_TimesTheRefreshPhase_AndAThrowStillEndsIt()
    {
        Patch(nameof(RefreshValues), typeof(PartyVM_RefreshValues_ShadowfaxParty_Patch), true, false, true);
        _service.BeginScreen();
        Inside = () => _clock.Advance(12);

        RefreshValues();
        Inside = () => { _clock.Advance(3); throw new InvalidOperationException("boom"); };
        Assert.ThrowsException<InvalidOperationException>(RefreshValues);

        StringAssert.Contains(_service.BuildOpenDetail(), "refresh 15.0 ms");
    }

    [TestMethod]
    public void RefreshValues_StrippedPrefix_FinalizerAddsNothing()
    {
        Patch(nameof(RefreshValues), typeof(PartyVM_RefreshValues_ShadowfaxParty_Patch), false, false, true);
        _service.BeginScreen();
        Inside = () => _clock.Advance(12);

        RefreshValues();

        StringAssert.Contains(_service.BuildOpenDetail(), "refresh 0.0 ms");
    }

    [TestMethod]
    public void TransferAllTroops_WritesItsOwnLine_AndRethrowsAThrow()
    {
        Patch(nameof(TransferAllTroops), typeof(PartyVM_TransferAllTroops_ShadowfaxParty_Patch), true, false, true);

        TransferAllTroops(default);
        StringAssert.StartsWith(_logger.Lines[0], "INFO [ScreenOpen] Party transfer all: ");

        Inside = () => throw new InvalidOperationException("boom");
        Assert.ThrowsException<InvalidOperationException>(() => TransferAllTroops(default));
        StringAssert.Contains(_logger.Lines[1], ", threw InvalidOperationException");
    }

    [TestMethod]
    public void TransferAllTroops_StrippedPrefix_WritesNothing()
    {
        Patch(nameof(TransferAllTroops), typeof(PartyVM_TransferAllTroops_ShadowfaxParty_Patch), false, false, true);

        TransferAllTroops(default);

        Assert.AreEqual(0, _logger.Lines.Count);
    }
}
