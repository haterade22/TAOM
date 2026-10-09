using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Rally;
using TAOM.Features.WarOfTheRingMomentum;

namespace TAOM.Tests.Features.WarChronicle;

[TestClass]
public class WarChronicleStateServiceTests
{
    private IModLogger _logger = null!;
    private WarEffectService _effects = null!;
    private WarBaselineService _baselines = null!;
    private RallyTierStore _tiers = null!;
    private WarChronicleStateService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        var settings = Substitute.For<IWarChronicleSettingsProvider>();
        settings.WarEffectStrength.Returns(1f);
        _effects = new WarEffectService(settings, _logger);
        _baselines = new WarBaselineService();
        _tiers = new RallyTierStore();
        _sut = new WarChronicleStateService(_effects, _baselines, _tiers, _logger);
    }

    private static KingdomWarSnapshot Kingdom(string id, int towns, int castles) =>
        new KingdomWarSnapshot { Id = id, Towns = towns, Castles = castles };

    private void Populate()
    {
        _effects.Apply(new WarEffect("rally:gondor", "gondor", WarEffectKind.VolunteerRate, 0.1f, 500d));
        _effects.Apply(new WarEffect("console", "mordor", WarEffectKind.PrisonerEscape, 0.5f, 600d));
        _baselines.EnsureBaselines(new[] { Kingdom("gondor", 3, 1), Kingdom("mordor", 2, 2) });
        _tiers.Restore(new Dictionary<string, int> { ["gondor"] = 2 });
    }

    [TestMethod]
    public void CaptureThenRestore_IntoAFreshState_RestoresEverySection()
    {
        Populate();
        var chunks = _sut.CaptureChunks();

        var settings = Substitute.For<IWarChronicleSettingsProvider>();
        settings.WarEffectStrength.Returns(1f);
        var effects = new WarEffectService(settings, _logger);
        var baselines = new WarBaselineService();
        var tiers = new RallyTierStore();
        new WarChronicleStateService(effects, baselines, tiers, _logger).RestoreFromChunks(chunks);

        Assert.AreEqual(2, effects.Snapshot().Count);
        Assert.AreEqual(1.1f, effects.GetMultiplier("gondor", WarEffectKind.VolunteerRate), 0.0001f);
        Assert.AreEqual(1.5f, effects.GetMultiplier("mordor", WarEffectKind.PrisonerEscape), 0.0001f);
        Assert.AreEqual(7, baselines.GetBaseline("gondor"));
        Assert.AreEqual(6, baselines.GetBaseline("mordor"));
        Assert.AreEqual(2, tiers.GetTier("gondor"));
    }

    [TestMethod]
    public void CaptureChunks_LargeState_EveryChunkIsAtMostTenThousandCharsAndUnderTheEngineByteCap()
    {
        for (var i = 0; i < 700; i++)
            _effects.Apply(new WarEffect("event:long_source_name_" + i, "kingdom_" + (i % 25), WarEffectKind.VolunteerRate, 0.01f, 5000d + i));

        var chunks = _sut.CaptureChunks();

        Assert.IsTrue(chunks.Count > 1, "the payload must span several chunks for this test to mean anything");
        foreach (var chunk in chunks)
        {
            Assert.IsTrue(chunk.Length <= MomentumSyncChunker.MaxChunkChars, "chunk of " + chunk.Length + " chars");
            Assert.IsTrue(MomentumSyncChunker.Utf8ByteLength(chunk) <= MomentumSyncChunker.EngineEntryByteLimit);
        }

        var settings = Substitute.For<IWarChronicleSettingsProvider>();
        settings.WarEffectStrength.Returns(1f);
        var effects = new WarEffectService(settings, _logger);
        new WarChronicleStateService(effects, new WarBaselineService(), new RallyTierStore(), _logger).RestoreFromChunks(chunks);
        Assert.AreEqual(_effects.Snapshot().Count, effects.Snapshot().Count, "the chunks join back to the same payload");
    }

    [TestMethod]
    public void CaptureChunks_EmptyState_IsOneSmallChunk()
    {
        var chunks = _sut.CaptureChunks();

        Assert.AreEqual(1, chunks.Count);
        StringAssert.Contains(chunks[0], "\"v\":1");
    }

    [TestMethod]
    public void RestoreFromChunks_ClearsAPreviousCampaignsStateFirst()
    {
        Populate();

        _sut.RestoreFromChunks(new List<string>());

        Assert.AreEqual(0, _effects.Snapshot().Count);
        Assert.IsNull(_baselines.GetBaseline("gondor"));
        Assert.AreEqual(0, _tiers.GetTier("gondor"));
    }

    [TestMethod]
    public void RestoreFromChunks_NullList_IsTreatedAsNoPayload()
    {
        Populate();

        _sut.RestoreFromChunks(null!);

        Assert.AreEqual(0, _baselines.Count);
        Assert.AreEqual(0, _effects.Snapshot().Count);
    }

    [TestMethod]
    public void RestoreFromChunks_CorruptBaselinesSection_ResetsOnlyThatSection()
    {
        var chunks = new List<string>
        {
            "{\"v\":1,\"effects\":[{\"s\":\"a\",\"k\":\"gondor\",\"kind\":\"VolunteerRate\",\"m\":0.1,\"end\":500}],"
            + "\"baselines\":\"corrupt\",\"rally\":{\"tiers\":{\"gondor\":1}}}",
        };

        _sut.RestoreFromChunks(chunks);

        Assert.AreEqual(1, _effects.Snapshot().Count);
        Assert.AreEqual(1, _tiers.GetTier("gondor"));
        Assert.IsNull(_baselines.GetBaseline("gondor"));
        Assert.AreEqual(0, _baselines.Count);
    }

    [TestMethod]
    public void RestoreFromChunks_LogsEachCodecWarning()
    {
        _sut.RestoreFromChunks(new List<string> { "{\"v\":1,\"effects\":\"oops\",\"rally\":[1]}" });

        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("[WarChronicle]")));
    }

    [TestMethod]
    public void RestoreFromChunks_ChunksSplitMidToken_JoinBeforeParsing()
    {
        Populate();
        var json = string.Concat(_sut.CaptureChunks());
        var halves = new List<string> { json.Substring(0, json.Length / 2), json.Substring(json.Length / 2) };

        _sut.RestoreFromChunks(halves);

        Assert.AreEqual(2, _effects.Snapshot().Count);
        Assert.AreEqual(7, _baselines.GetBaseline("gondor"));
    }

    [TestMethod]
    public void ChronicleSection_ARestoredOpaqueArray_IsWrittenBackUnchanged()
    {
        const string chronicle = "[{\"id\":\"hornburg\",\"state\":2,\"window\":[12.5,40]}]";
        var payload = new WarChroniclePayload { ChronicleJson = chronicle };
        _sut.RestoreFromChunks(new List<string> { WarChronicleSaveCodec.Serialize(payload) });

        var written = WarChronicleSaveCodec.Parse(string.Concat(_sut.CaptureChunks()));

        Assert.AreEqual(chronicle, written.ChronicleJson);
    }

    [TestMethod]
    public void ResetForNewSession_ClearsEveryRegistryAndTheOpaqueChronicle()
    {
        Populate();
        _sut.RestoreFromChunks(new List<string> { WarChronicleSaveCodec.Serialize(new WarChroniclePayload { ChronicleJson = "[1]" }) });
        Populate();

        _sut.ResetForNewSession();

        Assert.AreEqual(0, _effects.Snapshot().Count);
        Assert.IsNull(_baselines.GetBaseline("gondor"));
        Assert.AreEqual(0, _tiers.GetTier("gondor"));
        Assert.AreEqual("[]", WarChronicleSaveCodec.Parse(string.Concat(_sut.CaptureChunks())).ChronicleJson);
    }
}
