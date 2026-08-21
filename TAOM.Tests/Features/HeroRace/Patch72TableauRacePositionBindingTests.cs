using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// Drift-guard for Patch72's private-field injections. <c>HarmonyPatchBindingTests</c> resolves the
/// patch TARGET (<c>CharacterTableau.RefreshCharacterTableau</c>) automatically, but every
/// <c>____fieldName</c> parameter binds a private field BY NAME at patch-apply time and is not
/// covered there. Patch72 binds eight of them, which is a wide drift surface for one patch, so it
/// gets the widest binding test in the feature.
///
/// The eight exist because the postfix sets each entity origin ABSOLUTELY, from the tableau's own
/// spawn frames, rather than adding a delta to whatever origin the entity currently has. Reading
/// the four spawn frames is what makes re-applying the offset on every refresh a no-op instead of a
/// drift that walks the character out of shot. Cheaper binding, worse bug.
///
/// Failure mode if this drifts unnoticed: Harmony throws while applying the category, and the
/// guarded loop in SubModule.cs logs it and carries on, so the game does NOT crash. The character
/// preview silently reverts to vanilla framing instead. That is exactly the kind of quiet
/// regression a play-test does not reliably catch, so it has to fail here first.
/// </summary>
[TestClass]
public class Patch72TableauRacePositionBindingTests
{
    private const string TableauTypeName = "TaleWorlds.MountAndBlade.View.Tableaus.CharacterTableau";
    private const string AgentVisualsTypeName = "TaleWorlds.MountAndBlade.View.AgentVisuals";
    private const string MatrixFrameTypeName = "TaleWorlds.Library.MatrixFrame";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static System.Type RequireTableauType()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var tableauType = AccessTools.TypeByName(TableauTypeName);
        Assert.IsNotNull(
            tableauType,
            TableauTypeName + " did not resolve against the installed engine — Patch72's target type is gone.");
        return tableauType;
    }

    private static void AssertField(string fieldName, string expectedTypeName)
    {
        var tableauType = RequireTableauType();

        var field = AccessTools.Field(tableauType, fieldName);
        Assert.IsNotNull(
            field,
            $"CharacterTableau.{fieldName} did not resolve against the installed engine — the "
            + $"____{fieldName.TrimStart('_')} injection on Patch72 would fail to bind and the "
            + "per-race preview framing would silently revert to vanilla.");
        Assert.AreEqual(
            expectedTypeName, field.FieldType.FullName,
            $"CharacterTableau.{fieldName} changed type — the Patch72 injection signature drifted.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AgentVisualsField_BindingResolves_AgainstInstalledEngine()
        => AssertField("_agentVisuals", AgentVisualsTypeName);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MountVisualsField_BindingResolves_AgainstInstalledEngine()
        => AssertField("_mountVisuals", AgentVisualsTypeName);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void RaceField_BindingResolves_AgainstInstalledEngine()
        => AssertField("_race", typeof(int).FullName);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PlacesSwappedField_BindingResolves_AgainstInstalledEngine()
        => AssertField("_isCharacterMountPlacesSwapped", typeof(bool).FullName);

    // The four spawn frames are the absolute origins the offsets are measured from. If any one of
    // them drifts, the offset for that slot silently stops being applied.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void InitialSpawnFrameField_BindingResolves_AgainstInstalledEngine()
        => AssertField("_initialSpawnFrame", MatrixFrameTypeName);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CharacterMountPositionFrameField_BindingResolves_AgainstInstalledEngine()
        => AssertField("_characterMountPositionFrame", MatrixFrameTypeName);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MountSpawnPointField_BindingResolves_AgainstInstalledEngine()
        => AssertField("_mountSpawnPoint", MatrixFrameTypeName);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MountCharacterPositionFrameField_BindingResolves_AgainstInstalledEngine()
        => AssertField("_mountCharacterPositionFrame", MatrixFrameTypeName);

    // The tuner forces a redraw by setting this flag, so it is a binding too — just one reached
    // from a console command rather than from the patch signature.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void VisualsDirtyField_BindingResolves_AgainstInstalledEngine()
        => AssertField("_isVisualsDirty", typeof(bool).FullName);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void RefreshCharacterTableau_TargetMethodResolves_AgainstInstalledEngine()
    {
        var tableauType = RequireTableauType();

        var method = AccessTools.Method(tableauType, "RefreshCharacterTableau");
        Assert.IsNotNull(
            method,
            "CharacterTableau.RefreshCharacterTableau did not resolve — Patch72 has no target to postfix.");
    }
}
