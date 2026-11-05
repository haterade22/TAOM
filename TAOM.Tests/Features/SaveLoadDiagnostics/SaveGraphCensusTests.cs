using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Tests.Features.SaveLoadDiagnostics;

[TestClass]
public class SaveGraphCensusTests
{
    private sealed class EngineThing { }
    private sealed class OtherThing { }

    private static SaveGraphCensus NewCensus() => new SaveGraphCensus(minRowCount: 1, maxRowsPerKind: 3);

    [TestMethod]
    public void AddObject_CountsPerType()
    {
        var census = NewCensus();

        census.AddObject(typeof(string));
        census.AddObject(typeof(string));
        census.AddObject(typeof(int));

        var lines = census.FormatTypeLines().ToList();
        CollectionAssert.AreEqual(
            new[] { "obj n=2 type='System.String'", "obj n=1 type='System.Int32'" },
            lines);
    }

    [TestMethod]
    public void AddMbObject_CountsUnregisteredAndDuplicateIds()
    {
        var census = NewCensus();

        census.AddMbObject(typeof(string), 5, isRegistered: true);
        census.AddMbObject(typeof(string), 5, isRegistered: false);
        census.AddMbObject(typeof(string), 6, isRegistered: null);

        CollectionAssert.AreEqual(
            new[] { "obj n=3 unregistered=1 dupIds=1 type='System.String'" },
            census.FormatTypeLines().ToList());
        StringAssert.Contains(census.FormatSummary("r", 1, 0), "unregistered=1 duplicateIds=1");
    }

    [TestMethod]
    public void AddMbObject_ZeroIdIsNeverADuplicate()
    {
        var census = NewCensus();

        census.AddMbObject(typeof(string), 0, isRegistered: true);
        census.AddMbObject(typeof(string), 0, isRegistered: true);

        CollectionAssert.AreEqual(
            new[] { "obj n=2 unregistered=0 dupIds=0 type='System.String'" },
            census.FormatTypeLines().ToList());
    }

    [TestMethod]
    public void AddContainer_SumsElementCounts_IgnoringUnknown()
    {
        var census = NewCensus();

        census.AddContainer(typeof(List<int>), 4);
        census.AddContainer(typeof(List<int>), -1);
        census.AddContainer(typeof(List<int>), 2);

        CollectionAssert.AreEqual(
            new[] { "ctr n=3 elements=6 type='System.Collections.Generic.List<System.Int32>'" },
            census.FormatTypeLines().ToList());
    }

    [TestMethod]
    public void FormatTypeLines_ThresholdCapAndFlaggedRows()
    {
        var census = new SaveGraphCensus(minRowCount: 2, maxRowsPerKind: 2);
        AddObjects(census, typeof(string), 5);
        AddObjects(census, typeof(int), 4);
        AddObjects(census, typeof(long), 3);  // past the cap
        census.AddObject(typeof(short));      // below the threshold
        census.AddMbObject(typeof(byte), 9, isRegistered: false); // flagged: always listed
        census.AddContainer(typeof(List<int>), 1);
        census.AddContainer(typeof(List<int>), 1);
        census.AddContainer(typeof(List<string>), 1); // below the threshold

        CollectionAssert.AreEqual(
            new[]
            {
                "obj n=5 type='System.String'",
                "obj n=4 type='System.Int32'",
                "obj n=1 unregistered=1 dupIds=0 type='System.Byte'",
                "ctr n=2 elements=2 type='System.Collections.Generic.List<System.Int32>'",
            },
            census.FormatTypeLines().ToList());
    }

    [TestMethod]
    public void FriendlyName_NestedGenericsUseFullArgumentNames()
    {
        Assert.AreEqual(
            "System.Collections.Generic.Dictionary<System.String,System.Collections.Generic.List<System.Int32>>",
            SaveGraphCensus.FriendlyName(typeof(Dictionary<string, List<int>>)));
    }

    [TestMethod]
    public void Walk_UsesTheDescribeDelegateForEngineObjects()
    {
        var objects = new object[] { new EngineThing(), new EngineThing(), new OtherThing() };
        var containers = new object[] { new List<int> { 1, 2 }, new[] { 1, 2, 3 }, new object() };

        var census = SaveGraphCensus.Walk(objects, containers,
            o => o is EngineThing ? (5u, (bool?)false) : ((uint Id, bool? IsRegistered)?)null);

        StringAssert.Contains(
            census.FormatSummary("console", 2, 17),
            "reason=console loadsThisProcess=2 objects=3 containers=3 objectTypes=2 containerTypes=3 unregistered=2 duplicateIds=1 passMs=17");
        Assert.IsTrue(census.FormatTypeLines().Any(l =>
            l.StartsWith("obj n=2 unregistered=2 dupIds=1 type='") && l.Contains("EngineThing")));
    }

    private static void AddObjects(SaveGraphCensus census, Type type, int count)
    {
        for (int i = 0; i < count; i++)
            census.AddObject(type);
    }
}
