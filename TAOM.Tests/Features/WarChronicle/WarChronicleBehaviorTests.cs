using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarChronicle.Rally;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.WarChronicle;

/// <summary>
/// The behavior is built fresh in every campaign's OnGameStart (pinned in <see cref="WarChronicleWiringTests"/>),
/// before any campaign event and before a load's SyncData, so its constructor resets every singleton
/// that holds campaign state and a load then restores the saved state over the reset (no latch).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class WarChronicleBehaviorTests
{
    private IModLogger _logger = null!;
    private IWarChronicleSettingsProvider _settings = null!;
    private WarEffectService _effects = null!;
    private WarBaselineService _baselines = null!;
    private RallyTierStore _tiers = null!;
    private WarChronicleStateService _state = null!;
    private IKingdomWarSnapshotAdapter _snapshots = null!;
    private ICoopSessionProvider _coop = null!;
    private WarChronicleBehavior _sut = null!;
    private readonly List<string> _logged = new List<string>();

    [TestInitialize]
    public void Setup()
    {
        _logged.Clear();
        _logger = Substitute.For<IModLogger>();
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(c => _logged.Add(c.Arg<string>()));
        _settings = Substitute.For<IWarChronicleSettingsProvider>();
        _settings.WarEffectStrength.Returns(1f);
        _effects = new WarEffectService(_settings, _logger);
        _baselines = new WarBaselineService();
        _tiers = new RallyTierStore();
        _state = new WarChronicleStateService(_effects, _baselines, _tiers, _logger);
        _snapshots = Substitute.For<IKingdomWarSnapshotAdapter>();
        _snapshots.GetCampaignId().Returns("c1");
        _snapshots.GetNowHours().Returns(100d);
        _snapshots.GetKingdoms().Returns(new List<KingdomWarSnapshot>
        {
            new KingdomWarSnapshot { Id = "empire_w", CultureId = "gondor", Towns = 3, Castles = 1 },
        });
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _coop.IsCoopClient.Returns(false);
        _sut = NewBehavior();
    }

    private WarChronicleBehavior NewBehavior(WarEffectService? effects = null)
    {
        var effectService = effects ?? _effects;
        var wotr = Substitute.For<IWarOfTheRingService>();
        var ledger = new WarLedgerService(_snapshots, _baselines, _tiers, effectService, wotr, Substitute.For<IAlignmentService>(), _logger);
        var tick = new WarChronicleTickService(_snapshots, effectService, _baselines, ledger, NoEscapes(effectService), QuietRally(effectService, _baselines, _tiers, ledger), _logger);
        return new WarChronicleBehavior(tick, _state, ledger, _coop, _logger);
    }

    private WarEscapeDailyPass NoEscapes(IWarEffectService effects) =>
        new WarEscapeDailyPass(Substitute.For<IPrisonerEscapeAdapter>(), effects, new WarEscapeService(), _logger);

    private RallyService QuietRally(IWarEffectService effects, WarBaselineService baselines, RallyTierStore tiers, WarLedgerService ledger)
    {
        var config = Substitute.For<IRallyConfigProvider>();
        config.GetConfig().Returns(new RallyConfig());
        var settings = Substitute.For<IWarChronicleSettingsProvider>();
        settings.WarRallyEnabled.Returns(true);
        return new RallyService(baselines, tiers, effects, config, settings, Substitute.For<IAlignmentService>(), ledger, _logger);
    }

    private static WarEffect Effect(double end = 500d) =>
        new WarEffect("event", "empire_w", WarEffectKind.VolunteerRate, 0.1f, end);

    [TestMethod]
    public void Constructor_ResetsTheRegistriesOfThePreviousCampaign()
    {
        _effects.Apply(Effect());
        _baselines.EnsureBaselines(new[] { new KingdomWarSnapshot { Id = "empire_w", Towns = 3 } });
        _tiers.Restore(new Dictionary<string, int> { ["empire_w"] = 2 });

        NewBehavior();

        Assert.AreEqual(0, _effects.Snapshot().Count);
        Assert.IsNull(_baselines.GetBaseline("empire_w"));
        Assert.AreEqual(0, _tiers.GetTier("empire_w"));
    }

    [TestMethod]
    public void SyncData_Saving_WritesTheCountAndOneKeyPerChunk()
    {
        _effects.Apply(Effect());
        var store = new FakeDataStore { Loading = false };

        _sut.SyncData(store);

        Assert.AreEqual(1, store.Get<int>("_taom_war_chronicle_v1_count"));
        StringAssert.Contains(store.Get<string>("_taom_war_chronicle_v1_0"), "\"VolunteerRate\"");
        Assert.IsFalse(store.Has("_taom_war_chronicle_v1_1"));
    }

    [TestMethod]
    public void SyncData_SaveThenLoadIntoANewBehavior_RestoresOverTheConstructorReset()
    {
        _effects.Apply(Effect());
        _baselines.EnsureBaselines(new[] { new KingdomWarSnapshot { Id = "empire_w", Towns = 3, Castles = 1 } });
        var store = new FakeDataStore { Loading = false };
        _sut.SyncData(store);

        var effects2 = new WarEffectService(_settings, _logger);
        var baselines2 = new WarBaselineService();
        var state2 = new WarChronicleStateService(effects2, baselines2, new RallyTierStore(), _logger);
        var ledger2 = new WarLedgerService(_snapshots, baselines2, new RallyTierStore(), effects2, Substitute.For<IWarOfTheRingService>(),
            Substitute.For<IAlignmentService>(), _logger);
        var loaded = new WarChronicleBehavior(
            new WarChronicleTickService(_snapshots, effects2, baselines2, ledger2, NoEscapes(effects2), QuietRally(effects2, baselines2, new RallyTierStore(), ledger2), _logger), state2, ledger2, _coop, _logger);
        store.Loading = true;

        loaded.SyncData(store);

        Assert.AreEqual(1, effects2.Snapshot().Count);
        Assert.AreEqual(7, baselines2.GetBaseline("empire_w"));
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(5000)]
    public void SyncData_LoadingAnAbsurdChunkCount_IsTreatedAsNoPayload(int count)
    {
        var store = new FakeDataStore { Loading = true };
        store.Put("_taom_war_chronicle_v1_count", count);

        _sut.SyncData(store);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("chunk count")));
        Assert.AreEqual(0, _baselines.Count);
    }

    [TestMethod]
    public void OnDailyTick_Authority_ExpiresAndWritesTheLedger()
    {
        var effects = Substitute.For<IWarEffectService>();
        effects.GetMultiplier(Arg.Any<string>(), Arg.Any<WarEffectKind>()).Returns(1f);
        var sut = NewBehaviorWith(effects);

        sut.OnDailyTick();

        effects.Received(1).Expire(100d);
        Assert.IsTrue(_logged.Any(l => l.Contains("t=kingdom")));
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public void OnDailyTick_ACoopClientOrANonAuthority_DoesNothing(bool authority, bool client)
    {
        _coop.IsAuthority.Returns(authority);
        _coop.IsCoopClient.Returns(client);
        var effects = Substitute.For<IWarEffectService>();
        var sut = NewBehaviorWith(effects);

        sut.OnDailyTick();

        effects.DidNotReceive().Expire(Arg.Any<double>());
        Assert.AreEqual(0, _logged.Count);
    }

    [TestMethod]
    public void HandleKingdomDestroyed_Authority_WritesTheDestroyedLine()
    {
        _snapshots.GetElapsedDay().Returns(70);

        _sut.HandleKingdomDestroyed("vlandia");

        CollectionAssert.Contains(_logged, "[WarLedger] v=1 t=event cid=c1 day=70 ev=destroyed k=vlandia");
    }

    [TestMethod]
    public void HandleKingdomDestroyed_NotAuthority_WritesNothing()
    {
        _coop.IsAuthority.Returns(false);

        _sut.HandleKingdomDestroyed("vlandia");

        Assert.AreEqual(0, _logged.Count);
    }

    private WarChronicleBehavior NewBehaviorWith(IWarEffectService effects)
    {
        var ledger = new WarLedgerService(_snapshots, _baselines, _tiers, effects, Substitute.For<IWarOfTheRingService>(),
            Substitute.For<IAlignmentService>(), _logger);
        var tick = new WarChronicleTickService(_snapshots, effects, _baselines, ledger, NoEscapes(effects), QuietRally(effects, _baselines, _tiers, ledger), _logger);
        return new WarChronicleBehavior(tick, _state, ledger, _coop, _logger);
    }

    private sealed class FakeDataStore : IDataStore
    {
        private readonly Dictionary<string, object?> _data = new Dictionary<string, object?>();

        public bool Loading { get; set; }

        public bool IsSaving => !Loading;

        public bool IsLoading => Loading;

        public bool Has(string key) => _data.ContainsKey(key);

        public T Get<T>(string key) => (T)_data[key]!;

        public void Put(string key, object value) => _data[key] = value;

        public bool SyncData<T>(string key, ref T data)
        {
            if (Loading)
            {
                if (!_data.TryGetValue(key, out var stored))
                    return false;
                data = (T)stored!;
                return true;
            }

            _data[key] = data;
            return true;
        }
    }
}
