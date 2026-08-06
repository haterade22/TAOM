using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// Drift-guard for Patch67's private-field injection (issue #389 instrumentation).
///
/// The diagnostic postfix binds SIX private fields of <c>CharacterTableau</c> BY NAME. Harmony
/// resolves those at patch-apply time, so a rename in an engine bump throws while applying the
/// category — and because the diagnostic sits in the same isolated batch as the character-preview
/// patches, that failure would be logged rather than crash, i.e. the instrument would go silently
/// dead exactly when an engine bump made it most necessary. This is the offline red test
/// (Patch55/Patch58 precedent).
///
/// The type is resolved by name because TAOM.Tests carries no TaleWorlds.MountAndBlade.View
/// reference — mirroring how Harmony itself binds. Note this assembly lives in
/// <c>Modules\Native\bin\Win64_Shipping_Client</c>, not the primary <c>bin\</c>.
/// </summary>
[TestClass]
public class Patch67TableauResidencyBindingTests
{
    private const string TableauTypeName = "TaleWorlds.MountAndBlade.View.Tableaus.CharacterTableau";
    private const string AgentVisualsTypeName = "TaleWorlds.MountAndBlade.View.AgentVisuals";
    private const string BodyPropertiesTypeName = "TaleWorlds.Core.BodyProperties";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static System.Type ResolveTableau()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var tableauType = AccessTools.TypeByName(TableauTypeName);
        Assert.IsNotNull(
            tableauType,
            TableauTypeName + " did not resolve against the installed engine — Patch67's target type is gone.");
        return tableauType;
    }

    private static void AssertField(System.Type owner, string fieldName, string expectedTypeFullName)
    {
        var field = AccessTools.Field(owner, fieldName);
        Assert.IsNotNull(
            field,
            $"CharacterTableau.{fieldName} did not resolve — Harmony would throw applying Patch67 and the " +
            "#389 residency instrument would go dead.");
        Assert.AreEqual(
            expectedTypeFullName, field.FieldType.FullName,
            $"CharacterTableau.{fieldName} changed type — the ___{fieldName.TrimStart('_')} injection signature drifted.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LoadingCounterAndRaceFields_BindingResolves_AgainstInstalledEngine()
    {
        var tableau = ResolveTableau();

        // The report trigger. BOTH counters matter: vanilla gates the visibility swap on
        // `_mountVisualLoadingCounter == 0 && _agentVisualLoadingCounter == 0`.
        AssertField(tableau, "_agentVisualLoadingCounter", "System.Int32");
        AssertField(tableau, "_mountVisualLoadingCounter", "System.Int32");
        AssertField(tableau, "_race", "System.Int32");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CensusContextFields_BindingResolves_AgainstInstalledEngine()
    {
        var tableau = ResolveTableau();

        // Read by cached reflection on the reporting frame. AccessTools.Field returns null rather
        // than throwing on a rename, so without this gate the census would silently degrade to
        // "<unreadable>" for every field instead of failing loudly.
        AssertField(tableau, "_initialLoadingCounter", "System.Int32");
        AssertField(tableau, "_isEnabled", "System.Boolean");
        AssertField(tableau, "_clothColor1", "System.UInt32");
        AssertField(tableau, "_clothColor2", "System.UInt32");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CharacterKeyAndBodyPropertyFields_BindingResolves_AgainstInstalledEngine()
    {
        var tableau = ResolveTableau();

        // Without _charStringId every troop of a race collapses into one verdict and the
        // "uruk black / orc fine" comparison the instrument exists for is lost.
        AssertField(tableau, "_charStringId", "System.String");
        AssertField(tableau, "_bodyProperties", BodyPropertiesTypeName);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AgentVisualsBuffers_BindingResolves_AgainstInstalledEngine()
    {
        var tableau = ResolveTableau();

        AssertField(tableau, "_agentVisuals", AgentVisualsTypeName);
        AssertField(tableau, "_oldAgentVisuals", AgentVisualsTypeName);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchTargetMethods_Resolve_AgainstInstalledEngine()
    {
        var tableau = ResolveTableau();

        Assert.IsNotNull(
            AccessTools.Method(tableau, "OnTick"),
            "CharacterTableau.OnTick did not resolve — Patch67's census postfix has no target.");
        Assert.IsNotNull(
            AccessTools.Method(tableau, "SetRace"),
            "CharacterTableau.SetRace did not resolve — Patch67's race-change reset has no target.");
        Assert.IsNotNull(
            AccessTools.Method(tableau, "OnFinalize"),
            "CharacterTableau.OnFinalize did not resolve — Patch67's teardown reset has no target, so " +
            "closed tableaus would hold tracked slots for the rest of the session and the census would " +
            "go quiet after 64 characters.");
    }
}
