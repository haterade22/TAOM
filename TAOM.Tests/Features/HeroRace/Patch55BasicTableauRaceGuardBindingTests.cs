using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// Drift-guard for Patch55's private-field injection. <c>HarmonyPatchBindingTests</c> covers the
/// patch's <c>[HarmonyPatch]</c> target (<c>BasicCharacterTableau.RefreshCharacterTableau</c>), but
/// the <c>ref int ____race</c> parameter binds the private field <c>_race</c> BY NAME at
/// patch-apply time and is NOT covered there. On engine drift Harmony throws while applying the
/// category in <c>OnBeforeInitialModuleScreenSetAsRoot</c> — a module-load crash in-game. This is
/// the red test that catches the rename offline first (Patch58 precedent). The type is resolved by
/// name (TAOM.Tests carries no TaleWorlds.MountAndBlade.View reference), mirroring how Harmony
/// itself binds.
/// </summary>
[TestClass]
public class Patch55BasicTableauRaceGuardBindingTests
{
    private const string TableauTypeName = "TaleWorlds.MountAndBlade.View.Tableaus.BasicCharacterTableau";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void RaceField_BindingResolves_AgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var tableauType = AccessTools.TypeByName(TableauTypeName);
        Assert.IsNotNull(
            tableauType,
            TableauTypeName + " did not resolve against the installed engine — Patch55's target type is gone.");

        var field = AccessTools.Field(tableauType, "_race");
        Assert.IsNotNull(
            field,
            "BasicCharacterTableau._race did not resolve against the installed engine — " +
            "Harmony would throw applying Patch55 at module load (cold-menu CTD guard dead + load crash).");
        Assert.AreEqual(
            typeof(int), field.FieldType,
            "BasicCharacterTableau._race is no longer an int — the ____race injection signature drifted.");
    }
}
