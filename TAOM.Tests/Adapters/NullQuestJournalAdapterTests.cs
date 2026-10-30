using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

/// <summary>ADR-013. The quest journal adapter core keeps registered where TAOM.Client is absent (a dedicated server).</summary>
[TestClass]
public class NullQuestJournalAdapterTests
{
    [TestMethod]
    public void Bind_ReturnsAReason_AndTheAdapterNeverReportsBound()
    {
        var adapter = new NullQuestJournalAdapter();

        Assert.IsFalse(string.IsNullOrEmpty(adapter.Bind()), "a null reason would read as bound");
        Assert.IsFalse(adapter.IsBound);
    }

    [TestMethod]
    public void AnyUnexamined_IsAlwaysFalse()
    {
        Assert.IsFalse(new NullQuestJournalAdapter().AnyUnexamined(new object()));
    }

    [TestMethod]
    public void Rebuild_Throws_BecauseNothingDefersWithoutABinding()
    {
        Assert.ThrowsException<InvalidOperationException>(() => new NullQuestJournalAdapter().Rebuild(new object()));
    }
}
