using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.UI;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>
/// Pins the staged siege HUD movie to its view model. A binding that names nothing is a silent no-op in Gauntlet, and a
/// <c>DataSource</c> on a movie root is never read (the root is created with no parent), so the root carries none. The walk
/// reads attribute data and constructs no TaleWorlds attribute, so it runs without the game.
/// </summary>
[TestClass]
public class StagedSiegeHudPrefabTests
{
    private static string PrefabPath => Path.GetFullPath(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Main", "_Module", "GUI", "PreFabs", "StagedSiege", "StagedSiegeHud.xml"));

    private static XmlElement Root()
    {
        var doc = new XmlDocument();
        doc.Load(PrefabPath);
        return (XmlElement)doc.SelectSingleNode("/Prefab/Window/*")!;
    }

    [TestMethod]
    public void EveryBinding_ResolvesToAMemberOfTheViewModel()
    {
        var (failures, count) = PrefabBindingWalker.Walk(Root(), typeof(StagedSiegeHudVM), null);

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
        Assert.IsTrue(count > 0, "the walk found no bindings");
    }

    [TestMethod]
    public void TheMovieRoot_CarriesNoDataSource()
    {
        Assert.IsFalse(Root().HasAttribute("DataSource"), "GauntletMovie.LoadMovie reads no DataSource on the root view");
    }

    [TestMethod]
    public void EveryDataSourceProperty_IsBoundByThePrefab()
    {
        var bound = Root().SelectNodes("descendant-or-self::*/@*")!.Cast<XmlAttribute>()
            .Where(a => a.Value.StartsWith("@", StringComparison.Ordinal))
            .Select(a => a.Value.Substring(1))
            .ToHashSet();
        var declared = typeof(StagedSiegeHudVM).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CustomAttributes.Any(a => a.AttributeType.Name == "DataSourceProperty"))
            .Select(p => p.Name)
            .ToList();

        Assert.AreNotEqual(0, declared.Count);
        foreach (var name in declared)
            Assert.IsTrue(bound.Contains(name), $"[DataSourceProperty] {name} is bound by no widget");
    }
}
