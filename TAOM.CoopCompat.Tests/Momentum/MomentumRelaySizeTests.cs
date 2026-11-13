using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ProtoBuf;
using TAOM.CoopCompat.Messages;
using TAOM.CoopCompat.Momentum;
using TAOM.CoopCompat.Transport;
using TAOM.Core.Logging;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.CoopCompat.Tests.Momentum;

/// <summary>A real MomentumStateStore on both ends, through the real protobuf serializer.</summary>
[TestClass]
public class MomentumRelaySizeTests
{
    private static MomentumStateStore Fill()
    {
        var store = new MomentumStateStore(Substitute.For<IModLogger>());
        foreach (var side in new[] { store.State.Free, store.State.Evil })
        {
            foreach (var type in new[] { MomentumActionType.BattleWon, MomentumActionType.VillageRaided, MomentumActionType.Sieges })
            {
                for (var i = 0; i < MomentumSideData.MaxEventsPerType; i++)
                    side.AddEvent(new MomentumEvent(1, "event number " + i + " of a long enough description to look real", type, 1000.123456789 + i));
            }
        }

        return store;
    }

    [TestMethod]
    public void A600EntryStore_RoundTripsThroughTheServiceAndTheSerializer_AndTheSizeIsLogged()
    {
        var server = Fill();
        var serverTransport = new FakeCoopTransport { CurrentRole = CoopRole.Server };
        var _ = new MomentumRelayService(serverTransport, server, Substitute.For<IModLogger>(), Substitute.For<IOutcomeLog>(), () => 0, () => 0);
        serverTransport.RaiseHello(FakeCoopTransport.Peer(), new CoopCompatHello());
        var sent = serverTransport.SnapshotsToPeer.Single().Snapshot;
        Assert.IsTrue(sent.Keys.Length >= 600, "entries: " + sent.Keys.Length);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, sent);
        stream.Position = 0;
        var received = Serializer.Deserialize<CoopCompatMomentumSnapshot>(stream);
        Console.WriteLine("momentum snapshot: " + sent.Keys.Length + " entries, " + stream.Length + " bytes encoded");

        var clientStore = new MomentumStateStore(Substitute.For<IModLogger>());
        var clientTransport = new FakeCoopTransport { CurrentRole = CoopRole.Client };
        var __ = new MomentumRelayService(clientTransport, clientStore, Substitute.For<IModLogger>(), Substitute.For<IOutcomeLog>(), () => 0, () => 0);
        clientTransport.RaiseClientReady();
        clientTransport.RaiseSnapshot(received);

        CollectionAssert.AreEqual(server.CaptureWarState().OrderBy(p => p.Key).ToList(), clientStore.CaptureWarState().OrderBy(p => p.Key).ToList());
        Assert.AreEqual(server.State.Free.SideMomentum, clientStore.State.Free.SideMomentum);
    }
}
