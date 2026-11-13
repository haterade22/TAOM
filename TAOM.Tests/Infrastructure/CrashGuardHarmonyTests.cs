using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Encounters;
using TAOM.Dependencies.Foundation;
using TAOM.Features.MapEventGuard.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// PatchShield strips every unprotected owner's prefixes, postfixes and transpilers on a method after a
/// missing-API exception there, and "com.taom.mod" is not protected. A crash guard that shares its method with
/// another TAOM patch would go with it when that other patch breaks after a game update: on
/// PlayerEncounter.DoMeeting a broken Refuge or Supply Lines patch would also remove the #1099 empty-meeting
/// guard (Patch143). Crash guards are therefore applied under their own "TAOM.*" Harmony id, which
/// PatchShieldPolicy protects.
/// </summary>
[TestClass]
public class CrashGuardHarmonyTests
{
    private const string GuardOwner = "TAOM.CrashGuards";

    private static readonly BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void IsCrashGuard_EmptyMeetingGuard_IsTrue()
    {
        Assert.IsTrue(CrashGuardHarmony.IsCrashGuard(Patch143_EmptyMeetingGuard.Category));
    }

    [TestMethod]
    public void IsCrashGuard_OrdinaryCategory_IsFalse()
    {
        Assert.IsFalse(CrashGuardHarmony.IsCrashGuard("Patch75_Refuge"));
    }

    [TestMethod]
    public void CrashGuardId_IsProtectedFromThePatchShieldStrip()
    {
        Assert.IsTrue(PatchShieldPolicy.IsProtectedOwner(
            CrashGuardHarmony.Id, PatchShieldPolicy.CompiledProtectedOwnerPrefixes));
    }

    [TestMethod]
    public void MainId_IsNotProtected_SoABrokenFeaturePatchCanStillBeStripped()
    {
        Assert.IsFalse(PatchShieldPolicy.IsProtectedOwner(
            CrashGuardHarmony.MainId, PatchShieldPolicy.CompiledProtectedOwnerPrefixes));
    }

    [TestMethod]
    public void SubModule_AppliesCrashGuardsUnderTheirOwnHarmonyId()
    {
        var source = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        StringAssert.Contains(source, "new Harmony(CrashGuardHarmony.Id)");
        StringAssert.Contains(source, "CrashGuardHarmony.IsCrashGuard(category) ? _guardHarmony : _harmony");
        StringAssert.Contains(source, "_harmony?.UnpatchAll(CrashGuardHarmony.MainId)");
        StringAssert.Contains(source, "_guardHarmony?.UnpatchAll(CrashGuardHarmony.Id)");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void CategoryIndex_AppliesPatch143UnderTheCrashGuardOwnerOnTheRealTarget()
    {
        RequireGame();

        // The two instances SubModule builds, with the selection it passes to PatchCategoryApplier.
        var guardHarmony = new Harmony(CrashGuardHarmony.Id);
        var mainHarmony = new Harmony("taom.tests.crashguard-main");
        try
        {
            var category = Patch143_EmptyMeetingGuard.Category;
            PatchCategoryIndex.Build(typeof(Patch143_EmptyMeetingGuard).Assembly)
                .Apply(CrashGuardHarmony.IsCrashGuard(category) ? guardHarmony : mainHarmony, category);

            var target = AccessTools.Method(typeof(PlayerEncounter), "DoMeeting");
            var prefixes = Harmony.GetPatchInfo(target)?.Prefixes;
            Assert.IsNotNull(prefixes, "applying the category patched nothing");
            Assert.IsTrue(prefixes.Any(p => p.owner == GuardOwner && p.PatchMethod.DeclaringType == typeof(Patch143_EmptyMeetingGuard)),
                "Patch143 is not owned by TAOM.CrashGuards.");
            Assert.IsFalse(prefixes.Any(p => p.owner == mainHarmony.Id), "Patch143 landed under the main instance.");
        }
        finally
        {
            guardHarmony.UnpatchAll(CrashGuardHarmony.Id);
            mainHarmony.UnpatchAll(mainHarmony.Id);
        }
    }

    // The admission rule: a guard's patch methods name no TaleWorlds member. The JIT raises a missing-member
    // exception for a method before its try runs, so the guard would throw on every call and, as a protected
    // owner, never be stripped. Parameters of the patch method are the signature Harmony binds and are not
    // read: only method bodies (IL operands), their locals and their catch types are.
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void CrashGuardPatchClasses_NameNoTaleWorldsMember()
    {
        RequireGame();

        var guards = TaomAssemblies.All.SelectMany(AccessTools.GetTypesFromAssembly)
            .Where(t => t.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                .Cast<HarmonyPatchCategory>().Any(c => CrashGuardHarmony.IsCrashGuard(c.info.category)))
            .ToList();

        CollectionAssert.Contains(guards, typeof(Patch143_EmptyMeetingGuard),
            "the scan found no patch class for a crash-guard category");
        foreach (var guard in guards)
            CollectionAssert.AreEqual(new string[0], EngineReferencesIn(guard), guard.FullName);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EngineReferencesIn_BodyNamesAnEngineMember_ReportsItEvenInsideATry()
    {
        RequireGame();

        var found = EngineReferencesIn(typeof(ProbeNamingAnEngineMember));

        Assert.IsTrue(found.Any(r => r.Contains("PlayerEncounter")), string.Join("; ", found));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EngineReferencesIn_EngineTypeOnlyInASignature_ReportsNothing()
    {
        RequireGame();

        CollectionAssert.AreEqual(new string[0], EngineReferencesIn(typeof(ProbeWithEngineParameter)));
    }

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static class ProbeNamingAnEngineMember
    {
        internal static bool Prefix()
        {
            try { return PlayerEncounter.Current == null; }
            catch (Exception) { return true; }
        }
    }

    private static class ProbeWithEngineParameter
    {
        internal static bool Prefix(PlayerEncounter __instance) => __instance == null;
    }

    private static List<string> EngineReferencesIn(Type type)
    {
        var found = new List<string>();
        var methods = type.GetMethods(AllDeclared).Cast<MethodBase>().Concat(type.GetConstructors(AllDeclared));
        foreach (var method in methods)
            ScanBody(method, found);
        foreach (var nested in type.GetNestedTypes(AllDeclared))
            found.AddRange(EngineReferencesIn(nested));
        return found;
    }

    private static void ScanBody(MethodBase method, List<string> found)
    {
        var body = method.GetMethodBody();
        if (body == null) return;

        foreach (var local in body.LocalVariables)
            if (IsEngineType(local.LocalType)) found.Add($"{Describe(method)}: local {local.LocalType}");
        foreach (var clause in body.ExceptionHandlingClauses)
            if (clause.Flags == ExceptionHandlingClauseOptions.Clause && IsEngineType(clause.CatchType))
                found.Add($"{Describe(method)}: catch {clause.CatchType}");

        foreach (var member in IlCallScanner.ExtractReferencedMembers(method, body.GetILAsByteArray()))
            if (IsEngineMember(member)) found.Add($"{Describe(method)}: {member.DeclaringType}::{member.Name}");
    }

    private static string Describe(MethodBase method) => $"{method.DeclaringType.Name}.{method.Name}";

    private static bool IsEngineMember(MemberInfo member) =>
        member is Type type ? IsEngineType(type)
        : IsEngineType(member.DeclaringType)
          || (member is MethodInfo m && m.IsGenericMethod && m.GetGenericArguments().Any(IsEngineType));

    private static bool IsEngineType(Type type)
    {
        if (type == null) return false;
        if (type.IsArray || type.IsByRef || type.IsPointer) return IsEngineType(type.GetElementType());
        if (type.Assembly.GetName().Name.StartsWith("TaleWorlds.", StringComparison.Ordinal)) return true;
        return type.IsGenericType && type.GetGenericArguments().Any(IsEngineType);
    }
}
