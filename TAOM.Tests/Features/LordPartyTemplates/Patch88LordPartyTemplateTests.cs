using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LordPartyTemplates.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LordPartyTemplates;

/// <summary>
/// Patch88 lets a named lord field his own party template (#580). The decision is pure
/// (<see cref="LordPartyTemplateResolutionTests"/>); what remains is engine surface the patch cannot
/// see change, pinned here as binding drift-guards, plus the wiring a one-character edit could
/// silently undo: the category literal, the SubModule registration, and the calls each patch body
/// must still make. The override itself needs a live campaign and is verified in game.
/// </summary>
[TestClass]
public class Patch88LordPartyTemplateTests
{
    private const string Category = "Patch88_LordPartyTemplate";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type EngineType(string fullName)
    {
        var type = AccessTools.TypeByName(fullName);
        Assert.IsNotNull(type, fullName + " did not resolve.");
        return type!;
    }

    // ---- the seam ----------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ClanDefaultPartyTemplate_StillAnInstanceGetterReturningAPartyTemplate()
    {
        RequireGame();

        var getter = AccessTools.PropertyGetter(EngineType("TaleWorlds.CampaignSystem.Clan"), "DefaultPartyTemplate");
        Assert.IsNotNull(getter, "Clan.DefaultPartyTemplate getter is gone; the postfix would never apply.");
        Assert.IsFalse(getter!.IsStatic, "Clan.DefaultPartyTemplate became static; __instance no longer means the clan being read.");
        Assert.AreEqual("PartyTemplateObject", getter.ReturnType.Name, "Clan.DefaultPartyTemplate no longer returns a PartyTemplateObject.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpawnLordParty_StillResolves_PrivateInstance_HeroAndIsNewGame()
    {
        RequireGame();

        var type = EngineType("TaleWorlds.CampaignSystem.CampaignBehaviors.HeroSpawnCampaignBehavior");
        const string name = "SpawnLordParty";

        var method = AccessTools.Method(type, name);
        Assert.IsNotNull(method, name + " did not resolve; the top-up scope would never apply.");
        Assert.IsFalse(method!.IsStatic, name + " became static.");
        Assert.IsTrue(method.IsPrivate, name + " is no longer private; the registry entry describes a private target.");

        // Harmony binds the prefix's `hero` by NAME, so a rename is a silent null. Pin the names.
        CollectionAssert.AreEqual(
            new[] { "hero", "isNewGame" },
            method.GetParameters().Select(p => p.Name).ToArray(),
            name + " parameter names drifted; update the Patch88 scope prefix to match.");

        Assert.AreEqual(1, type.GetMethods(AccessTools.all).Count(m => m.Name == name),
            name + " gained an overload; [HarmonyPatch] by name is now ambiguous.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void InitializeLordPartyProperties_StillResolves_OnTheNestedArgs_MobilePartyAndOwner()
    {
        RequireGame();

        var type = EngineType("TaleWorlds.CampaignSystem.Party.PartyComponents.LordPartyComponent+InitializationArgs");
        const string name = "InitializeLordPartyProperties";

        var method = AccessTools.Method(type, name);
        Assert.IsNotNull(method, name + " did not resolve; the initial-roster scope would never apply.");
        Assert.IsFalse(method!.IsStatic, name + " became static.");

        CollectionAssert.AreEqual(
            new[] { "mobileParty", "owner" },
            method.GetParameters().Select(p => p.Name).ToArray(),
            name + " parameter names drifted; update the Patch88 scope prefix to match.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BothVanillaReads_StillGoThroughTheClanGetter()
    {
        RequireGame();

        // The premise of the whole seam: both places that pick a lord's roster read
        // Clan.DefaultPartyTemplate. Call PRESENCE only; the branch shape is re-read per engine bump.
        var spawn = AccessTools.Method(EngineType("TaleWorlds.CampaignSystem.CampaignBehaviors.HeroSpawnCampaignBehavior"), "SpawnLordParty")!;
        var init = AccessTools.Method(EngineType("TaleWorlds.CampaignSystem.Party.PartyComponents.LordPartyComponent+InitializationArgs"), "InitializeLordPartyProperties")!;

        CollectionAssert.Contains(CalledNames(spawn).ToList(), "get_DefaultPartyTemplate",
            "SpawnLordParty no longer reads Clan.DefaultPartyTemplate; the top-up scope covers nothing.");
        CollectionAssert.Contains(CalledNames(init).ToList(), "get_DefaultPartyTemplate",
            "InitializeLordPartyProperties no longer reads Clan.DefaultPartyTemplate; the initial-roster scope covers nothing.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PostfixBody_EngineMembers_StillResolve()
    {
        RequireGame();

        // Every engine member the postfix body references. A JIT-time resolution failure never
        // enters the body's own try, so each one needs its own pin.
        var hero = EngineType("TaleWorlds.CampaignSystem.Hero");
        var clan = EngineType("TaleWorlds.CampaignSystem.Clan");
        var objectManager = EngineType("TaleWorlds.ObjectSystem.MBObjectManager");

        Assert.IsNotNull(AccessTools.PropertyGetter(hero, "StringId"), "Hero.StringId getter is gone.");
        Assert.IsNotNull(AccessTools.PropertyGetter(hero, "Clan"), "Hero.Clan getter is gone.");
        Assert.IsNotNull(AccessTools.PropertyGetter(clan, "StringId"), "Clan.StringId getter is gone.");
        Assert.IsNotNull(AccessTools.PropertyGetter(objectManager, "Instance"), "MBObjectManager.Instance getter is gone.");

        var getObject = objectManager.GetMethods(AccessTools.all)
            .FirstOrDefault(m => m.Name == "GetObject" && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
        Assert.IsNotNull(getObject, "MBObjectManager.GetObject<T>(string) is gone; the template lookup would not compile against this engine.");
    }

    // ---- the wiring --------------------------------------------------------------------------

    [TestMethod]
    public void AllThreePatches_CarryTheSameCategoryLiteral()
    {
        foreach (var type in new[]
        {
            typeof(Patch88_LordPartyTemplate),
            typeof(Patch88_SpawnLordPartyScope),
            typeof(Patch88_InitializeLordPartyPropertiesScope),
        })
        {
            var category = type.GetCustomAttribute<HarmonyPatchCategory>();
            Assert.IsNotNull(category, type.Name + " has no [HarmonyPatchCategory]; it is dead code.");
            Assert.AreEqual(Category, category!.info.category, type.Name + " is in the wrong category.");
        }
    }

    [TestMethod]
    public void SubModule_AppliesTheCategory_InitializesThePatch_AndResetsItOnUnload()
    {
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), "Main", "SubModule.cs"));

        StringAssert.Contains(source, "_harmony.PatchCategory(\"" + Category + "\")",
            "SubModule.cs never applies " + Category + "; all three patches are dead code.");
        StringAssert.Contains(source, "Patch88_LordPartyTemplate.Initialize(",
            "SubModule.cs never hands the patch its service; the postfix would return on every call.");
        StringAssert.Contains(source, "Patch88_LordPartyTemplate.ResetForUnload()",
            "SubModule.cs never resets the patch on unload; a reload would keep a disposed logger.");
    }

    [TestMethod]
    public void Postfix_StillCallsTheDecisionAndTheObjectManager()
    {
        var postfix = typeof(Patch88_LordPartyTemplate).GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static)!;

        CollectionAssert.IsSubsetOf(
            new[] { "Resolve", "GetObject" },
            CalledNames(postfix).ToList(),
            "the postfix no longer routes through LordPartyTemplateResolution.Resolve and MBObjectManager.GetObject.");
    }

    [TestMethod]
    public void BothScopes_PrefixAndFinalizer_StillWriteTheAmbientOwner()
    {
        foreach (var type in new[] { typeof(Patch88_SpawnLordPartyScope), typeof(Patch88_InitializeLordPartyPropertiesScope) })
        foreach (var name in new[] { "Prefix", "Finalizer" })
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;
            CollectionAssert.Contains(CalledNames(method).ToList(), "set_AmbientOwner",
                type.Name + "." + name + " no longer writes the ambient owner; the scope is open or never closes.");
        }
    }

    // ---- the behaviour reachable without a campaign ---------------------------------------

    [TestMethod]
    public void Finalizers_ReturnTheExceptionUntouched_AndRestoreTheSavedOwner()
    {
        // Patch65 finalizes SpawnLordParty too and decides what is swallowed; this one must be
        // transparent or it overwrites that decision in the shared exception slot.
        var ex = new InvalidOperationException("vanilla");

        Assert.AreSame(ex, Patch88_SpawnLordPartyScope.Finalizer(ex, null));
        Assert.IsNull(Patch88_SpawnLordPartyScope.Finalizer(null, null));
        Assert.AreSame(ex, Patch88_InitializeLordPartyPropertiesScope.Finalizer(ex, null));
        Assert.IsNull(Patch88_LordPartyTemplate.AmbientOwner);
    }

    [TestMethod]
    public void ResetForUnload_ClearsTheAmbientOwner()
    {
        Patch88_LordPartyTemplate.ResetForUnload();

        Assert.IsNull(Patch88_LordPartyTemplate.AmbientOwner);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static HashSet<string> CalledNames(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, method.Name + " has no readable IL body.");

        var names = new HashSet<string>(
            IlCallScanner.ExtractCalledMethods(method, il!).Select(m => m.Name), StringComparer.Ordinal);

        Assert.AreNotEqual(0, names.Count, method.Name + " resolved no calls; the scan failed, not the method.");
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
