using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SiegeForces;
using TAOM.Features.SiegeForces.Domain;
using TAOM.Features.SiegeForces.Hooks;
using static TAOM.Tests.Features.SiegeForces.FakeReadyListWindow;
using static TAOM.Tests.Features.SiegeForces.SiegeForcesFixtures;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// The two prefixes' own bodies, run for real (no Harmony is applied in the test host: the prefixes are plain static
/// methods). The service each prefix resolves lazily is replaced through its private cache with one built over fake
/// adapters, so what is asserted is what the prefix does with the engine's arguments: the latch, the skip-vanilla result,
/// the guard order of the totals prefix, and that no fault ever escapes. <c>PlayerSiege.StartSiegeMission</c> is called
/// for real by the re-entry; with no campaign it throws a NullReferenceException, which is the mission-open fault the
/// re-entry must hand back unwrapped, with the latch cleared.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class Patch102PrefixTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

    private ISiegeForcesAdapter _adapter = null!;
    private ISiegeForcesSettingsProvider _settings = null!;
    private SiegeForcesService _service = null!;
    private object _token = null!;
    private Action<IReadOnlyDictionary<string, int>>? _onDone;

    [TestInitialize]
    public void Setup()
    {
        _token = new object();
        _adapter = Substitute.For<ISiegeForcesAdapter>();
        _settings = Substitute.For<ISiegeForcesSettingsProvider>();
        var config = Substitute.For<ISiegeForcesConfigProvider>();
        _settings.PickerEnabled.Returns(true);
        config.StartOversizedUnticked.Returns(true);
        _adapter.CaptureWallBattle().Returns(WorkedExample(_token));
        _adapter.ReadMapEventToken().Returns(_token);
        _adapter.IsSpawnTotalsFitAttached().Returns(true);
        _adapter.TryOpenPicker(Arg.Any<PickerRequest>(), Arg.Any<Action<IReadOnlyDictionary<string, int>>>())
            .Returns(call =>
            {
                _onDone = call.Arg<Action<IReadOnlyDictionary<string, int>>>();
                return true;
            });

        _service = new SiegeForcesService(_adapter, config, _settings, FakeRaceManager.WithTrolls(),
            Substitute.For<ICoopSessionProvider>(), Substitute.For<IDedicatedServerProvider>(), Substitute.For<IModLogger>());
        SetField(typeof(Patch102_StartSiegeMissionPicker), "_service", _service);
        SetField(typeof(Patch102_SpawnTotalsFit), "_service", _service);
        SetField(typeof(Patch102_StartSiegeMissionPicker), "_resuming", false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        SetField(typeof(Patch102_StartSiegeMissionPicker), "_service", null);
        SetField(typeof(Patch102_SpawnTotalsFit), "_service", null);
        SetField(typeof(Patch102_StartSiegeMissionPicker), "_resuming", false);
    }

    private static void SetField(Type type, string name, object? value) => type.GetField(name, Private)!.SetValue(null, value);

    private static object GetField(Type type, string name) => type.GetField(name, Private)!.GetValue(null)!;

    private static DefaultBattleMissionAgentSpawnLogic SpawnLogic(BattleSideEnum playerSide)
    {
        var logic = (DefaultBattleMissionAgentSpawnLogic)FormatterServices.GetUninitializedObject(typeof(DefaultBattleMissionAgentSpawnLogic));
        typeof(DefaultBattleMissionAgentSpawnLogic).GetProperty(nameof(DefaultBattleMissionAgentSpawnLogic.PlayerSide))!.SetValue(logic, playerSide);
        return logic;
    }

    // A pending fit for the worked example's assault: 9 of the attackers' 36 left out.
    private void ArmThePendingFit()
    {
        Assert.IsTrue(_service.TryOfferPicker(() =>
        {
            _service.FilterAppended(true, () => new FakeReadyListWindow("main", Attacker, MainEntries()));
            _service.FilterAppended(true, () => Of("vassalA", Attacker, 12, (Uruk, 20), (CaveTrollTroop, 3)));
        }));
        _onDone!(WorkedSelection());
        Assert.IsTrue(_service.HasPending);
    }

    private static IEnumerable<(string? Id, bool IsPlayer)> MainEntries()
    {
        for (var i = 0; i < 10; i++) yield return (Uruk, false);
        yield return (HillTrollTroop, false);
        yield return (HillTrollTroop, false);
        yield return (Player, true);
    }

    // --- the entry prefix -------------------------------------------------------------------------------------

    [TestMethod]
    public void EntryPrefix_WhileResuming_LetsVanillaRun_AndNeverTouchesTheService()
    {
        SetField(typeof(Patch102_StartSiegeMissionPicker), "_resuming", true);

        Assert.IsTrue(Patch102_StartSiegeMissionPicker.Prefix(null!));

        _adapter.DidNotReceiveWithAnyArgs().CaptureWallBattle();
    }

    [TestMethod]
    public void EntryPrefix_TheLatchIsPerThread_AnotherThreadStillOffersThePicker()
    {
        // The re-entry is synchronous on one thread; a flag shared by every thread would let a re-entry on one thread
        // silently skip the picker on another.
        SetField(typeof(Patch102_StartSiegeMissionPicker), "_resuming", true);
        bool? otherThreadResult = null;

        var thread = new System.Threading.Thread(() => otherThreadResult = Patch102_StartSiegeMissionPicker.Prefix(null!));
        thread.Start();
        thread.Join();

        Assert.AreEqual(false, otherThreadResult, "the other thread has its own latch, clear, so the picker opened and vanilla was skipped");
        Assert.IsTrue(Patch102_StartSiegeMissionPicker.Prefix(null!), "this thread's latch is still set");
    }

    [TestMethod]
    public void EntryPrefix_ThePickerOpens_SkipsVanilla()
    {
        Assert.IsFalse(Patch102_StartSiegeMissionPicker.Prefix(null!));
        Assert.IsNotNull(_onDone, "the picker should have been opened");
    }

    [TestMethod]
    public void EntryPrefix_NoPickerIsOffered_LetsVanillaRun()
    {
        _adapter.CaptureWallBattle().Returns((SiegeForcesSnapshot?)null);

        Assert.IsTrue(Patch102_StartSiegeMissionPicker.Prefix(null!));
    }

    [TestMethod]
    public void EntryPrefix_TheSettingIsOff_LetsVanillaRun()
    {
        _settings.PickerEnabled.Returns(false);

        Assert.IsTrue(Patch102_StartSiegeMissionPicker.Prefix(null!));
    }

    [TestMethod]
    public void EntryPrefix_TheServiceCannotBeResolved_LetsVanillaRun_AndNothingEscapes()
    {
        // No cached service and no configured container: resolving throws. Vanilla is the safe default HERE: it opens
        // the mission with every troop, which is exactly the unpatched game.
        SetField(typeof(Patch102_StartSiegeMissionPicker), "_service", null);

        Assert.IsTrue(Patch102_StartSiegeMissionPicker.Prefix(null!));
    }

    [TestMethod]
    public void Resume_AMissionOpenFault_ReachesTheCallerUnwrapped_WithItsStack_AndClearsTheLatch()
    {
        Assert.IsFalse(Patch102_StartSiegeMissionPicker.Prefix(null!));

        // The re-entry calls the real PlayerSiege.StartSiegeMission, which throws without a campaign.
        var thrown = Assert.ThrowsException<NullReferenceException>(() => _onDone!(WorkedSelection()));

        StringAssert.Contains(thrown.StackTrace, "PlayerSiege", "the throw site's frames survive the re-entry");
        Assert.AreEqual(false, GetField(typeof(Patch102_StartSiegeMissionPicker), "_resuming"), "the latch is cleared in a finally");
        Assert.IsFalse(_service.HasPending, "the window closed, and no mission opened");
    }

    [TestMethod]
    public void Resume_AfterAFault_ThePrefixOffersThePickerAgain()
    {
        Patch102_StartSiegeMissionPicker.Prefix(null!);
        Assert.ThrowsException<NullReferenceException>(() => _onDone!(WorkedSelection()));
        _onDone = null;

        Assert.IsFalse(Patch102_StartSiegeMissionPicker.Prefix(null!), "a stuck latch would skip the picker for the rest of the process");
        Assert.IsNotNull(_onDone);
    }

    // --- the totals prefix ------------------------------------------------------------------------------------

    [TestMethod]
    public void TotalsPrefix_NothingPending_LeavesTheIntsAlone_AndReadsNoEngineState()
    {
        int d = 60, a = 36, di = 60, ai = 36;

        Patch102_SpawnTotalsFit.Prefix(SpawnLogic(BattleSideEnum.Attacker), ref d, ref a, ref di, ref ai);

        CollectionAssert.AreEqual(new[] { 60, 36, 60, 36 }, new[] { d, a, di, ai });
        _adapter.DidNotReceiveWithAnyArgs().ReadMapEventToken();
    }

    [TestMethod]
    public void TotalsPrefix_ARecordForThisBattleAndSide_WritesTheFittedTotals()
    {
        ArmThePendingFit();
        int d = 60, a = 36, di = 60, ai = 36;

        Patch102_SpawnTotalsFit.Prefix(SpawnLogic(BattleSideEnum.Attacker), ref d, ref a, ref di, ref ai);

        CollectionAssert.AreEqual(new[] { 60, 27, 60, 27 }, new[] { d, a, di, ai },
            "defender total, attacker total, defender initial, attacker initial: only the attackers' pair shrinks");
    }

    [TestMethod]
    public void TotalsPrefix_TheOtherSide_LeavesTheIntsAlone()
    {
        ArmThePendingFit();
        int d = 60, a = 36, di = 60, ai = 36;

        Patch102_SpawnTotalsFit.Prefix(SpawnLogic(BattleSideEnum.Defender), ref d, ref a, ref di, ref ai);

        CollectionAssert.AreEqual(new[] { 60, 36, 60, 36 }, new[] { d, a, di, ai });
    }

    [TestMethod]
    public void TotalsPrefix_AnotherBattle_LeavesTheIntsAlone_AndDropsTheRecord()
    {
        ArmThePendingFit();
        _adapter.ReadMapEventToken().Returns(new object());
        int d = 60, a = 36, di = 60, ai = 36;

        Patch102_SpawnTotalsFit.Prefix(SpawnLogic(BattleSideEnum.Attacker), ref d, ref a, ref di, ref ai);

        CollectionAssert.AreEqual(new[] { 60, 36, 60, 36 }, new[] { d, a, di, ai });
        Assert.IsFalse(_service.HasPending);
    }

    [TestMethod]
    public void TotalsPrefix_TheTokenReadThrows_LeavesTheIntsAlone_AndNothingEscapes()
    {
        ArmThePendingFit();
        _adapter.ReadMapEventToken().Throws(new NullReferenceException("Campaign.Current"));
        int d = 60, a = 36, di = 60, ai = 36;

        Patch102_SpawnTotalsFit.Prefix(SpawnLogic(BattleSideEnum.Attacker), ref d, ref a, ref di, ref ai);

        CollectionAssert.AreEqual(new[] { 60, 36, 60, 36 }, new[] { d, a, di, ai });
    }

    [TestMethod]
    public void TotalsPrefix_AnInstanceThatIsNotTheSpawnLogic_LeavesTheIntsAlone_AndNothingEscapes()
    {
        ArmThePendingFit();
        int d = 60, a = 36, di = 60, ai = 36;

        Patch102_SpawnTotalsFit.Prefix("not a spawn logic", ref d, ref a, ref di, ref ai);

        CollectionAssert.AreEqual(new[] { 60, 36, 60, 36 }, new[] { d, a, di, ai });
    }

    [TestMethod]
    public void TotalsPrefix_TheServiceCannotBeResolved_LeavesTheIntsAlone_AndNothingEscapes()
    {
        SetField(typeof(Patch102_SpawnTotalsFit), "_service", null);
        int d = 60, a = 36, di = 60, ai = 36;

        Patch102_SpawnTotalsFit.Prefix(SpawnLogic(BattleSideEnum.Attacker), ref d, ref a, ref di, ref ai);

        CollectionAssert.AreEqual(new[] { 60, 36, 60, 36 }, new[] { d, a, di, ai });
    }
}
