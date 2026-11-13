using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionDiagnostic;

namespace TAOM.Tests.Features.MissionDiagnostic;

// The census reads an agent on the tick AFTER the spawning call returned (the engine sets the final action set
// after SpawnAgent returns), reads it once, and skips an agent removed in between. Plain objects stand in for agents.
[TestClass]
public class PendingAgentReadsTests
{
    private PendingAgentReads<object> _sut = null!;
    private List<object> _read = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new PendingAgentReads<object>();
        _read = new List<object>();
    }

    [TestMethod]
    public void Drain_AddedAgent_ReadsItOnce()
    {
        var a = new object();
        _sut.Add(a);

        _sut.Drain(_read.Add);

        CollectionAssert.AreEqual(new[] { a }, _read);
    }

    [TestMethod]
    public void Drain_SecondTickWithoutNewAgents_ReadsNothing()
    {
        _sut.Add(new object());
        _sut.Drain(_ => { });

        _sut.Drain(_read.Add);

        Assert.AreEqual(0, _read.Count);
    }

    [TestMethod]
    public void Drain_AgentAddedAfterTheDrain_IsReadOnTheNextDrainOnly()
    {
        var late = new object();
        _sut.Add(new object());
        _sut.Drain(_ => { });
        _sut.Add(late);

        _sut.Drain(_read.Add);
        _sut.Drain(_read.Add);

        CollectionAssert.AreEqual(new[] { late }, _read);
    }

    [TestMethod]
    public void Drain_AgentRemovedBeforeTheTick_IsNotRead()
    {
        var gone = new object();
        var kept = new object();
        _sut.Add(gone);
        _sut.Add(kept);

        _sut.Remove(gone);
        _sut.Drain(_read.Add);

        CollectionAssert.AreEqual(new[] { kept }, _read);
    }

    [TestMethod]
    public void Remove_AgentAlreadyRead_DoesNothing()
    {
        var a = new object();
        _sut.Add(a);
        _sut.Drain(_ => { });

        _sut.Remove(a);
        _sut.Drain(_read.Add);

        Assert.AreEqual(0, _read.Count);
    }

    [TestMethod]
    public void Drain_SameAgentAddedTwice_ReadsItOnce()
    {
        var a = new object();
        _sut.Add(a);
        _sut.Add(a);

        _sut.Drain(_read.Add);

        Assert.AreEqual(1, _read.Count);
    }

    [TestMethod]
    public void Drain_ReadsInTheOrderAgentsWereAdded()
    {
        var first = new object();
        var second = new object();
        var third = new object();
        _sut.Add(first);
        _sut.Add(second);
        _sut.Add(third);

        _sut.Drain(_read.Add);

        CollectionAssert.AreEqual(new[] { first, second, third }, _read);
    }

    [TestMethod]
    public void Drain_ReaderThrows_RestIsDroppedNotRetried()
    {
        _sut.Add(new object());
        _sut.Add(new object());

        Assert.ThrowsException<System.InvalidOperationException>(
            () => _sut.Drain(_ => throw new System.InvalidOperationException()));
        _sut.Drain(_read.Add);

        Assert.AreEqual(0, _read.Count);
    }

    [TestMethod]
    public void Clear_DropsEverythingPending()
    {
        _sut.Add(new object());

        _sut.Clear();
        _sut.Drain(_read.Add);

        Assert.AreEqual(0, _read.Count);
    }
}
