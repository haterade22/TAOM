using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.HeroRace;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.HeroRace;

[TestClass]
public class RacePersistenceBehaviorTests
{
    private RacePersistenceBehavior _sut;
    private IRacePersistenceService _service;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<IRacePersistenceService>();
        _sut = new RacePersistenceBehavior(_service);
    }

    [TestMethod]
    public void SyncData_DelegatesToService()
    {
        var dataStore = Substitute.For<IDataStore>();

        _sut.SyncData(dataStore);

        _service.Received(1).SyncRaceData(dataStore);
    }

    [TestMethod]
    public void Behavior_IsCampaignBehaviorBase()
    {
        Assert.IsInstanceOfType(_sut, typeof(CampaignBehaviorBase));
    }

    // --- Phase 9b #183 — OnSessionLaunched restore + RegisterEvents wiring ---
    //
    // Pre-this, only SyncData delegation was covered. OnSessionLaunched (the path that re-applies
    // captured race IDs on game load) and OnBeforeSave (the path that captures them) had no test.
    // The cross-feature invariant from Phase 6 #171 (CharacterCreation → HeroRace race ID
    // round-trip via save/load) is now pinned at the behavior boundary.
    //
    // RegisterEvents touches sealed `CampaignEvents` so the subscriptions themselves are verified
    // via source-content assertion. The handler delegation (when invoked) is tested by reflection
    // over the lambda captures — too fragile here, so we rely on source content for the
    // subscription points and direct service-call assertion for SyncData (already covered).

    [TestMethod]
    public void RegisterEvents_SubscribesOnBeforeSaveAndOnSessionLaunched()
    {
        var source = ReadProjectSource("Main", "Features", "HeroRace", "RacePersistenceBehavior.cs");
        if (source == null)
            Assert.Inconclusive("RacePersistenceBehavior.cs not found — run from repo root");

        StringAssert.Contains(source, "CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, _service.CaptureHeroRaces)",
            "OnBeforeSave must capture races so the save snapshot includes the latest live race state. " +
            "Cross-feature contract with CharacterCreation (#171): newly-set race IDs from CC must reach the saved snapshot.");

        StringAssert.Contains(source, "CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => OnSessionLaunched())",
            "OnSessionLaunched must restore captured races so loaded heroes get their persisted race re-applied. " +
            "Without this subscription, every save-load reverts all heroes to vanilla race=0 (human).");
    }

    // --- Multiplayer field report 2026-08-03 §1 — session-launch capture ---

    [TestMethod]
    public void OnSessionLaunched_RestoresThenCaptures()
    {
        // A co-op host's save transfer to a joining client never raises OnBeforeSaveEvent, so without
        // a capture here the race map is empty when the world is serialized and the joiner receives
        // no race data at all.
        _sut.OnSessionLaunched();

        Received.InOrder(() =>
        {
            _service.RestoreHeroRaces();
            _service.CaptureHeroRaces();
        });
    }

    [TestMethod]
    public void OnSessionLaunched_DoesNotCaptureBeforeRestoring()
    {
        // The inverse of the above, asserted independently: capturing first would snapshot every
        // hero at their raw XML race and write that over the map the restore is about to apply,
        // destroying the persisted races on every single load.
        var callOrder = new System.Collections.Generic.List<string>();
        _service.When(s => s.RestoreHeroRaces()).Do(_ => callOrder.Add("restore"));
        _service.When(s => s.CaptureHeroRaces()).Do(_ => callOrder.Add("capture"));

        _sut.OnSessionLaunched();

        CollectionAssert.AreEqual(new[] { "restore", "capture" }, callOrder);
    }

    [TestMethod]
    public void MainSubModule_AndIoC_RegisterRacePersistenceBehavior()
    {
        var subModuleSource = ReadProjectSource("Main", "SubModule.cs");
        var iocSource = ReadProjectSource("Main", "IoC.cs");
        if (subModuleSource == null || iocSource == null)
            Assert.Inconclusive("Main/IoC.cs or SubModule.cs not found — run from repo root");

        // Behavior wiring catches the Messengers-class regression — dropping AddBehavior breaks
        // the entire HeroRace cross-feature contract silently.
        StringAssert.Contains(subModuleSource, "RacePersistenceBehavior",
            "Main/SubModule.cs must reference RacePersistenceBehavior — without AddBehavior the persistence loop never fires.");

        StringAssert.Contains(iocSource, "HeroRaceIoC.RegisterHeroRaceFeature(container)",
            "Main/IoC.cs::Configure must call HeroRaceIoC.RegisterHeroRaceFeature — otherwise " +
            "the behavior's IoC.Resolve<IRacePersistenceService>() throws at startup.");
    }

    // --- Helpers ---

    private static string ReadProjectSource(params string[] relativeParts)
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }
}
