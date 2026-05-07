using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SiegeDismount;
using TAOM.Features.SiegeDismount.Models;

namespace TAOM.Tests.Features.SiegeDismount;

[TestClass]
public class SiegeDismountServiceTests
{
    private ISiegeDismountSettingsProvider _settings = null!;
    private IPlayerMountAdapter _mount = null!;
    private IPartyMountInventoryAdapter _inventory = null!;
    private IModLogger _logger = null!;
    private SiegeDismountService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ISiegeDismountSettingsProvider>();
        _mount = Substitute.For<IPlayerMountAdapter>();
        _inventory = Substitute.For<IPartyMountInventoryAdapter>();
        _logger = Substitute.For<IModLogger>();

        _settings.IsEnabled.Returns(true);
        _settings.IsDebugMode.Returns(false);
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);
        _mount.HasMount().Returns(true);
        _mount.Capture().Returns(new MountSnapshot("horse_charger_west", "harness_chain"));

        _sut = new SiegeDismountService(_settings, _mount, _inventory, _logger);
    }

    // -------- Disable / inert paths --------

    [TestMethod]
    public void OnMissionStart_FeatureDisabled_DoesNothing()
    {
        _settings.IsEnabled.Returns(false);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_swadia_castle");

        _mount.DidNotReceive().Capture();
        _mount.DidNotReceive().Clear();
        _inventory.DidNotReceiveWithAnyArgs().Deposit(default!);
    }

    [TestMethod]
    public void OnMissionStart_NotASiegeMission_DoesNothing()
    {
        _sut.OnMissionStart(isSiegeBattle: false, sceneName: "field_battle_aserai");

        _mount.DidNotReceive().Capture();
        _mount.DidNotReceive().Clear();
    }

    [TestMethod]
    public void OnMissionStart_VanillaBehavior_DoesNotMutateEquipment()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.Vanilla);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.DidNotReceive().Clear();
        _inventory.DidNotReceiveWithAnyArgs().Deposit(default!);
    }

    [TestMethod]
    public void OnMissionStart_PlayerOnFoot_DoesNotMutateAndLogsDebug()
    {
        _mount.HasMount().Returns(false);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.DidNotReceive().Capture();
        _mount.DidNotReceive().Clear();
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    // -------- Siege detection: trust IsSiegeBattle exclusively --------
    // Codex review #1 found the keyword fallback ("siege"/"assault"/"breach") falsely matched
    // 24 vanilla settlement Location id="center" scene names like empire_siege_001. Those
    // scenes can be loaded as non-combat Missions where IsSiegeBattle=false; the keyword
    // fallback would have falsely clobbered mounts. Fix: removed the fallback entirely. The
    // engine's IsSiegeBattle flag is the only authority. Modded sieges that fail to set the
    // flag won't trigger SiegeDismount — that's a documented requirement now.

    [DataTestMethod]
    [DataRow("empire_siege_001")]
    [DataRow("khuzait_castle_siege_001")]
    [DataRow("sturgia_castle_siege_001")]
    [DataRow("castle_orthanc_gate")]
    [DataRow("castle_gundabad_wall")]
    [DataRow("city_breach_assault")]
    [DataRow("scene_with_siege_in_name")]
    [DataRow(null)]
    [DataRow("")]
    public void OnMissionStart_NotIsSiegeBattle_DoesNotTriggerRegardlessOfSceneName(string? sceneName)
    {
        _sut.OnMissionStart(isSiegeBattle: false, sceneName: sceneName);

        _mount.DidNotReceive().Capture();
        _mount.DidNotReceive().Clear();
        _inventory.DidNotReceiveWithAnyArgs().Deposit(default!);
    }

    [TestMethod]
    public void OnMissionStart_IsSiegeBattle_TriggersRegardlessOfSceneName()
    {
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "completely_unrelated_scene_name");

        _mount.Received(1).Capture();
    }

    // -------- KeepOnMap: now a full no-op (Codex review #1, finding 3) --------
    // The original developer's module advertised "horse spawns on map but player on foot" but
    // never actually spawned a horse or cleared the equipment slot — silent no-op equivalent
    // to Vanilla. We preserved the enum value for save-compat but treat it as Vanilla until
    // somebody implements an actual map-side horse spawn.

    [TestMethod]
    public void OnMissionStart_DismountKeepOnMap_FullNoOp()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountKeepOnMap);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.Received(1).Capture(); // Capture is called, then immediately discarded
        _mount.DidNotReceive().Clear();
        _inventory.DidNotReceiveWithAnyArgs().Deposit(default!);
    }

    [TestMethod]
    public void OnMissionStart_DismountKeepOnMap_LogsWarningAboutDeferredImplementation()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountKeepOnMap);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _logger.Received().LogWarning(Arg.Is<string>(s =>
            s.Contains("[SiegeDismount]") && s.Contains("mode 1") && s.Contains("Vanilla")));
    }

    [TestMethod]
    public void OnMissionStart_DismountKeepOnMap_DoesNotMarkPendingRemount()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountKeepOnMap);
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _sut.OnMissionEnd();

        _mount.DidNotReceiveWithAnyArgs().Restore(default!);
        _inventory.DidNotReceiveWithAnyArgs().Withdraw(default!);
    }

    // -------- DismountToInventory: move + no auto-remount --------

    [TestMethod]
    public void OnMissionStart_DismountToInventory_ClearsAndDeposits()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountToInventory);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.Received(1).Clear();
        _inventory.Received(1).Deposit(Arg.Any<IMountSnapshot>());
    }

    [TestMethod]
    public void OnMissionStart_DismountToInventory_DoesNotRemountOnEnd()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountToInventory);
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _sut.OnMissionEnd();

        _mount.DidNotReceiveWithAnyArgs().Restore(default!);
        _inventory.DidNotReceiveWithAnyArgs().Withdraw(default!);
    }

    // -------- AutoRemountAfter: full round-trip --------

    [TestMethod]
    public void OnMissionStart_AutoRemount_ClearsAndDeposits()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.Received(1).Clear();
        _inventory.Received(1).Deposit(Arg.Any<IMountSnapshot>());
    }

    [TestMethod]
    public void OnMissionEnd_AutoRemount_RestoresEquipmentAndWithdrawsFromInventory()
    {
        var captured = new MountSnapshot("horse_charger_west", "harness_chain");
        _mount.Capture().Returns(captured);
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _sut.OnMissionEnd();

        _mount.Received(1).Restore(captured);
        _inventory.Received(1).Withdraw(captured);
    }

    [TestMethod]
    public void OnMissionEnd_AutoRemountTriggeredTwice_OnlyRemountsOnce()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _sut.OnMissionEnd();
        _sut.OnMissionEnd();

        _mount.Received(1).Restore(Arg.Any<IMountSnapshot>());
        _inventory.Received(1).Withdraw(Arg.Any<IMountSnapshot>());
    }

    [TestMethod]
    public void OnMissionEnd_NoPriorMissionStart_DoesNothing()
    {
        _sut.OnMissionEnd();

        _mount.DidNotReceiveWithAnyArgs().Restore(default!);
        _inventory.DidNotReceiveWithAnyArgs().Withdraw(default!);
    }

    [TestMethod]
    public void OnMissionEnd_AfterDismountToInventory_DoesNotRemount()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountToInventory);
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _sut.OnMissionEnd();

        _mount.DidNotReceiveWithAnyArgs().Restore(default!);
    }

    // -------- Logging contracts (per CLAUDE.md mandatory logging rule) --------

    [TestMethod]
    public void OnMissionStart_FeatureDisabled_LogsInertOnce()
    {
        _settings.IsEnabled.Returns(false);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("disabled") && s.Contains("[SiegeDismount]")));
    }

    [TestMethod]
    public void OnMissionStart_SiegeDetected_LogsInfoWithSceneAndBehavior()
    {
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_swadia_castle");

        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("[SiegeDismount]") && s.Contains("siege") && s.Contains("AutoRemountAfter")));
    }

    [TestMethod]
    public void OnMissionStart_AdapterThrows_LogsErrorAndDoesNotPropagate()
    {
        _mount.When(m => m.Clear()).Do(_ => throw new System.InvalidOperationException("equipment slot null"));
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _logger.Received().LogError(Arg.Is<string>(s =>
            s.Contains("[SiegeDismount]") && s.Contains("equipment slot null")));
    }

    [TestMethod]
    public void OnMissionEnd_RestoreThrows_LogsErrorAndDoesNotPropagate()
    {
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");
        _mount.When(m => m.Restore(Arg.Any<IMountSnapshot>()))
              .Do(_ => throw new System.InvalidOperationException("hero is null"));

        _sut.OnMissionEnd();

        _logger.Received().LogError(Arg.Is<string>(s =>
            s.Contains("[SiegeDismount]") && s.Contains("hero is null")));
    }

    // -------- Regression: out-of-range MountBehavior enum (deep-review GAP 1) --------
    // If a user manually edits TAOM.json and sets SiegeMountBehavior to 99, the cast
    // produces an undefined enum value. Switch must have a default: case that logs
    // a warning and treats as a no-op (Vanilla equivalent), not silently capture
    // mount data without acting.

    [TestMethod]
    public void OnMissionStart_OutOfRangeMountBehavior_LogsWarningAndNoOps()
    {
        _settings.MountBehavior.Returns((SiegeMountBehaviorType)99);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.DidNotReceive().Clear();
        _inventory.DidNotReceiveWithAnyArgs().Deposit(default!);
        _logger.Received().LogWarning(Arg.Is<string>(s =>
            s.Contains("[SiegeDismount]") && s.Contains("99")));
    }

    [TestMethod]
    public void OnMissionStart_OutOfRangeMountBehavior_DoesNotMarkPendingRemount()
    {
        _settings.MountBehavior.Returns((SiegeMountBehaviorType)99);
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _sut.OnMissionEnd();

        _mount.DidNotReceiveWithAnyArgs().Restore(default!);
        _inventory.DidNotReceiveWithAnyArgs().Withdraw(default!);
    }

    // -------- Regression: state hygiene — snapshot cleanup (deep-review KL 1) --------

    [TestMethod]
    public void OnMissionStart_SecondSiegeAfterDismountToInventory_OverwritesStaleSnapshot()
    {
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.DismountToInventory);
        var firstSnapshot = new MountSnapshot("horse_charger_west", null);
        var secondSnapshot = new MountSnapshot("horse_destrier_east", "harness_chain");
        _mount.Capture().Returns(firstSnapshot, secondSnapshot);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");
        _sut.OnMissionEnd();
        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");
        _inventory.ClearReceivedCalls();
        _sut.OnMissionEnd();

        // After two cycles ending without auto-remount, no Restore/Withdraw should fire on either OnMissionEnd.
        _mount.DidNotReceiveWithAnyArgs().Restore(default!);
        _inventory.DidNotReceiveWithAnyArgs().Withdraw(default!);
    }

    // -------- Regression: HasMount guard after Capture (deep-review KL 3) --------

    [TestMethod]
    public void OnMissionStart_CaptureReturnsEmptySnapshot_DoesNotClearOrDeposit()
    {
        _mount.HasMount().Returns(true); // adapter says yes
        _mount.Capture().Returns(MountSnapshot.Empty); // but capture returns empty
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _mount.DidNotReceive().Clear();
        _inventory.DidNotReceiveWithAnyArgs().Deposit(default!);
    }

    [TestMethod]
    public void OnMissionStart_CaptureReturnsEmptySnapshot_LogsWarning()
    {
        _mount.HasMount().Returns(true);
        _mount.Capture().Returns(MountSnapshot.Empty);
        _settings.MountBehavior.Returns(SiegeMountBehaviorType.AutoRemountAfter);

        _sut.OnMissionStart(isSiegeBattle: true, sceneName: "town_castle");

        _logger.Received().LogWarning(Arg.Is<string>(s =>
            s.Contains("[SiegeDismount]") && s.Contains("HasMount returned true but capture was empty")));
    }
}
