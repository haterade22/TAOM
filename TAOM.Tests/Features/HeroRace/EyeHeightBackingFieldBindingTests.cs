using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// Drift-guard for the two compiler-generated field names <c>EyeHeightAdjustmentHook</c> writes by
/// string literal.
///
/// <para><c>Monster.StandingEyeHeight</c> and <c>CrouchEyeHeight</c> are auto-properties with private
/// setters, so the hook reaches their backing fields directly. Those names are an artefact of the C#
/// compiler, not part of any public contract: if TaleWorlds converts either to a plain field, an
/// expression-bodied property, or adds a public setter, the literal stops resolving. The hook catches
/// the resulting exception and logs it, which means the dwarf camera quietly reverts to human eye
/// height with nothing in-game to say why.</para>
///
/// <para><c>HarmonyPatchBindingTests</c> cannot cover this: there is no <c>[HarmonyPatch]</c>
/// attribute here to enumerate, just a reflection write inside a service. Same reasoning as
/// <c>ReflectionSiteBindingTests</c>.</para>
/// </summary>
[TestClass]
public class EyeHeightBackingFieldBindingTests
{
    private const string MonsterTypeName = "TaleWorlds.Core.Monster";
    private const string StandingBackingField = "<StandingEyeHeight>k__BackingField";
    private const string CrouchBackingField = "<CrouchEyeHeight>k__BackingField";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static System.Type RequireMonsterType()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AccessTools.TypeByName(MonsterTypeName);
        Assert.IsNotNull(type, MonsterTypeName + " did not resolve against the installed engine.");
        return type;
    }

    private static void AssertBackingField(string fieldName, string propertyName)
    {
        var monsterType = RequireMonsterType();

        var field = AccessTools.Field(monsterType, fieldName);
        Assert.IsNotNull(
            field,
            $"Monster.{propertyName} no longer has the auto-property backing field '{fieldName}'. "
            + "EyeHeightAdjustmentHook writes that literal by name; the write now throws, is caught "
            + "and logged, and the dwarf eye-height fix silently stops applying.");
        Assert.AreEqual(
            typeof(float), field.FieldType,
            $"Monster.{propertyName} is no longer a float.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void StandingEyeHeightBackingField_Resolves_AgainstInstalledEngine()
        => AssertBackingField(StandingBackingField, "StandingEyeHeight");

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CrouchEyeHeightBackingField_Resolves_AgainstInstalledEngine()
        => AssertBackingField(CrouchBackingField, "CrouchEyeHeight");

    // The hook constructs no Monster itself, but the tests that exercise it do. If TaleWorlds ever
    // declares a constructor with required arguments, those tests stop compiling rather than
    // silently covering nothing, and this states the dependency explicitly.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Monster_IsConstructibleWithoutArguments()
    {
        var monsterType = RequireMonsterType();

        var ctor = AccessTools.Constructor(monsterType, new System.Type[0]);
        Assert.IsNotNull(ctor,
            "Monster no longer has a parameterless constructor, so EyeHeightAdjustmentHookTests "
            + "can no longer build one.");
    }
}
