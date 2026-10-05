using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SiegeForces;
using TAOM.Features.SiegeForces.Domain;
using static TAOM.Tests.Features.SiegeForces.FakeReadyListWindow;
using static TAOM.Tests.Features.SiegeForces.SiegeForcesFixtures;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// The siege troop picker's orchestration (the pure rules have their own tests): the gates that decide whether the
/// screen opens, the armed window around the vanilla mission open, the window filter, and the one-shot spawn-totals
/// fit. The adapters are NSubstitute fakes, so what is asserted is which seam was called, with what, and how often:
/// no-pending means no engine read (the totals prefix runs for every battle, Custom Battle included), a failure in the
/// plan means vanilla, and a failure while opening the mission closes the window and rethrows.
/// </summary>
[TestClass]
public class SiegeForcesServiceTests
{
    private ISiegeForcesAdapter _adapter = null!;
    private ISiegeForcesConfigProvider _config = null!;
    private ISiegeForcesSettingsProvider _settings = null!;
    private FakeRaceManager _races = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _server = null!;
    private IModLogger _logger = null!;
    private SiegeForcesService _sut = null!;

    private object _token = null!;
    private PickerRequest? _request;
    private Action<IReadOnlyDictionary<string, int>>? _onDone;

    [TestInitialize]
    public void Setup()
    {
        _token = new object();
        _adapter = Substitute.For<ISiegeForcesAdapter>();
        _config = Substitute.For<ISiegeForcesConfigProvider>();
        _settings = Substitute.For<ISiegeForcesSettingsProvider>();
        _races = FakeRaceManager.WithTrolls();
        _coop = Substitute.For<ICoopSessionProvider>();
        _server = Substitute.For<IDedicatedServerProvider>();
        _logger = Substitute.For<IModLogger>();

        _settings.PickerEnabled.Returns(true);
        _config.StartOversizedUnticked.Returns(true);
        _adapter.CaptureWallBattle().Returns(WorkedExample(_token));
        _adapter.ReadMapEventToken().Returns(_token);
        _adapter.IsSpawnTotalsFitAttached().Returns(true);
        _adapter.TryOpenPicker(Arg.Any<PickerRequest>(), Arg.Any<Action<IReadOnlyDictionary<string, int>>>())
            .Returns(call =>
            {
                _request = call.Arg<PickerRequest>();
                _onDone = call.Arg<Action<IReadOnlyDictionary<string, int>>>();
                return true;
            });

        _sut = new SiegeForcesService(_adapter, _config, _settings, _races, _coop, _server, _logger);
    }

    // The worked example's windows, as the engine's per-party model calls leave them: the main party first (13 entries),
    // then the vassal (23 entries) appended after what the main party kept, and an enemy garrison on the other side.
    private static FakeReadyListWindow MainWindow() => new("main", Attacker, new (string? Id, bool IsPlayer)[]
    {
        (Uruk, false), (Uruk, false), (Uruk, false), (Uruk, false), (Uruk, false),
        (Uruk, false), (Uruk, false), (Uruk, false), (Uruk, false), (Uruk, false),
        (HillTrollTroop, false), (HillTrollTroop, false), (Player, true),
    });

    private static FakeReadyListWindow VassalWindow(int otherEntries = 12) =>
        Of("vassalA", Attacker, otherEntries, (Uruk, 20), (CaveTrollTroop, 3));

    private static FakeReadyListWindow EnemyWindow() => Of("enemy_garrison", Defender, 0, ("enemy_soldier", 50));

    private void OfferAndPick(Dictionary<string, int> selection, Action duringOpen)
    {
        Assert.IsTrue(_sut.TryOfferPicker(duringOpen), "the picker was expected to open");
        _onDone!(selection);
    }

    private void FillWorkedExample(FakeReadyListWindow? main = null, FakeReadyListWindow? vassal = null)
    {
        _sut.FilterAppended(true, () => main ?? MainWindow());
        _sut.FilterAppended(true, () => vassal ?? VassalWindow());
        _sut.FilterAppended(true, EnemyWindow);
    }

    private static SpawnTotals Totals(int attacker = 36) => new(DefenderTotal: 60, AttackerTotal: attacker, DefenderInitial: 60, AttackerInitial: attacker);

    // --- TryOfferPicker: the gates ----------------------------------------------------------------------------

    [TestMethod]
    public void TryOfferPicker_EverythingAllows_OpensThePicker()
    {
        var opened = _sut.TryOfferPicker(() => { });

        Assert.IsTrue(opened);
        Assert.IsNotNull(_request);
        CollectionAssert.AreEqual(
            new[] { "main_hero:1:0:1", "uruk_hai:30:0:30", "hill_troll:2:0:0", "cave_troll:3:0:0" },
            _request!.Rows.Select(r => $"{r.CharacterId}:{r.Number}:{r.Wounded}:{r.Initial}").ToArray());
        Assert.AreEqual(36, _request.Max);
        Assert.AreEqual(1, _request.Min);
    }

    [TestMethod]
    public void TryOfferPicker_TheSpawnFitIsNotAttached_OffersNothing_AndWarnsOnce()
    {
        _adapter.IsSpawnTotalsFitAttached().Returns(false);

        Assert.IsFalse(_sut.TryOfferPicker(() => { }));
        Assert.IsFalse(_sut.TryOfferPicker(() => { }));

        _adapter.DidNotReceiveWithAnyArgs().CaptureWallBattle();
        _adapter.DidNotReceiveWithAnyArgs().TryOpenPicker(null!, null!);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("[SiegeForces]") && s.Contains("spawn totals")));
    }

    [TestMethod]
    public void TryOfferPicker_TheSpawnFitIsAttached_IsAskedOnce_ThenCached()
    {
        Assert.IsTrue(_sut.TryOfferPicker(() => { }));
        Assert.IsTrue(_sut.TryOfferPicker(() => { }));

        _adapter.Received(1).IsSpawnTotalsFitAttached();
    }

    [TestMethod]
    public void TryOfferPicker_TheSpawnFitCheckThrows_OffersNothing()
    {
        _adapter.IsSpawnTotalsFitAttached().Throws(new InvalidOperationException("harmony"));

        Assert.IsFalse(_sut.TryOfferPicker(() => { }));

        _adapter.DidNotReceiveWithAnyArgs().TryOpenPicker(null!, null!);
    }

    [TestMethod]
    public void TryOfferPicker_TheSettingIsOff_NeverAsksWhetherTheFitIsAttached()
    {
        _settings.PickerEnabled.Returns(false);

        _sut.TryOfferPicker(() => { });

        _adapter.DidNotReceive().IsSpawnTotalsFitAttached();
    }

    [TestMethod]
    public void TryOfferPicker_TheConfigSaysTicked_OffersTheTrollsTicked()
    {
        _config.StartOversizedUnticked.Returns(false);

        _sut.TryOfferPicker(() => { });

        Assert.AreEqual(2, _request!.Rows.Single(r => r.CharacterId == HillTrollTroop).Initial);
        Assert.AreEqual(3, _request.Rows.Single(r => r.CharacterId == CaveTrollTroop).Initial);
    }

    [TestMethod]
    public void TryOfferPicker_WithoutAnOpenAction_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => _sut.TryOfferPicker(null!));
    }

    [TestMethod]
    public void TryOfferPicker_TheSettingIsOff_DoesNothing_AndTouchesNoEngineSeam()
    {
        _settings.PickerEnabled.Returns(false);

        Assert.IsFalse(_sut.TryOfferPicker(() => Assert.Fail("vanilla opens the mission, not the service")));

        _adapter.DidNotReceiveWithAnyArgs().CaptureWallBattle();
        _adapter.DidNotReceiveWithAnyArgs().TryOpenPicker(default!, default!);
    }

    [TestMethod]
    public void TryOfferPicker_ACoopSessionIsLive_DoesNothing()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.IsFalse(_sut.TryOfferPicker(() => { }));
        _adapter.DidNotReceiveWithAnyArgs().CaptureWallBattle();
    }

    [TestMethod]
    public void TryOfferPicker_TheHostDecidesForThisPeer_DoesNothing()
    {
        // ShouldDeferToHost is also true for a co-op mod TAOM cannot probe, where the only safe answer is to yield.
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_sut.TryOfferPicker(() => { }));
        _adapter.DidNotReceiveWithAnyArgs().CaptureWallBattle();
    }

    [TestMethod]
    public void TryOfferPicker_ADedicatedServer_DoesNothing()
    {
        _server.IsDedicatedServer.Returns(true);

        Assert.IsFalse(_sut.TryOfferPicker(() => { }));
        _adapter.DidNotReceiveWithAnyArgs().CaptureWallBattle();
    }

    [TestMethod]
    public void TryOfferPicker_TheCoopStateIsReadLive_NeverCached()
    {
        // A player can host, disconnect and load a solo save in one process: a value cached at the first assault is
        // wrong for the rest of the session (ICoopSessionProvider: read these live at the point of use).
        _coop.IsSessionActive.Returns(true);
        Assert.IsFalse(_sut.TryOfferPicker(() => { }));

        _coop.IsSessionActive.Returns(false);
        Assert.IsTrue(_sut.TryOfferPicker(() => { }));
    }

    [TestMethod]
    public void TryOfferPicker_TheSettingIsReadLive_NeverCached()
    {
        _settings.PickerEnabled.Returns(false);
        Assert.IsFalse(_sut.TryOfferPicker(() => { }));

        _settings.PickerEnabled.Returns(true);
        Assert.IsTrue(_sut.TryOfferPicker(() => { }));
    }

    [TestMethod]
    public void TryOfferPicker_NotAWallBattle_DoesNothing()
    {
        _adapter.CaptureWallBattle().Returns((SiegeForcesSnapshot?)null);

        Assert.IsFalse(_sut.TryOfferPicker(() => { }));
        _adapter.DidNotReceiveWithAnyArgs().TryOpenPicker(default!, default!);
    }

    [TestMethod]
    public void TryOfferPicker_NothingToChoose_DoesNothing()
    {
        _adapter.CaptureWallBattle().Returns(Snapshot(new[]
        {
            Party("main", new[] { Troop(Player, 1, isPlayer: true) }, main: true),
        }, token: _token));

        Assert.IsFalse(_sut.TryOfferPicker(() => { }));
        _adapter.DidNotReceiveWithAnyArgs().TryOpenPicker(default!, default!);
    }

    [TestMethod]
    public void TryOfferPicker_ThePickerDidNotOpen_ReturnsFalse()
    {
        _adapter.TryOpenPicker(Arg.Any<PickerRequest>(), Arg.Any<Action<IReadOnlyDictionary<string, int>>>()).Returns(false);

        Assert.IsFalse(_sut.TryOfferPicker(() => { }));
    }

    [TestMethod]
    public void TryOfferPicker_TheCaptureThrows_ReturnsFalse_AndWarns()
    {
        _adapter.CaptureWallBattle().Throws(new InvalidOperationException("no campaign"));

        Assert.IsFalse(_sut.TryOfferPicker(() => { }));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("no campaign")));
    }

    [TestMethod]
    public void TryOfferPicker_OpeningThePickerThrows_ReturnsFalse_AndWarns()
    {
        _adapter.TryOpenPicker(Arg.Any<PickerRequest>(), Arg.Any<Action<IReadOnlyDictionary<string, int>>>())
            .Throws(new InvalidOperationException("view layer"));

        Assert.IsFalse(_sut.TryOfferPicker(() => { }));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("view layer")));
    }

    [TestMethod]
    public void TryOfferPicker_AGateThrows_ReturnsFalse_AndWarns()
    {
        _coop.IsSessionActive.Returns(_ => throw new InvalidOperationException("probe broke"));

        Assert.IsFalse(_sut.TryOfferPicker(() => { }));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("probe broke")));
    }

    [TestMethod]
    public void TryOfferPicker_AnOffer_LogsOneLine()
    {
        _sut.TryOfferPicker(() => { });

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("[SiegeForces]") && s.Contains("offering the picker")));
    }

    [TestMethod]
    public void TryOfferPicker_TheLoggerFaultsAfterTheScreenIsUp_IsNotReportedAsNotOpened()
    {
        // Logging happens before the screen opens and nothing runs after it, so a logger fault can never turn an opened
        // screen into "false" (which would let vanilla open the mission underneath the picker). A logger that throws
        // keeps the screen from opening at all, and the answer is then the honest false.
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log sink down"));

        Assert.IsFalse(_sut.TryOfferPicker(() => { }));

        _adapter.DidNotReceiveWithAnyArgs().TryOpenPicker(default!, default!);
    }

    [TestMethod]
    public void TryOfferPicker_OpeningTheScreenArmsNothing()
    {
        // Cancel has no callback: an offer alone must leave no window open and no fit pending.
        _sut.TryOfferPicker(() => { });

        var factoryCalls = 0;
        _sut.FilterAppended(true, () => { factoryCalls++; return MainWindow(); });
        Assert.AreEqual(0, factoryCalls);
        Assert.IsFalse(_sut.HasPending);
    }

    // --- A fit an earlier assault left pending ----------------------------------------------------------------

    /// <summary>Every way <see cref="SiegeForcesService.TryOfferPicker"/> can end, one per return path.</summary>
    public enum OfferEnds
    {
        TheSettingIsOff,
        ACoopSessionIsLive,
        NotAWallBattle,
        NothingToChoose,
        ThePickerDidNotOpen,
        AGateThrows,
        ThePickerOpensAndNobodyPressesDone,
    }

    private void ArrangeTheOfferToEnd(OfferEnds ending)
    {
        switch (ending)
        {
            case OfferEnds.TheSettingIsOff:
                _settings.PickerEnabled.Returns(false);
                break;
            case OfferEnds.ACoopSessionIsLive:
                _coop.IsSessionActive.Returns(true);
                break;
            case OfferEnds.NotAWallBattle:
                _adapter.CaptureWallBattle().Returns((SiegeForcesSnapshot?)null);
                break;
            case OfferEnds.NothingToChoose:
                _adapter.CaptureWallBattle().Returns(Snapshot(new[]
                {
                    Party("main", new[] { Troop(Player, 1, isPlayer: true) }, main: true),
                }, token: _token));
                break;
            case OfferEnds.ThePickerDidNotOpen:
                _adapter.TryOpenPicker(Arg.Any<PickerRequest>(), Arg.Any<Action<IReadOnlyDictionary<string, int>>>()).Returns(false);
                break;
            case OfferEnds.AGateThrows:
                _coop.IsSessionActive.Returns(_ => throw new InvalidOperationException("probe broke"));
                break;
            case OfferEnds.ThePickerOpensAndNobodyPressesDone:
                break;   // Setup already opens the picker, and Cancel has no callback
            default:
                Assert.Fail($"no arrangement for {ending}");
                break;
        }
    }

    [DataTestMethod]
    [DataRow(OfferEnds.TheSettingIsOff)]
    [DataRow(OfferEnds.ACoopSessionIsLive)]
    [DataRow(OfferEnds.NotAWallBattle)]
    [DataRow(OfferEnds.NothingToChoose)]
    [DataRow(OfferEnds.ThePickerDidNotOpen)]
    [DataRow(OfferEnds.AGateThrows)]
    [DataRow(OfferEnds.ThePickerOpensAndNobodyPressesDone)]
    public void TryOfferPicker_AFitAnEarlierAssaultLeftPending_IsDropped_HoweverTheOfferEnds(OfferEnds ending)
    {
        // An earlier assault's mission opened but never reached the siege spawn handler's AfterStart, so no battle
        // consumed its fit. A siege that continues keeps the same map event (v1.5.3 PlayerEncounter.ContinueBattle,
        // :1221-1235, works on the existing _mapEvent), so the record's token would still match: the totals prefix would shrink the next battle's player
        // side for troops nobody left out. Every assault passes the offer before vanilla opens its mission, and the
        // offer has seven ways to end, so each return path has its own row.
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());
        Assert.IsTrue(_sut.HasPending, "arrange: the earlier assault left a fit pending");
        ArrangeTheOfferToEnd(ending);

        _sut.TryOfferPicker(() => { });

        Assert.IsFalse(_sut.HasPending);
        Assert.IsNull(_sut.FitSpawnTotals(true, Totals()), "the next battle of the same map event keeps vanilla's totals");
    }

    // --- Done: arming around the vanilla mission open ---------------------------------------------------------

    [TestMethod]
    public void Done_OpensTheVanillaMissionInsideTheArmedWindow()
    {
        var window = VassalWindow();

        OfferAndPick(WorkedSelection(), () => _sut.FilterAppended(true, () => window));

        Assert.AreEqual(8, window.RemovedIndices.Count, "the window filter ran, so the plan was armed while the mission opened");
    }

    [TestMethod]
    public void Done_OpensTheMissionExactlyOnce()
    {
        var opens = 0;

        OfferAndPick(WorkedSelection(), () => opens++);

        Assert.AreEqual(1, opens);
    }

    [TestMethod]
    public void Done_CalledTwice_OpensTheMissionOnce()
    {
        // A double-clicked Done must not open two missions.
        var opens = 0;
        OfferAndPick(WorkedSelection(), () => opens++);

        _onDone!(WorkedSelection());

        Assert.AreEqual(1, opens);
    }

    [TestMethod]
    public void Done_WhenTheMissionHasOpened_TheWindowIsClosed()
    {
        OfferAndPick(WorkedSelection(), () => { });

        var factoryCalls = 0;
        _sut.FilterAppended(true, () => { factoryCalls++; return VassalWindow(); });

        Assert.AreEqual(0, factoryCalls, "a model call after the mission opened must never be filtered");
    }

    [TestMethod]
    public void Done_ThePlanCannotBeBuilt_OpensTheVanillaMissionWithoutAPlan()
    {
        var opens = 0;
        var filteredInside = 0;
        Assert.IsTrue(_sut.TryOfferPicker(() =>
        {
            opens++;
            _sut.FilterAppended(true, () => { filteredInside++; return VassalWindow(); });
        }));

        _onDone!(null!);

        Assert.AreEqual(1, opens, "a plan fault falls back to the vanilla mission");
        Assert.AreEqual(0, filteredInside, "and arms nothing");
        Assert.IsFalse(_sut.HasPending);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("plan") && s.Contains("vanilla")));
    }

    [TestMethod]
    public void Done_ThePlanCannotBeBuilt_ButTheMissionFailsToOpen_TheFaultStillReachesTheCaller()
    {
        var boom = new InvalidOperationException("mission refused");
        Assert.IsTrue(_sut.TryOfferPicker(() => throw boom));

        var thrown = Assert.ThrowsException<InvalidOperationException>(() => _onDone!(null!));

        Assert.AreSame(boom, thrown);
    }

    [TestMethod]
    public void Done_TheMissionOpenThrows_ClosesTheWindow_AndRethrowsTheSameException()
    {
        var boom = new InvalidOperationException("mission refused");
        Assert.IsTrue(_sut.TryOfferPicker(() =>
        {
            _sut.FilterAppended(true, () => VassalWindow());
            throw boom;
        }));

        var thrown = Assert.ThrowsException<InvalidOperationException>(() => _onDone!(WorkedSelection()));

        Assert.AreSame(boom, thrown, "the fault is rethrown unchanged, as vanilla would have thrown it");
        var factoryCalls = 0;
        _sut.FilterAppended(true, () => { factoryCalls++; return VassalWindow(); });
        Assert.AreEqual(0, factoryCalls, "the window is closed in a finally");
    }

    [TestMethod]
    public void Done_TheMissionOpenThrows_AfterSomethingWasDropped_StillRecordsThePendingFit()
    {
        // The throw may come after the spawn logic's own call (a later behaviour), so the record is kept whenever
        // something was dropped; the token guard and the next offer's reset neutralise a stale one.
        Assert.IsTrue(_sut.TryOfferPicker(() =>
        {
            _sut.FilterAppended(true, () => VassalWindow());
            throw new InvalidOperationException("mission refused");
        }));

        Assert.ThrowsException<InvalidOperationException>(() => _onDone!(WorkedSelection()));

        Assert.IsTrue(_sut.HasPending);
    }

    [TestMethod]
    public void Done_TheMissionOpenThrows_AfterNothingWasDropped_LeavesNoPendingFit()
    {
        Assert.IsTrue(_sut.TryOfferPicker(() => throw new InvalidOperationException("mission refused")));

        Assert.ThrowsException<InvalidOperationException>(() => _onDone!(WorkedSelection()));

        Assert.IsFalse(_sut.HasPending);
    }

    [TestMethod]
    public void Done_TheMissionOpenThrows_ThePendingFitIsStillDroppedByTheNextOffer()
    {
        Assert.IsTrue(_sut.TryOfferPicker(() =>
        {
            _sut.FilterAppended(true, () => VassalWindow());
            throw new InvalidOperationException("mission refused");
        }));
        Assert.ThrowsException<InvalidOperationException>(() => _onDone!(WorkedSelection()));

        _adapter.CaptureWallBattle().Returns((SiegeForcesSnapshot?)null);
        _sut.TryOfferPicker(() => { });

        Assert.IsFalse(_sut.HasPending);
    }

    [TestMethod]
    public void Done_TheLoggerThrowsWhileTheWindowCloses_StillClosesIt_AndNeverThrows()
    {
        // The close runs in a finally: a fault in it would replace whatever is in flight. Here the model never ran, so
        // the close warns about the model slot, and the log sink is down. Both the warning and the report of its failure throw.
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log sink down"));
        Assert.IsTrue(_sut.TryOfferPicker(() => { }));

        _onDone!(WorkedSelection());

        var factoryCalls = 0;
        _sut.FilterAppended(true, () => { factoryCalls++; return VassalWindow(); });
        Assert.AreEqual(0, factoryCalls, "the window must be closed even though the close failed");
        Assert.IsFalse(_sut.HasPending);
    }

    [TestMethod]
    public void Done_SomethingWasLeftOut_LeavesAPendingFit()
    {
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());

        Assert.IsTrue(_sut.HasPending);
    }

    [TestMethod]
    public void Done_NobodyWasLeftOut_LeavesNoPendingFit()
    {
        OfferAndPick(EveryoneSelected(), () => FillWorkedExample());

        Assert.IsFalse(_sut.HasPending);
    }

    [TestMethod]
    public void Done_TheSettingSwitchedOffWhileThePickerWasOpen_StillAppliesTheSelection()
    {
        // The toggle gates the offer, never a state transition (harmony-patches.md "Latches & Toggle Gates").
        Assert.IsTrue(_sut.TryOfferPicker(() => FillWorkedExample()));
        _settings.PickerEnabled.Returns(false);

        _onDone!(WorkedSelection());

        Assert.IsTrue(_sut.HasPending);
    }

    [TestMethod]
    public void Done_NoModelCallReachedAPlanParty_WarnsOnce_AndLeavesNoPendingFit()
    {
        // Another mod owns the TroopSupplierProbabilityModel slot: nothing was filtered, so every troop will fight.
        OfferAndPick(WorkedSelection(), () => { });

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("model slot")));
        Assert.IsFalse(_sut.HasPending);
    }

    [TestMethod]
    public void Done_TheModelRanForAPlanParty_DoesNotWarnAboutTheSlot()
    {
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());

        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("model slot")));
    }

    [TestMethod]
    public void Done_OnlyANonPlanPartyWasFiltered_StillWarnsAboutTheSlot()
    {
        OfferAndPick(WorkedSelection(), () => _sut.FilterAppended(true, EnemyWindow));

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("model slot")));
    }

    [TestMethod]
    public void Done_ASecondOffer_StartsFromFreshCounters()
    {
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());   // left out: 1 from the main party, 8 from the vassal
        _sut.FitSpawnTotals(true, Totals());

        // The second assault is the main party alone. A party that only the first battle had (the vassal, 8 dropped)
        // must not leave its count behind to be summed into this one.
        _adapter.CaptureWallBattle().Returns(Snapshot(new[] { MainParty() }, token: _token));
        OfferAndPick(new Dictionary<string, int> { [Player] = 1, [Uruk] = 10, [HillTrollTroop] = 1, [CaveTrollTroop] = 0 },
            () => _sut.FilterAppended(true, MainWindow));
        var fitted = _sut.FitSpawnTotals(true, Totals(attacker: 13));

        Assert.AreEqual(12, fitted!.Value.AttackerTotal, "13 men minus the one hill troll left out this time, not minus 9 more");
    }

    [TestMethod]
    public void Done_ASecondOffer_WarnsAgainWhenTheModelDoesNotRunThisTime()
    {
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());
        _logger.ClearReceivedCalls();

        // The first battle's filtered parties must not count for the second: nothing ran for it.
        OfferAndPick(WorkedSelection(), () => { });

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("model slot")));
    }

    // --- FilterAppended -----------------------------------------------------------------------------------------

    [TestMethod]
    public void FilterAppended_NotArmed_NeverBuildsTheWindow()
    {
        var factoryCalls = 0;

        _sut.FilterAppended(true, () => { factoryCalls++; return MainWindow(); });

        Assert.AreEqual(0, factoryCalls);
    }

    [TestMethod]
    public void FilterAppended_ASimulation_NeverBuildsTheWindow()
    {
        // MapEventSide.MakeReadyForSimulation passes includePlayer false: nothing is ever dropped from a simulation.
        var factoryCalls = 0;

        OfferAndPick(WorkedSelection(), () => _sut.FilterAppended(false, () => { factoryCalls++; return MainWindow(); }));

        Assert.AreEqual(0, factoryCalls);
    }

    [TestMethod]
    public void FilterAppended_APartyNotInThePlan_RemovesNothing()
    {
        var enemy = EnemyWindow();

        OfferAndPick(WorkedSelection(), () => _sut.FilterAppended(true, () => enemy));

        Assert.AreEqual(0, enemy.RemovedIndices.Count);
        Assert.AreEqual(50, enemy.Count);
    }

    [TestMethod]
    public void FilterAppended_APlanParty_RemovesTheLeftOutEntries_InDescendingIndexOrder()
    {
        var vassal = VassalWindow();

        OfferAndPick(WorkedSelection(), () => _sut.FilterAppended(true, () => vassal));

        // 15 of 20 Uruk-hai kept (the last 15, indices 5-19: the engine's window runs lowest priority first); all 3 cave
        // trolls (20-22) and Uruk-hai 0-4 go.
        CollectionAssert.AreEqual(new[] { 22, 21, 20, 4, 3, 2, 1, 0 }, vassal.RemovedIndices,
            "ascending removal would shift the entries after each one and remove the wrong troops");
        Assert.AreEqual(15, vassal.RemainingIds.Count(id => id == Uruk));
        Assert.AreEqual(0, vassal.RemainingIds.Count(id => id == CaveTrollTroop));
    }

    [TestMethod]
    public void FilterAppended_TheMainParty_KeepsTheLastHillTroll_AndDropsTheFirst()
    {
        var main = MainWindow();

        OfferAndPick(WorkedSelection(), () => _sut.FilterAppended(true, () => main));

        CollectionAssert.AreEqual(new[] { 10 }, main.RemovedIndices, "the window runs lowest priority first, so the last of a kind is kept");
        Assert.AreEqual(1, main.RemainingIds.Count(id => id == HillTrollTroop));
        Assert.AreEqual(10, main.RemainingIds.Count(id => id == Uruk));
    }

    [TestMethod]
    public void FilterAppended_NeverRemovesThePlayer_EvenWhenTheSelectionAsksForZero()
    {
        var main = MainWindow();
        var selection = WorkedSelection();
        selection[Player] = 0;

        OfferAndPick(selection, () => _sut.FilterAppended(true, () => main));

        CollectionAssert.Contains(main.RemainingIds.ToList(), Player);
        CollectionAssert.DoesNotContain(main.RemovedIndices, 12);
    }

    [TestMethod]
    public void FilterAppended_AnEntryWithNoId_IsKept()
    {
        var window = new FakeReadyListWindow("vassalA", Attacker, new (string? Id, bool IsPlayer)[] { (null, false), (Uruk, false) });
        var selection = WorkedSelection();
        selection[Uruk] = 0;

        OfferAndPick(selection, () => _sut.FilterAppended(true, () => window));

        CollectionAssert.AreEqual(new[] { 1 }, window.RemovedIndices);
        CollectionAssert.AreEqual(new string?[] { null }, window.RemainingIds.ToArray());
    }

    [TestMethod]
    public void FilterAppended_AFilterThatRemovesNothing_ReportsNothing()
    {
        var main = MainWindow();

        OfferAndPick(EveryoneSelected(), () => _sut.FilterAppended(true, () => main));

        Assert.AreEqual(0, main.RemovedIndices.Count);
    }

    [TestMethod]
    public void FilterAppended_TheSamePartyTwice_OverwritesItsDropCount_NeverSumsIt()
    {
        // A second MakeReady rebuilds the list and calls the model again for the same party: the drop count is the
        // last call's, or the total would shrink twice.
        OfferAndPick(WorkedSelection(), () =>
        {
            _sut.FilterAppended(true, VassalWindowFactory);
            _sut.FilterAppended(true, VassalWindowFactory);
        });

        var fitted = _sut.FitSpawnTotals(true, Totals(attacker: 23));

        Assert.AreEqual(15, fitted!.Value.AttackerTotal, "23 - 8 dropped once, not 23 - 16");
    }

    private static FakeReadyListWindow VassalWindowFactory() => VassalWindow(otherEntries: 0);

    [TestMethod]
    public void FilterAppended_RemovalThrowsMidway_StillRecordsThePlannedDropCount_TheSafeDirection()
    {
        // The count is written before the removal, so a throw can only shrink the total by too much, never too little:
        // fewer troops spawn than the list could supply, and the battle still completes.
        var vassal = VassalWindow(otherEntries: 0);
        vassal.ThrowOnRemoval = 3;

        OfferAndPick(WorkedSelection(), () => _sut.FilterAppended(true, () => vassal));

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("the engine's list refused a removal")));
        var fitted = _sut.FitSpawnTotals(true, Totals(attacker: 23));
        Assert.AreEqual(15, fitted!.Value.AttackerTotal, "all 8 planned drops were recorded although only 2 were removed");
    }

    [TestMethod]
    public void FilterAppended_TheFactoryThrows_FiltersNothing_RecordsNothing_AndWarns()
    {
        // An adapter fault means no filtering and no record, so the battle runs vanilla.
        OfferAndPick(WorkedSelection(), () => _sut.FilterAppended(true, () => throw new InvalidOperationException("window broke")));

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("window broke")));
        Assert.IsFalse(_sut.HasPending);
    }

    [TestMethod]
    public void FilterAppended_TheFactoryReturnsNull_DoesNothing()
    {
        OfferAndPick(WorkedSelection(), () => _sut.FilterAppended(true, () => null!));

        Assert.IsFalse(_sut.HasPending);
    }

    [TestMethod]
    public void FilterAppended_TheSideListSize_IsTheLastWritePerSide_SoThePlayersSideClampsTheTotal()
    {
        // H1: the player's side ends with 20 entries in its ready list although 36 - 9 = 27 were expected (a hero
        // healed inside one continued map event is counted but not listed). The fit clamps to the list.
        var main = MainWindow();
        var vassal = VassalWindow(otherEntries: 5);   // the main party left 12 in the real flow; here the list ends at 5 + 15 = 20
        OfferAndPick(WorkedSelection(), () =>
        {
            _sut.FilterAppended(true, () => main);
            _sut.FilterAppended(true, () => vassal);
        });

        var fitted = _sut.FitSpawnTotals(true, Totals());

        Assert.AreEqual(20, fitted!.Value.AttackerTotal);
        Assert.AreEqual(20, fitted.Value.AttackerInitial);
    }

    [TestMethod]
    public void FilterAppended_AnAlliedPartyAppendedAfterThePlanParties_CountsInTheSideListSize()
    {
        // The side's list is shared: an ally outside the plan appends after the plan parties, so the final size is
        // read after it, not after the last plan party.
        var ally = Of("ally", Attacker, otherEntries: 27, ("ally_knight", 4));
        OfferAndPick(WorkedSelection(), () =>
        {
            FillWorkedExample();
            _sut.FilterAppended(true, () => ally);
        });

        var fitted = _sut.FitSpawnTotals(true, Totals(attacker: 40));

        Assert.AreEqual(31, fitted!.Value.AttackerTotal, "40 - 9 = 31 and the list holds 27 + 4 = 31: no clamp bites");
    }

    [TestMethod]
    public void FilterAppended_TheOtherSidesList_NeverWritesThePlayersSideCount()
    {
        // The enemy garrison's list is the LAST write and ends at 5; a count shared by both sides would clamp the
        // player's side to it and spawn five men.
        OfferAndPick(WorkedSelection(), () =>
        {
            FillWorkedExample();
            _sut.FilterAppended(true, () => Of("enemy_late", Defender, 0, ("enemy_soldier", 5)));
        });

        var fitted = _sut.FitSpawnTotals(true, Totals());

        Assert.AreEqual(27, fitted!.Value.AttackerTotal);
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(2)]
    [DataRow(99)]
    public void FilterAppended_ASideThatIsNeitherDefenderNorAttacker_IsIgnoredForTheCount(int side)
    {
        OfferAndPick(WorkedSelection(), () =>
        {
            FillWorkedExample();
            _sut.FilterAppended(true, () => Of("odd", side, 0, ("x", 3)));
        });

        var fitted = _sut.FitSpawnTotals(true, Totals());

        Assert.AreEqual(27, fitted!.Value.AttackerTotal);
    }

    [TestMethod]
    public void FilterAppended_ForADefence_TheDefenderSidesListIsTheOneThatCounts()
    {
        _adapter.CaptureWallBattle().Returns(Snapshot(new[] { MainParty(), VassalA() },
            playerIsAttacker: false, leadsArmy: true, token: _token));
        var main = new FakeReadyListWindow("main", Defender, MainWindowEntries());
        var vassal = Of("vassalA", Defender, 3, (Uruk, 20), (CaveTrollTroop, 3));   // the defender list ends at 3 + 15 = 18
        OfferAndPick(WorkedSelection(), () =>
        {
            _sut.FilterAppended(true, () => main);
            _sut.FilterAppended(true, () => vassal);
            _sut.FilterAppended(true, () => Of("besieger", Attacker, 0, ("enemy_soldier", 7)));
        });

        var fitted = _sut.FitSpawnTotals(false, new SpawnTotals(DefenderTotal: 36, AttackerTotal: 60, DefenderInitial: 36, AttackerInitial: 60));

        Assert.AreEqual(18, fitted!.Value.DefenderTotal, "clamped to the defenders' list, not the besiegers' 7 and not 36 - 9");
        Assert.AreEqual(18, fitted.Value.DefenderInitial);
        Assert.AreEqual(60, fitted.Value.AttackerTotal);
        Assert.AreEqual(60, fitted.Value.AttackerInitial);
    }

    private static IEnumerable<(string? Id, bool IsPlayer)> MainWindowEntries() =>
        Enumerable.Repeat(((string?)Uruk, false), 10)
            .Concat(Enumerable.Repeat(((string?)HillTrollTroop, false), 2))
            .Append(((string?)Player, true));

    // --- FitSpawnTotals -----------------------------------------------------------------------------------------

    [TestMethod]
    public void HasPending_BeforeAnyOffer_IsFalse()
    {
        Assert.IsFalse(_sut.HasPending);
    }

    [TestMethod]
    public void FitSpawnTotals_NothingPending_ReturnsNull_AndNeverReadsTheEngine()
    {
        // R1: the prefix runs for every battle, Custom Battle included, where MapEvent.PlayerMapEvent throws.
        var fitted = _sut.FitSpawnTotals(true, Totals());

        Assert.IsNull(fitted);
        _adapter.DidNotReceiveWithAnyArgs().ReadMapEventToken();
    }

    [TestMethod]
    public void FitSpawnTotals_AnAssault_SubtractsTheLeftOutTroopsFromTheAttackersOnly()
    {
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());

        var fitted = _sut.FitSpawnTotals(true, Totals());

        Assert.AreEqual(new SpawnTotals(60, 27, 60, 27), fitted);
    }

    [TestMethod]
    public void FitSpawnTotals_ADefence_SubtractsTheLeftOutTroopsFromTheDefendersOnly()
    {
        _adapter.CaptureWallBattle().Returns(Snapshot(new[] { MainParty(), VassalA() },
            playerIsAttacker: false, leadsArmy: true, token: _token));
        OfferAndPick(WorkedSelection(), () =>
        {
            _sut.FilterAppended(true, () => new FakeReadyListWindow("main", Defender, MainWindowEntries()));
            _sut.FilterAppended(true, () => Of("vassalA", Defender, 12, (Uruk, 20), (CaveTrollTroop, 3)));
        });

        var fitted = _sut.FitSpawnTotals(false, new SpawnTotals(DefenderTotal: 36, AttackerTotal: 60, DefenderInitial: 36, AttackerInitial: 60));

        Assert.AreEqual(new SpawnTotals(27, 60, 27, 60), fitted);
    }

    [TestMethod]
    public void FitSpawnTotals_ConsumesTheRecord_SoOnlyTheNextBattleIsFitted()
    {
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());

        Assert.IsNotNull(_sut.FitSpawnTotals(true, Totals()));
        Assert.IsFalse(_sut.HasPending);
        Assert.IsNull(_sut.FitSpawnTotals(true, Totals()));
    }

    [TestMethod]
    public void FitSpawnTotals_ADifferentBattle_ChangesNothing_AndDiscardsTheRecord()
    {
        // The stale-record case: a siege mission that never reached AfterStart, then a Custom Siege or a field battle.
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());
        _adapter.ReadMapEventToken().Returns(new object());

        var fitted = _sut.FitSpawnTotals(true, Totals());

        Assert.IsNull(fitted);
        Assert.IsFalse(_sut.HasPending, "a record that matched no battle is dropped, so the next call reads no engine state");
    }

    [TestMethod]
    public void FitSpawnTotals_NoMapEvent_ChangesNothing_AndDiscardsTheRecord()
    {
        // Custom Battle: no campaign, so no map event. Null-safe, never a throw.
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());
        _adapter.ReadMapEventToken().Returns((object?)null);

        Assert.IsNull(_sut.FitSpawnTotals(true, Totals()));
        Assert.IsFalse(_sut.HasPending);
    }

    [TestMethod]
    public void FitSpawnTotals_TheOtherSide_ChangesNothing_AndDiscardsTheRecord()
    {
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());

        Assert.IsNull(_sut.FitSpawnTotals(playerIsAttacker: false, Totals()));
        Assert.IsFalse(_sut.HasPending);
    }

    [TestMethod]
    public void FitSpawnTotals_TheTokenReadThrows_ChangesNothing_AndDiscardsTheRecord()
    {
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());
        _adapter.ReadMapEventToken().Throws(new NullReferenceException("Campaign.Current"));

        var fitted = _sut.FitSpawnTotals(true, Totals());

        Assert.IsNull(fitted, "an exception leaves the four totals untouched");
        Assert.IsFalse(_sut.HasPending);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("Campaign.Current")));
    }

    [TestMethod]
    public void FitSpawnTotals_TheReadyListIsShorterThanTheArithmetic_ClampsTheTotalToIt()
    {
        var vassal = VassalWindow(otherEntries: 5);   // the list ends at 5 + 15 = 20
        OfferAndPick(WorkedSelection(), () =>
        {
            _sut.FilterAppended(true, () => MainWindow());
            _sut.FilterAppended(true, () => vassal);
        });

        var fitted = _sut.FitSpawnTotals(true, new SpawnTotals(60, 36, 60, 36));

        Assert.AreEqual(new SpawnTotals(60, 20, 60, 20), fitted);
    }

    [TestMethod]
    public void FitSpawnTotals_ThePlayerIsOffTheListAndNobodyElseStays_FitsTheSideToNobody()
    {
        // MapEventParty.Update leaves a hero who is still in _woundedInBattle off the list (v1.5.3 MapEventParty.cs
        // :279-285), so the involved-men count holds him and the list does not. With every other troop left out the list is
        // empty: an initial spawn of 1 waits for a reserved troop the supplier can never give (v1.5.3
        // DefaultBattleMissionAgentSpawnLogic.CheckDeployment :539-547), while a side whose initial spawn is 0 is skipped
        // (:535-538). The recorded size is 0, and 0 is a real answer.
        _adapter.CaptureWallBattle().Returns(Snapshot(new[]
        {
            Party("main", new[] { Troop(Player, 1, isPlayer: true), Troop(Uruk, 10) }, main: true),
        }, token: _token));
        var selection = new Dictionary<string, int> { [Player] = 1, [Uruk] = 0 };
        OfferAndPick(selection, () => _sut.FilterAppended(true, () => Of("main", Attacker, 0, (Uruk, 10))));

        var fitted = _sut.FitSpawnTotals(true, new SpawnTotals(DefenderTotal: 60, AttackerTotal: 11, DefenderInitial: 60, AttackerInitial: 11));

        Assert.AreEqual(new SpawnTotals(60, 0, 60, 0), fitted, "11 men counted, 10 left out, and nobody on the list to wait for");
    }

    [TestMethod]
    public void FitSpawnTotals_StillAppliesAfterTheSettingIsSwitchedOff()
    {
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());
        _settings.PickerEnabled.Returns(false);

        Assert.AreEqual(new SpawnTotals(60, 27, 60, 27), _sut.FitSpawnTotals(true, Totals()));
    }

    [TestMethod]
    public void FitSpawnTotals_AFit_LogsOneLineWithBothNumbers()
    {
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());

        _sut.FitSpawnTotals(true, Totals());

        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.Contains("[SiegeForces]") && s.Contains("attacker") && s.Contains("36") && s.Contains("27")));
    }

    [TestMethod]
    public void FitSpawnTotals_TheLoggerThrowsAfterTheFitIsComputed_StillReturnsTheFit()
    {
        OfferAndPick(WorkedSelection(), () => FillWorkedExample());
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("logger broke"));

        var fitted = _sut.FitSpawnTotals(true, Totals());

        Assert.AreEqual(new SpawnTotals(60, 27, 60, 27), fitted, "a logger fault must not discard the fit");
    }
}
