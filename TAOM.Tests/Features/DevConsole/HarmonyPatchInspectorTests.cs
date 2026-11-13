using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DevConsole;
using TAOM.Features.MapEventGuard.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>
/// The dev console's patch report declares a category from the assemblies it is given. The Telchar's Mark
/// postfixes live in TAOM.Client (ADR-013, B5): a report that read TAOM.dll alone would count their applied
/// patches but never declare the category.
/// </summary>
[TestClass]
public class HarmonyPatchInspectorTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("No game install resolved: the patch categories cannot be read.");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Collect_TwoAssemblies_DeclaresTheClientCategories()
    {
        RequireGame();
        var result = HarmonyPatchInspector.Collect(CrashGuardHarmony.TaomOwnerIds, TaomAssemblies.All);

        CollectionAssert.Contains(result.Declared.ToList(), "Patch37_CrashReport");
        CollectionAssert.Contains(result.Declared.ToList(), "Patch109_TelcharsMark");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Collect_CoreAlone_DoesNotDeclareTheClientCategories()
    {
        RequireGame();
        var result = HarmonyPatchInspector.Collect(CrashGuardHarmony.TaomOwnerIds, new[] { TaomAssemblies.All[0] });

        CollectionAssert.Contains(result.Declared.ToList(), "Patch37_CrashReport");
        CollectionAssert.DoesNotContain(result.Declared.ToList(), "Patch109_TelcharsMark");
    }

    [TestMethod]
    public void TaomOwnerIds_AreTheMainIdAndTheCrashGuardId()
    {
        CollectionAssert.AreEquivalent(new[] { "com.taom.mod", "TAOM.CrashGuards" }, CrashGuardHarmony.TaomOwnerIds);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Collect_CategoryAppliedUnderASecondTaomOwner_CountsItAsApplied()
    {
        RequireGame();
        const string ownerId = "taom.tests.inspector-second-owner";
        var harmony = new Harmony(ownerId);
        try
        {
            harmony.CreateClassProcessor(typeof(Patch143_EmptyMeetingGuard)).Patch();

            var both = HarmonyPatchInspector.Collect(new[] { "com.taom.mod", ownerId }, TaomAssemblies.All);
            var mainOnly = HarmonyPatchInspector.Collect(new[] { "com.taom.mod" }, TaomAssemblies.All);

            Assert.AreEqual(1, both.AppliedCounts[Patch143_EmptyMeetingGuard.Category]);
            Assert.IsFalse(both.OtherOwners.ContainsKey(ownerId));
            Assert.IsFalse(mainOnly.AppliedCounts.ContainsKey(Patch143_EmptyMeetingGuard.Category));
            Assert.IsTrue(mainOnly.OtherOwners.ContainsKey(ownerId));
        }
        finally
        {
            harmony.UnpatchAll(ownerId);
        }
    }

    [TestMethod]
    public void DiagnosticCheats_PrintPatches_PassesBothTaomOwnerIds()
    {
        var source = RepoPaths.ReadSource("Main/Features/DevConsole/Cheats/DiagnosticCheats.cs", stripComments: true);

        StringAssert.Contains(source, "HarmonyPatchInspector.Collect(CrashGuardHarmony.TaomOwnerIds");
    }
}
