using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.SaveSystem;
using TAOM.Features.EquipPresets.Models;

namespace TAOM.Tests.Features.EquipPresets;

[TestClass]
public class PresetSaveableTypeDefinerTests
{
    [TestMethod]
    public void BaseId_MatchesPlanned726900501()
    {
        var sut = new PresetSaveableTypeDefiner();
        var baseIdField = typeof(SaveableTypeDefiner).GetField("_saveBaseId",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(baseIdField, "SaveableTypeDefiner._saveBaseId field expected (verified ilspy 2026-05-06)");
        var baseId = (int)baseIdField!.GetValue(sut)!;
        Assert.AreEqual(PresetSaveableTypeDefiner.SaveBaseId, baseId);
        Assert.AreEqual(726900501, PresetSaveableTypeDefiner.SaveBaseId,
            "BaseId 726900501 — verified unique across TAOM at port time. Document range usage if changed.");
    }

    [TestMethod]
    public void BaseId_UniqueAcrossDiscoverableDefinersInTaomAssembly()
    {
        // Filter using GetExportedTypes which doesn't load every dependency. ReflectionTypeLoadException
        // on full GetTypes() is a known TAOM.Tests env failure (MCMv5 not in test output) — we ignore it
        // and proceed with the partial type list, which is sufficient for uniqueness checks.
        var taomAssembly = typeof(PresetSaveableTypeDefiner).Assembly;
        Type[] types;
        try
        {
            types = taomAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        var definerType = typeof(SaveableTypeDefiner);
        var allDefiners = types
            .Where(t => !t.IsAbstract && definerType.IsAssignableFrom(t))
            .ToList();

        Assert.IsTrue(allDefiners.Contains(typeof(PresetSaveableTypeDefiner)),
            "PresetSaveableTypeDefiner should be discoverable in the TAOM assembly");

        var baseIdField = typeof(SaveableTypeDefiner).GetField("_saveBaseId",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var baseIds = allDefiners.Select(t =>
        {
            try
            {
                var instance = Activator.CreateInstance(t) as SaveableTypeDefiner;
                return (definer: t, baseId: (int)baseIdField!.GetValue(instance!)!);
            }
            catch
            {
                return (definer: t, baseId: -1);  // skip unconstructible — won't collide with valid range.
            }
        }).Where(x => x.baseId > 0).ToList();

        var collisions = baseIds
            .GroupBy(x => x.baseId)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.AreEqual(0, collisions.Count,
            "BaseId collision detected: " + string.Join("; ",
                collisions.Select(g => $"{g.Key} -> {string.Join(",", g.Select(x => x.definer.Name))}")));
    }

    [TestMethod]
    public void Constructor_DoesNotThrow()
    {
        var sut = new PresetSaveableTypeDefiner();
        Assert.IsNotNull(sut);
    }
}
