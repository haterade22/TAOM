using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BanditManagement.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BanditManagement;

/// <summary>
/// Drift-guards for Patch86, the hideout boss-fight sizing (#564). Two prefixes, one category:
///
/// - <see cref="Patch86_HideoutAssaultBossFight"/> replaces
///   <c>Helpers.MapEventHelper.GetPriorityListForHideoutMission(List&lt;MobileParty&gt;, out int)</c>,
///   whose single caller is <c>SandBoxMissions.OpenHideoutBattleMission</c>. Harmony binds the
///   <c>out</c> parameter by NAME, so a rename would make the prefix a silent no-op.
/// - <see cref="Patch86_HideoutAmbushBossFight"/> prefixes the private
///   <c>HideoutAmbushMissionController.SpawnRemainingTroopsForBossFight(List&lt;MatrixFrame&gt;, int)</c>
///   and injects three private fields. A renamed field throws while applying the WHOLE category.
///
/// Both bodies reference engine members that a JIT-time resolution failure would surface at the
/// patched target (harmony-il.md: a patch's own try/catch cannot survive that), so every such
/// member is pinned. The behaviour is covered by <c>HideoutBossFightServiceTests</c>.
/// </summary>
[TestClass]
public class Patch86HideoutBossFightBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type Resolve(string fullName)
    {
        var type = AccessTools.TypeByName(fullName);
        Assert.IsNotNull(type, fullName + " did not resolve.");
        return type;
    }

    private static Type MapEventHelperType() => Resolve("Helpers.MapEventHelper");
    private static Type AmbushControllerType() => Resolve("SandBox.Missions.MissionLogics.Hideout.HideoutAmbushMissionController");

    // ------------------------------------------------------------------ assault target

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetPriorityListForHideoutMission_ResolvesPublicStatic_WithNamedOutParameter()
    {
        RequireGame();

        var method = AccessTools.Method(MapEventHelperType(), "GetPriorityListForHideoutMission");
        Assert.IsNotNull(method, "MapEventHelper.GetPriorityListForHideoutMission did not resolve — the assault prefix has no target.");
        Assert.IsTrue(method.IsPublic && method.IsStatic, "the target stopped being public static; nameof() in the attribute would not compile.");

        var overloads = MapEventHelperType().GetMethods(AccessTools.all).Count(m => m.Name == "GetPriorityListForHideoutMission");
        Assert.AreEqual(1, overloads, "the target gained an overload — [HarmonyPatch] by name is now ambiguous.");

        // Harmony injects by NAME. `partyList` and `firstPhaseTroopCount` are the prefix's own
        // parameter names; a rename here leaves the prefix's parameters null and the patch silent.
        var parameters = method.GetParameters();
        CollectionAssert.AreEqual(new[] { "partyList", "firstPhaseTroopCount" }, parameters.Select(p => p.Name).ToArray(),
            "parameter names changed — the prefix binds by name and would receive nothing.");
        Assert.IsTrue(parameters[1].IsOut, "firstPhaseTroopCount is no longer an out parameter — re-derive the prefix signature.");
        Assert.AreEqual("FlattenedTroopRoster", method.ReturnType.Name, "the return type changed — __result no longer matches.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AssaultPrefixBodyEngineMembers_StillExist()
    {
        RequireGame();

        // Everything the replacement body touches. A missing member here raises at JIT time of the
        // prefix, before its try/catch, and PatchShield then returns default from the target.
        var roster = Resolve("TaleWorlds.CampaignSystem.Roster.FlattenedTroopRoster");
        Assert.IsNotNull(AccessTools.Constructor(roster, new[] { typeof(int) }), "FlattenedTroopRoster(int) ctor is gone.");
        Assert.IsNotNull(roster.GetMethods(AccessTools.all).FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 1),
            "FlattenedTroopRoster.Add(...) is gone.");
        Assert.IsNotNull(roster.GetMethods(AccessTools.all).FirstOrDefault(m => m.Name == "RemoveIf"), "FlattenedTroopRoster.RemoveIf is gone.");
        Assert.IsTrue(typeof(IEnumerable<>).MakeGenericType(Resolve("TaleWorlds.CampaignSystem.Roster.FlattenedTroopRosterElement")).IsAssignableFrom(roster),
            "FlattenedTroopRoster is no longer enumerable as FlattenedTroopRosterElement.");

        var element = Resolve("TaleWorlds.CampaignSystem.Roster.FlattenedTroopRosterElement");
        Assert.IsNotNull(AccessTools.Property(element, "IsWounded"), "FlattenedTroopRosterElement.IsWounded is gone.");
        Assert.IsNotNull(AccessTools.Property(element, "Troop"), "FlattenedTroopRosterElement.Troop is gone.");
        Assert.IsNotNull(AccessTools.Property(element, "Descriptor"), "FlattenedTroopRosterElement.Descriptor is gone.");

        var troopRoster = Resolve("TaleWorlds.CampaignSystem.Roster.TroopRoster");
        Assert.IsNotNull(AccessTools.Property(troopRoster, "TotalHealthyCount"), "TroopRoster.TotalHealthyCount is gone.");
        Assert.IsNotNull(AccessTools.Method(troopRoster, "GetTroopRoster"), "TroopRoster.GetTroopRoster is gone.");

        var culture = Resolve("TaleWorlds.CampaignSystem.CultureObject");
        Assert.IsNotNull(AccessTools.Property(culture, "BanditBoss"), "CultureObject.BanditBoss is gone — the boss can no longer be told apart from a regular.");

        var character = Resolve("TaleWorlds.CampaignSystem.CharacterObject");
        Assert.IsNotNull(AccessTools.Property(character, "IsHero"), "CharacterObject.IsHero is gone.");
        Assert.IsNotNull(AccessTools.Property(character, "Level"), "CharacterObject.Level is gone.");
        // CharacterObject.Culture is declared `new` over BasicCharacterObject.Culture (CultureObject vs
        // BasicCultureObject), so the base-walking lookup is ambiguous; the prefix reads the derived one.
        var cultureProperty = AccessTools.DeclaredProperty(character, "Culture");
        Assert.IsNotNull(cultureProperty, "CharacterObject.Culture is gone.");
        Assert.AreEqual("CultureObject", cultureProperty.PropertyType.Name, "CharacterObject.Culture no longer returns CultureObject — .BanditBoss would not resolve on it.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void BossPhaseCap_StillFeedsTheCampaignTrim()
    {
        RequireGame();

        // The premise of the model half: ArrangeHideoutTroopCountsForMission trims the hideout to
        // FirstFightMax + NumberOfMaximumTroopCountForBossFightInHideout. If it stops reading the
        // boss-phase cap, TaomBanditDensityModel's value no longer bounds anything and phase 1
        // grows by the difference.
        var behavior = Resolve("TaleWorlds.CampaignSystem.CampaignBehaviors.HideoutCampaignBehavior");
        var arrange = AccessTools.Method(behavior, "ArrangeHideoutTroopCountsForMission");
        Assert.IsNotNull(arrange, "HideoutCampaignBehavior.ArrangeHideoutTroopCountsForMission is gone — re-derive where the hideout is trimmed.");

        var called = CalledNames(arrange);
        Assert.IsTrue(called.Contains("get_NumberOfMaximumTroopCountForBossFightInHideout"),
            "ArrangeHideoutTroopCountsForMission no longer reads NumberOfMaximumTroopCountForBossFightInHideout — the model override is dead.");
        Assert.IsTrue(called.Contains("get_NumberOfMaximumTroopCountForFirstFightInHideout"),
            "ArrangeHideoutTroopCountsForMission no longer reads NumberOfMaximumTroopCountForFirstFightInHideout.");
    }

    // ------------------------------------------------------------------ ambush target

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpawnRemainingTroopsForBossFight_ResolvesPrivateInstance_WithNamedParameters()
    {
        RequireGame();

        var method = AccessTools.Method(AmbushControllerType(), "SpawnRemainingTroopsForBossFight");
        Assert.IsNotNull(method, "HideoutAmbushMissionController.SpawnRemainingTroopsForBossFight did not resolve — the ambush prefix has no target.");
        Assert.IsFalse(method.IsStatic, "the target became static — the injected instance fields would not bind.");

        var overloads = AmbushControllerType().GetMethods(AccessTools.all).Count(m => m.Name == "SpawnRemainingTroopsForBossFight");
        Assert.AreEqual(1, overloads, "the target gained an overload — [HarmonyPatch] by name is now ambiguous.");

        var parameters = method.GetParameters();
        CollectionAssert.AreEqual(new[] { "spawnFrames", "spawnCount" }, parameters.Select(p => p.Name).ToArray(),
            "parameter names changed — `ref int spawnCount` binds by name and would receive nothing.");
        Assert.AreEqual(typeof(int), parameters[1].ParameterType, "spawnCount is no longer an int.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void InjectedFields_ResolveWithTheTypesTheParametersDeclare()
    {
        RequireGame();

        // ____allEnemyTroops etc. bind by field name (three underscores + the literal name) AND by
        // type: a type change throws at apply time for the whole category.
        var origin = Resolve("TaleWorlds.Core.IAgentOriginBase");
        var listOfOrigin = typeof(List<>).MakeGenericType(origin);

        AssertField("_allEnemyTroops", listOfOrigin);
        AssertField("_allEnemyTroopTypesCache", listOfOrigin);
        AssertField("_overriddenHideoutBossAgentOrigin", origin);
    }

    private static void AssertField(string name, Type expected)
    {
        var field = AccessTools.Field(AmbushControllerType(), name);
        Assert.IsNotNull(field, $"HideoutAmbushMissionController.{name} is gone — the ambush prefix would fail to apply and take the whole Patch86 category with it.");
        Assert.AreEqual(expected, field.FieldType, $"HideoutAmbushMissionController.{name} changed type — the injected parameter would not bind.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void AmbushPadding_StillDrawsFromTheTypeCache()
    {
        RequireGame();

        // The prefix passes canPad = (____allEnemyTroopTypesCache.Count > 0) because vanilla's
        // GetNewRandomEnemyTroop derefs GetRandomElement on that cache. If the padding helper is
        // gone or reads another field, the guard is protecting the wrong thing.
        var pad = AccessTools.Method(AmbushControllerType(), "GetNewRandomEnemyTroop");
        Assert.IsNotNull(pad, "HideoutAmbushMissionController.GetNewRandomEnemyTroop is gone — re-derive the padding premise.");

        var spawn = AccessTools.Method(AmbushControllerType(), "SpawnRemainingTroopsForBossFight");
        Assert.IsTrue(CalledNames(spawn).Contains("GetNewRandomEnemyTroop"),
            "SpawnRemainingTroopsForBossFight no longer pads via GetNewRandomEnemyTroop — spawnCount may no longer be a floor.");
    }

    // ------------------------------------------------------------------ the patch classes

    [TestMethod]
    public void AssaultPrefix_StillCallsPlanAssaultAndRemoveIf()
    {
        var called = CalledNames(PrefixOf(typeof(Patch86_HideoutAssaultBossFight)));
        Assert.IsTrue(called.Contains("PlanAssault"), "the assault prefix no longer asks the service for the split — the seam has been gutted.");
        Assert.IsTrue(called.Contains("RemoveIf"), "the assault prefix no longer filters the roster it returns.");
    }

    [TestMethod]
    public void AmbushPrefix_StillCallsPlanAmbush()
    {
        var called = CalledNames(PrefixOf(typeof(Patch86_HideoutAmbushBossFight)));
        Assert.IsTrue(called.Contains("PlanAmbush"), "the ambush prefix no longer asks the service which troops to keep — the seam has been gutted.");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void PatchClasses_AreRegisteredInAllThreePlaces()
    {
        // Patch39 shipped missing its category attribute and every review agent missed it
        // (lessons/harmony-il.md), so all three places are asserted rather than assumed.
        foreach (var patch in new[] { typeof(Patch86_HideoutAssaultBossFight), typeof(Patch86_HideoutAmbushBossFight) })
        {
            var target = patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Cast<HarmonyPatch>().ToList();
            Assert.AreEqual(1, target.Count, patch.Name + " lost its [HarmonyPatch] target attribute.");
            Assert.IsNotNull(target[0].info.declaringType,
                patch.Name + " must carry typeof(<target>) in its attribute: HarmonyFieldInjectionNamingTests only scans classes that do.");

            var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                .Cast<HarmonyPatchCategory>()
                .Select(c => c.info.category)
                .ToList();
            CollectionAssert.Contains(categories, Patch86_HideoutBossFight.Category,
                patch.Name + " lost its [HarmonyPatchCategory] — SubModule's PatchCategory call would apply nothing.");
        }

        Assert.AreEqual("Patch86_HideoutBossFight", Patch86_HideoutBossFight.Category, "the category literal moved — update SubModule.cs and the registry.");

        var subModule = Path.Combine(FindRepoRoot(), "Main", "SubModule.cs");
        Assert.IsTrue(File.Exists(subModule), $"SubModule.cs not found at {subModule}");
        var source = File.ReadAllText(subModule);
        StringAssert.Contains(source, "_harmony.PatchCategory(\"Patch86_HideoutBossFight\")",
            "SubModule.cs no longer applies Patch86_HideoutBossFight — both prefixes are dead code.");
        StringAssert.Contains(source, "Patch86_HideoutBossFight.Initialize(",
            "SubModule.cs no longer initialises Patch86 — the prefixes would have no service and defer to vanilla forever.");
        StringAssert.Contains(source, "Patch86_HideoutBossFight.ResetForUnload()",
            "SubModule.cs no longer resets Patch86 on unload — a stale resolver would outlive the container.");
    }

    // ------------------------------------------------------------------ helpers

    private static MethodInfo PrefixOf(Type patchType)
    {
        var method = patchType.GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, patchType.Name + ".Prefix is gone.");
        return method;
    }

    private static HashSet<string> CalledNames(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, method.Name + " has no readable IL body.");

        var names = new HashSet<string>(
            IlCallScanner.ExtractCalledMethods(method, il).Select(m => m.Name), StringComparer.Ordinal);

        Assert.AreNotEqual(0, names.Count, method.Name + " resolved no calls — the scan failed, not the method.");
        return names;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }
}
