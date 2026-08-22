using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FieldCamp.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// Drift-guard for Patch74's string-bound target: the patch names
/// <c>PartyPlayerNameplateWidget.UpdateNameplatesVisibility</c> by string, so the compiler cannot
/// catch the engine renaming or re-signaturing it. If the binding drifts, Harmony throws at
/// category-apply time, SubModule's guarded loop logs it and carries on, and the camp icon
/// silently never appears: exactly the quiet regression that must fail here first.
///
/// The sprite names are pinned as constants only: whether the atlas actually renders them cannot
/// be certified statically (sprite sheets are data, resolved by the live UI context). A missing
/// sprite degrades to an invisible icon by design, so the tests assert the names are present and
/// atlas-path shaped, nothing more.
/// </summary>
[TestClass]
public class Patch74NameplateBindingTests
{
    private const string WidgetTypeName =
        "TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate.PartyPlayerNameplateWidget";
    private const string TargetMethodName = "UpdateNameplatesVisibility";
    private const string ExpectedCategory = "Patch74_FieldCampNameplateIcon";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static System.Type RequireWidgetType()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var widgetType = AccessTools.TypeByName(WidgetTypeName);
        Assert.IsNotNull(
            widgetType,
            WidgetTypeName + " did not resolve against the installed engine; Patch74's target type is gone.");
        return widgetType;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TargetType_Resolves_AgainstInstalledEngine()
        => RequireWidgetType();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void UpdateNameplatesVisibility_FloatOverload_ResolvesAgainstInstalledEngine()
    {
        var widgetType = RequireWidgetType();

        var method = AccessTools.Method(widgetType, TargetMethodName, new[] { typeof(float) });
        Assert.IsNotNull(
            method,
            WidgetTypeName + "." + TargetMethodName + "(float) did not resolve; Patch74 would fail "
            + "to apply and the camp nameplate icon would silently never render.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchCategory_MatchesSubModuleRegistration()
    {
        // SubModule.cs applies the category by string; a rename on either side silently orphans
        // the patch (Harmony applies nothing and reports nothing for an empty category).
        var attributes = typeof(PartyNameplateCampIconPatch)
            .GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false);
        Assert.AreEqual(1, attributes.Length, "Patch74 lost its HarmonyPatchCategory attribute.");
        Assert.AreEqual(ExpectedCategory, ((HarmonyPatchCategory)attributes[0]).info.category);
    }

    [TestMethod]
    public void SpriteNames_ArePinnedAndAtlasPathShaped()
    {
        // Rendering is not certifiable statically; this only pins the ids against accidental
        // edits. Each is an atlas path (Category\sprite), hence the separator check.
        foreach (var sprite in new[]
        {
            PartyNameplateCampIconPatch.LookoutSprite,
            PartyNameplateCampIconPatch.AmbushSprite,
            PartyNameplateCampIconPatch.CampSprite,
        })
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(sprite), "A camp icon sprite id is empty.");
            StringAssert.Contains(sprite, "\\", "Camp icon sprite id '" + sprite + "' lost its atlas path.");
        }
    }

    [TestMethod]
    public void IconWidgetId_IsPinned()
    {
        // FindIcon matches children by this id every frame; an empty id would match unrelated
        // widgets that never set one.
        Assert.IsFalse(string.IsNullOrWhiteSpace(PartyNameplateCampIconPatch.IconWidgetId));
    }
}
