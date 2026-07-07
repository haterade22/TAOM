using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Newtonsoft.Json;
using TAOM.Core.Logging;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

/// <summary>
/// Regression guard for the v2.0.9 save-corruption bug: the momentum log serialized as a
/// single SyncData string crossed the engine's 32,767-byte per-entry limit in a developed
/// campaign, and ArchiveSerializer's (short)length truncation bricked the save at write time.
/// The chunker splits the JSON so every synced string stays under the limit.
/// </summary>
[TestClass]
public class MomentumSyncChunkerTests
{
    // ---- Split/Join round trip ----

    [TestMethod]
    public void SplitJoin_ArbitraryString_RoundTrips()
    {
        var original = string.Concat(Enumerable.Range(0, 55000).Select(i => (char)('a' + i % 26)));
        var chunks = MomentumSyncChunker.Split(original);
        Assert.AreEqual(original, MomentumSyncChunker.Join(chunks));
    }

    [TestMethod]
    public void Split_EmptyOrNull_ReturnsNoChunks()
    {
        Assert.AreEqual(0, MomentumSyncChunker.Split("").Count);
        Assert.AreEqual(0, MomentumSyncChunker.Split(null).Count);
    }

    [TestMethod]
    public void Join_EmptyList_ReturnsEmptyString()
    {
        Assert.AreEqual(string.Empty, MomentumSyncChunker.Join(new List<string>()));
        Assert.AreEqual(string.Empty, MomentumSyncChunker.Join(null));
    }

    [TestMethod]
    public void Split_StringUnderCap_ReturnsSingleChunk()
    {
        var chunks = MomentumSyncChunker.Split("short payload");
        Assert.AreEqual(1, chunks.Count);
        Assert.AreEqual("short payload", chunks[0]);
    }

    [TestMethod]
    public void Split_EveryChunk_StaysUnderEngineByteLimit()
    {
        var original = string.Concat(Enumerable.Range(0, 100000).Select(i => (char)('a' + i % 26)));
        foreach (var chunk in MomentumSyncChunker.Split(original))
            Assert.IsTrue(MomentumSyncChunker.Utf8ByteLength(chunk) <= MomentumSyncChunker.EngineEntryByteLimit,
                $"chunk was {MomentumSyncChunker.Utf8ByteLength(chunk)} bytes — exceeds the engine entry limit");
    }

    [TestMethod]
    public void Split_MultibyteUtf8_ChunkBytesStayUnderLimit()
    {
        // Worst-case-ish: 3-byte UTF-8 chars (each 1 UTF-16 code unit). MaxChunkChars is sized
        // so even this can't exceed the byte limit.
        var original = new string('中', 40000);   // CJK, 3 bytes each in UTF-8
        var chunks = MomentumSyncChunker.Split(original);
        Assert.AreEqual(original, MomentumSyncChunker.Join(chunks));
        foreach (var chunk in chunks)
            Assert.IsTrue(MomentumSyncChunker.Utf8ByteLength(chunk) <= MomentumSyncChunker.EngineEntryByteLimit);
    }

    // ---- The actual corruption condition, end to end ----

    [TestMethod]
    public void RealisticMaxMomentumLog_ExceedsEngineLimitAsOneString_ButEveryChunkIsSafe()
    {
        // Build a fully-loaded momentum store: both sides, every action type at the 100-event
        // cap, each event carrying a realistic localized description — the day-~50 state that
        // bricked v2.0.9 saves.
        var store = new MomentumStateStore(Substitute.For<IModLogger>());
        store.State.MarkWarStarted();
        foreach (var side in new[] { store.State.Free, store.State.Evil })
        {
            side.AddKingdom("empire_w"); side.AddKingdom("sturgia"); side.AddKingdom("vlandia");
            foreach (MomentumActionType type in Enum.GetValues(typeof(MomentumActionType)))
                for (int n = 0; n < MomentumSideData.MaxEventsPerType; n++)
                    side.AddEvent(new MomentumEvent(
                        500 + n, $"Eine Armee unter der Führung von Théoden hat sich versammelt. (#{n})",
                        type, 600000.0 + n));
        }

        var json = JsonConvert.SerializeObject(store.Serialize());
        Assert.IsTrue(Encoding.UTF8.GetByteCount(json) > MomentumSyncChunker.EngineEntryByteLimit,
            "the realistic max log must exceed the engine limit as one string — that is the bug being guarded");

        var chunks = MomentumSyncChunker.Split(json);
        Assert.IsTrue(chunks.Count > 1, "an over-limit payload must split into more than one chunk");
        foreach (var chunk in chunks)
            Assert.IsTrue(MomentumSyncChunker.Utf8ByteLength(chunk) <= MomentumSyncChunker.EngineEntryByteLimit);

        // And the split is lossless — the store round-trips through chunk-join.
        var rehydrated = new MomentumStateStore(Substitute.For<IModLogger>());
        rehydrated.Deserialize(JsonConvert.DeserializeObject<Dictionary<string, string>>(
            MomentumSyncChunker.Join(chunks)));
        Assert.AreEqual(store.State.Free.SideMomentum, rehydrated.State.Free.SideMomentum);
        Assert.AreEqual(store.State.Evil.SideMomentum, rehydrated.State.Evil.SideMomentum);
    }
}
