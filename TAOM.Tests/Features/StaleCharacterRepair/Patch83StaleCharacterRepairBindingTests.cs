using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StaleCharacterRepair.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.StaleCharacterRepair;

/// <summary>
/// Drift-guard for Patch83. The repair rests on one patch target and four reflected engine members,
/// and every one of them fails QUIETLY:
///
/// <list type="bullet">
/// <item>the TARGET, <c>MBObjectManager.PreAfterLoad</c> — a rename throws at category-apply time.
/// <c>SubModule</c> now catches and logs that (it previously would have aborted module init), so
/// the observable result is simply that the repair never runs.</item>
/// <item>the four reflected members — a rename makes <c>AccessTools</c> return null, every repair
/// returns <c>BindingUnavailable</c>, and a save that used to load starts crashing again.</item>
/// </list>
///
/// Either drift turns a shipped crash fix back into the crash it fixed, with nothing louder than a
/// log line. That is what this class exists to catch first.
///
/// It cannot prove the repair BEHAVES: building a save-restored <c>CharacterObject</c> with null
/// fields needs a live campaign. <see cref="StaleCharacterRepairServiceTests"/> covers the decision
/// logic instead.
/// </summary>
[TestClass]
public class Patch83StaleCharacterRepairBindingTests
{
    private const string ExpectedCategory = "Patch83_StaleCharacterRepair";
    private const string ObjectManagerTypeName = "TaleWorlds.ObjectSystem.MBObjectManager";
    private const string BasicCharacterTypeName = "TaleWorlds.Core.BasicCharacterObject";
    private const string CharacterObjectTypeName = "TaleWorlds.CampaignSystem.CharacterObject";
    private const string SkillsTypeName = "TaleWorlds.Core.MBCharacterSkills";
    private const string BodyPropertyTypeName = "TaleWorlds.Core.MBBodyProperty";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type Resolve(string name)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AccessTools.TypeByName(name);
        Assert.IsNotNull(type, name + " did not resolve — Patch83 would apply to nothing.");
        return type;
    }

    // ---- the patch target ----------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PreAfterLoad_TargetResolves_AndIsStillParameterless()
    {
        var method = AccessTools.Method(Resolve(ObjectManagerTypeName), "PreAfterLoad");

        Assert.IsNotNull(method,
            "MBObjectManager.PreAfterLoad did not resolve. Patch83 postfixes it because it runs at "
            + "Campaign.OnGameLoaded:683, before the CampaignObjectManager.AfterLoad call that "
            + "crashes at :688 — re-verify that ordering before rebinding elsewhere.");
        Assert.AreEqual(0, method.GetParameters().Length,
            "PreAfterLoad gained parameters — the postfix signature must be re-checked.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AfterLoad_StillExists_SoTheFallbackSeamRemainsAvailable()
    {
        // Not the target, but the documented alternative one line later. If it ever disappears the
        // registry's ordering rationale needs rewriting, so pin it rather than discover it later.
        Assert.IsNotNull(AccessTools.Method(Resolve(ObjectManagerTypeName), "AfterLoad"));
    }

    // ---- the four reflected members ------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void DefaultCharacterSkillsField_Resolves_AndIsTheTypeTheRepairAssigns()
    {
        var field = AccessTools.Field(Resolve(BasicCharacterTypeName), "DefaultCharacterSkills");

        Assert.IsNotNull(field, "BasicCharacterObject.DefaultCharacterSkills did not resolve.");
        Assert.AreEqual(SkillsTypeName, field.FieldType.FullName);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BasicNameField_Resolves_AndIsATextObject()
    {
        var field = AccessTools.Field(Resolve(BasicCharacterTypeName), "_basicName");

        Assert.IsNotNull(field, "BasicCharacterObject._basicName did not resolve.");
        Assert.AreEqual("TaleWorlds.Localization.TextObject", field.FieldType.FullName);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BodyPropertyRangeSetter_Resolves_AndTakesAnMBBodyProperty()
    {
        var setter = AccessTools.PropertySetter(Resolve(BasicCharacterTypeName), "BodyPropertyRange");

        Assert.IsNotNull(setter,
            "BasicCharacterObject.BodyPropertyRange has no setter — the repair mirrors vanilla's "
            + "own fallback at BasicCharacterObject.cs:472-474 and needs it.");
        Assert.AreEqual(BodyPropertyTypeName, setter.GetParameters().Single().ParameterType.FullName);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void UpgradeTargetsSetter_Resolves_AndTakesACharacterObjectArray()
    {
        var setter = AccessTools.PropertySetter(Resolve(CharacterObjectTypeName), "UpgradeTargets");

        Assert.IsNotNull(setter,
            "CharacterObject.UpgradeTargets has no setter. It is an auto-property INITIALIZER, so "
            + "a save-restored object skips it and PartyCharacterVM:1113 reads .Length on null.");
        Assert.AreEqual(CharacterObjectTypeName + "[]",
            setter.GetParameters().Single().ParameterType.FullName);
    }

    // ---- the construction the repair performs --------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MBCharacterSkills_HasAParameterlessCtor_ThatBuildsItsSkillsOwner()
    {
        var type = Resolve(SkillsTypeName);

        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
        Assert.IsNotNull(type.GetProperty("Skills", BindingFlags.Public | BindingFlags.Instance),
            "MBCharacterSkills.Skills is what GetSkillValue derefs; the repair is pointless without it.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MBBodyProperty_TakesAStringId_AndExposesInit()
    {
        var type = Resolve(BodyPropertyTypeName);

        Assert.IsNotNull(type.GetConstructor(new[] { typeof(string) }),
            "the repair calls new MBBodyProperty(StringId), exactly as vanilla does.");
        Assert.IsNotNull(AccessTools.Method(type, "Init"),
            "vanilla follows the ctor with Init(min, max); the repair mirrors that.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void RegisterPresumedObject_Resolves()
        => Assert.IsNotNull(AccessTools.Method(Resolve(ObjectManagerTypeName), "RegisterPresumedObject"),
            "vanilla registers the MBBodyProperty it creates; the repair does the same.");

    // ---- registration --------------------------------------------------------------------

    [TestMethod]
    public void Patch_DeclaresTheExpectedCategory()
    {
        var attribute = typeof(Patch83_StaleCharacterRepair)
            .GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>()
            .SingleOrDefault();

        Assert.IsNotNull(attribute, "Patch83 must declare a HarmonyPatchCategory to be applied.");
        Assert.AreEqual(ExpectedCategory, attribute.info.category,
            "the category string must match the PatchCategory call in SubModule.OnSubModuleLoad.");
    }

    [TestMethod]
    public void Patch_ExposesResetForUnload_LikeEverySiblingHoldingAStaticService()
    {
        Assert.IsNotNull(
            typeof(Patch83_StaleCharacterRepair).GetMethod(
                "ResetForUnload", BindingFlags.Public | BindingFlags.Static),
            "a static injected service must be cleared on unload or a reloaded module runs "
            + "against a dead container.");
    }
}
