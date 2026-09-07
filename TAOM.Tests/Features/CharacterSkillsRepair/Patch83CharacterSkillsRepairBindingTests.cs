using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CharacterSkillsRepair.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CharacterSkillsRepair;

/// <summary>
/// Drift-guard for Patch83. The repair rests on two engine bindings the compiler cannot check, and
/// BOTH fail silently rather than loudly:
///
/// <list type="bullet">
/// <item>the patch TARGET, <c>MBObjectManager.AfterLoad</c> — if it is renamed or re-signatured,
/// Harmony throws at category-apply time, SubModule's guarded loop logs it and carries on, and the
/// repair simply never runs;</item>
/// <item>the reflected FIELD, <c>BasicCharacterObject.DefaultCharacterSkills</c> — if it is renamed,
/// <c>AccessTools.Field</c> returns null and <c>TryGiveEmptySkillSet</c> returns false for every
/// character, so the sweep reports "could NOT repair" for a save that used to load.</item>
/// </list>
///
/// Either drift turns a shipped crash fix back into the crash it fixed, with nothing louder than a
/// log line to say so. That is what this class exists to catch first.
///
/// It does not and cannot prove the repair BEHAVES: constructing a save-restored
/// <c>CharacterObject</c> with a null skill set needs a live campaign.
/// <see cref="CharacterSkillsRepairServiceTests"/> covers the decision logic instead.
/// </summary>
[TestClass]
public class Patch83CharacterSkillsRepairBindingTests
{
    private const string ExpectedCategory = "Patch83_CharacterSkillsRepair";
    private const string ObjectManagerTypeName = "TaleWorlds.ObjectSystem.MBObjectManager";
    private const string BasicCharacterTypeName = "TaleWorlds.Core.BasicCharacterObject";
    private const string SkillsTypeName = "TaleWorlds.Core.MBCharacterSkills";

    // The field CharacterSkillsAdapter reflects. Protected, no setter — the three vanilla paths
    // that assign it (Deserialize, FillFrom, InitializeHeroBasicCharacterOnAfterLoad) are all
    // unreachable for a character restored from a save whose XML definition is gone.
    private const string SkillsFieldName = "DefaultCharacterSkills";

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

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AfterLoad_TargetResolves_AgainstInstalledEngine()
    {
        var method = AccessTools.Method(Resolve(ObjectManagerTypeName), "AfterLoad");

        Assert.IsNotNull(method,
            "MBObjectManager.AfterLoad did not resolve. Patch83 is applied to it because it runs at "
            + "Campaign.OnGameLoaded:687, immediately before the CampaignObjectManager.AfterLoad "
            + "call that crashes — re-verify the ordering before rebinding elsewhere.");
        Assert.AreEqual(0, method.GetParameters().Length,
            "AfterLoad gained parameters — the postfix signature must be re-checked.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void DefaultCharacterSkillsField_Resolves_AndIsTheTypeTheRepairAssigns()
    {
        var field = AccessTools.Field(Resolve(BasicCharacterTypeName), SkillsFieldName);

        Assert.IsNotNull(field,
            $"BasicCharacterObject.{SkillsFieldName} did not resolve. CharacterSkillsAdapter writes "
            + "this field by reflection; without it every repair silently fails.");
        Assert.AreEqual(SkillsTypeName, field.FieldType.FullName,
            "the field's type changed — CharacterSkillsAdapter assigns a new MBCharacterSkills.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetDefaultCharacterSkills_IsPublic_SoTheScanNeedsNoReflection()
    {
        var getter = AccessTools.Method(Resolve(BasicCharacterTypeName), "GetDefaultCharacterSkills");

        Assert.IsNotNull(getter, "GetDefaultCharacterSkills did not resolve — the scan reads it.");
        Assert.IsTrue(getter.IsPublic,
            "the getter went non-public; the adapter's scan would need reflection too.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MBCharacterSkills_HasAParameterlessCtor_ThatBuildsItsSkillsOwner()
    {
        var type = Resolve(SkillsTypeName);

        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes),
            "the repair does `new MBCharacterSkills()` — vanilla's own fallback for a troop that "
            + "declares no skill_template and no <skills> block.");
        var skills = type.GetProperty("Skills", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(skills,
            "MBCharacterSkills.Skills is what GetSkillValue derefs; the repair is pointless without it.");
    }

    [TestMethod]
    public void Patch_DeclaresTheExpectedCategory()
    {
        var attribute = typeof(Patch83_CharacterSkillsRepair)
            .GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>()
            .SingleOrDefault();

        Assert.IsNotNull(attribute, "Patch83 must declare a HarmonyPatchCategory to be applied.");
        Assert.AreEqual(ExpectedCategory, attribute.info.category,
            "the category string must match the PatchCategory call in SubModule.OnSubModuleLoad.");
    }
}
